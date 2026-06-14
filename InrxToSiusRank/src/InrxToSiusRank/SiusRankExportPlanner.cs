namespace InrxToSiusRank;

internal static class SiusRankExportPlanner
{
    public static SiusRankExportPlan Build(SiusRankCsvExportOptions options)
    {
        if (options.OutputDirectory is null)
        {
            throw new ArgumentException("File export requires --output-dir.");
        }

        using var repository = new InrxRepository(options.DatabasePath);
        var stevner = ResolveStevner(repository, options);
        var shooterGroupsTemplate = options.ShooterGroupsTemplatePath is null
            ? null
            : ShooterGroupsTemplate.Load(options.ShooterGroupsTemplatePath);

        var outputDirectory = Path.GetFullPath(options.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var eventExports = ResolveEventExports(repository, options, stevner);
        ValidateSilhouetteTargets(eventExports, options.SilhouetteShootersPerStand);

        var startNumbers = ChampionshipStartNumbers.Create(
            eventExports.SelectMany(eventExport => eventExport.Starters),
            eventExports.Select(eventExport => eventExport.Stevne),
            Path.Combine(outputDirectory, ChampionshipStartNumbers.BibMapFileName));

        var files = eventExports
            .SelectMany(eventExport => ResolveFileExports(eventExport, options.FinalClasses ?? []))
            .Select(fileExport => BuildFile(fileExport, startNumbers, shooterGroupsTemplate, options.SilhouetteShootersPerStand))
            .ToList();

        return new SiusRankExportPlan(outputDirectory, options.ShooterGroupsTemplatePath, files);
    }

    private static SiusRankPlannedFile BuildFile(
        EventFileExport fileExport,
        IReadOnlyDictionary<int, string> startNumbers,
        ShooterGroupsTemplate? shooterGroupsTemplate,
        int silhouetteShootersPerStand)
    {
        var starters = fileExport.Starters
            .OrderBy(item => EffectiveKmNmClass.SortKey(item.EffectiveClass))
            .ThenBy(item => item.EffectiveClass, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Index)
            .ToList();

        var rows = starters
            .Select(item => StarterMapper.Map(
                item.Starter with { KmNmClass = item.EffectiveClass },
                siusGroupOverride: null,
                includeClubTeam: true,
                startNumber: startNumbers[item.Starter.DeltakerId]))
            .ToList();

        ExportValidator.ValidateShooterGroups(rows, shooterGroupsTemplate);

        return new SiusRankPlannedFile(
            fileExport.Stevne,
            fileExport.Ovelse,
            fileExport.SiusEventCode,
            FormatGroupSummary(rows),
            rows,
            IncludeSilhouetteImportColumns(fileExport.Ovelse, silhouetteShootersPerStand),
            ExportValidator.Validate(rows).ToList());
    }

    private static string FormatGroupSummary(IReadOnlyList<SiusRankStarter> rows) =>
        string.Join(
            ",",
            rows.Select(row => row.Groups)
                .Where(group => !string.IsNullOrWhiteSpace(group))
                .Distinct(StringComparer.OrdinalIgnoreCase));

    private static void ValidateSilhouetteTargets(
        IReadOnlyList<EventExport> eventExports,
        int silhouetteShootersPerStand)
    {
        foreach (var eventExport in eventExports)
        {
            ExportValidator.EnsureValidInrxSilhouetteTargets(
                eventExport.Starters,
                eventExport.Ovelse,
                silhouetteShootersPerStand);
        }
    }

    private static bool IncludeSilhouetteImportColumns(OvelseInfo ovelse, int silhouetteShootersPerStand) =>
        silhouetteShootersPerStand == 2 && ExportValidator.IsSilhouette(ovelse);

    private static IReadOnlyList<EventFileExport> ResolveFileExports(
        EventExport eventExport,
        IReadOnlyList<string> finalClasses)
    {
        var starters = eventExport.Starters
            .Select((starter, index) => new StarterExportItem(
                starter,
                EffectiveKmNmClass.Resolve(starter, eventExport.Ovelse),
                index))
            .ToList();

        var finalClassSet = SiusRankCsvFinalClassRules.ResolveFor(eventExport.Ovelse, finalClasses);
        if (finalClassSet.Count == 0)
        {
            return [new EventFileExport(eventExport.Stevne, eventExport.Ovelse, SiusEventCode: null, starters)];
        }

        var result = starters
            .Where(item => IsFinalClass(item.EffectiveClass, finalClassSet))
            .GroupBy(
                item => OutputFileName.EventFilterForImport(eventExport.Ovelse, item.EffectiveClass),
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new EventFileExport(
                eventExport.Stevne,
                eventExport.Ovelse,
                group.Key,
                group.ToList()))
            .ToList();

        var combined = starters
            .Where(item => !IsFinalClass(item.EffectiveClass, finalClassSet))
            .ToList();

        if (combined.Count > 0)
        {
            result.Add(new EventFileExport(eventExport.Stevne, eventExport.Ovelse, SiusEventCode: null, combined));
        }

        return result;
    }

    private static bool IsFinalClass(string effectiveClass, IReadOnlySet<string> finalClassSet) =>
        finalClassSet.Contains(effectiveClass.Trim()) ||
        finalClassSet.Contains(GroupNormalizer.Normalize(effectiveClass));

    private static IReadOnlyList<StevneInfo> ResolveStevner(InrxRepository repository, SiusRankCsvExportOptions options)
    {
        if (options.StevneIds.Count > 0)
        {
            return options.StevneIds.Select(repository.GetStevneById).ToList();
        }

        if (options.StevneId is not null)
        {
            return [repository.GetStevneById(options.StevneId.Value)];
        }

        return [repository.ResolveStevne(options)];
    }

    private static IReadOnlyList<OvelseInfo> ResolveOvelser(
        InrxRepository repository,
        SiusRankCsvExportOptions options,
        StevneInfo stevne)
    {
        if (options.OvelseId is not null || !string.IsNullOrWhiteSpace(options.OvelseName))
        {
            return [repository.ResolveOvelse(options)];
        }

        var ovelser = repository.GetOvelserForStevne(stevne.Id);
        if (ovelser.Count == 0)
        {
            throw new InvalidOperationException($"No exercises found for Stevne.Id={stevne.Id}.");
        }

        if (ovelser.Count == 1 || options.StevneIds.Count > 0)
        {
            return ovelser
                .Select(ovelse => new OvelseInfo(
                    ovelse.Id,
                    ovelse.Name,
                    ovelse.ShortName,
                    ovelse.HovedOvelseId))
                .ToList();
        }

        throw new InvalidOperationException(
            $"Stevne.Id={stevne.Id} has multiple exercises. Use --ovelse or --ovelse-id. Matches: " +
            string.Join(", ", ovelser.Select(ovelse => $"{ovelse.Id}:{ovelse.Name}")));
    }

    private static IReadOnlyList<EventExport> ResolveEventExports(
        InrxRepository repository,
        SiusRankCsvExportOptions options,
        IReadOnlyList<StevneInfo> stevner)
    {
        return stevner
            .SelectMany(stevne =>
            {
                var ovelser = ResolveOvelser(repository, options, stevne);
                return ovelser.Select(ovelse =>
                {
                    var rawStarters = repository.GetStarters(stevne.Id, ovelse.Id);
                    if (rawStarters.Count == 0)
                    {
                        throw new InvalidOperationException(
                            $"No starters found for Stevne.Id={stevne.Id} and OvelseDef.Id={ovelse.Id}.");
                    }

                    return new EventExport(stevne, ovelse, rawStarters);
                });
            })
            .ToList();
    }

    private sealed record EventExport(
        StevneInfo Stevne,
        OvelseInfo Ovelse,
        IReadOnlyList<InrxStarter> Starters);

    private sealed record EventFileExport(
        StevneInfo Stevne,
        OvelseInfo Ovelse,
        string? SiusEventCode,
        IReadOnlyList<StarterExportItem> Starters);

    private sealed record StarterExportItem(
        InrxStarter Starter,
        string EffectiveClass,
        int Index);
}

internal sealed record SiusRankExportPlan(
    string OutputDirectory,
    string? ShooterGroupsTemplatePath,
    IReadOnlyList<SiusRankPlannedFile> Files);

internal sealed record SiusRankPlannedFile(
    StevneInfo Stevne,
    OvelseInfo Ovelse,
    string? SiusEventCode,
    string KmNmClass,
    IReadOnlyList<SiusRankStarter> Rows,
    bool IncludeSilhouetteImportColumns,
    IReadOnlyList<string> Warnings);
