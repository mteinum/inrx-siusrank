namespace InrxToSiusRank;

public static class SiusRankCsvExportRunner
{
    public static SiusRankCsvExportResult Run(SiusRankCsvExportOptions options)
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

        var results = new List<SiusRankCsvExportFileResult>();
        foreach (var eventExport in eventExports)
        {
            var starters = eventExport.Starters
                .Select((starter, index) => new
                {
                    Starter = starter,
                    EffectiveClass = EffectiveKmNmClass.Resolve(starter, eventExport.Ovelse),
                    Index = index
                })
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

            var warnings = ExportValidator.Validate(rows).ToList();
            var outputPath = Path.Combine(
                outputDirectory,
                OutputFileName.ForCompetitionImport(eventExport.Stevne, eventExport.Ovelse));
            SiusRankCsvWriter.Write(
                outputPath,
                rows,
                options.EncodingName,
                IncludeSilhouetteImportColumns(eventExport.Ovelse, options.SilhouetteShootersPerStand));

            results.Add(new SiusRankCsvExportFileResult(
                eventExport.Stevne,
                eventExport.Ovelse,
                FormatGroupSummary(rows),
                rows.Count,
                outputPath,
                warnings));
        }

        return new SiusRankCsvExportResult(outputDirectory, options.ShooterGroupsTemplatePath, results);
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
}
