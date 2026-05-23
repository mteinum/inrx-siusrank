namespace InrxToSiusRank;

public static class BulkExportRunner
{
    public static BulkExportResult Run(AppOptions options)
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

        var startNumbers = ChampionshipStartNumbers.Create(
            eventExports.SelectMany(eventExport => eventExport.Starters),
            eventExports.Select(eventExport => eventExport.Stevne),
            Path.Combine(outputDirectory, ChampionshipStartNumbers.BibMapFileName));

        var results = new List<BulkExportFileResult>();
        foreach (var eventExport in eventExports)
        {
            var classGroups = eventExport.Starters
                .GroupBy(starter => EffectiveKmNmClass.Resolve(starter, eventExport.Ovelse))
                .OrderBy(group => EffectiveKmNmClass.SortKey(group.Key))
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var classGroup in classGroups)
            {
                var selectedStarters = classGroup.ToList();
                if (selectedStarters.Count == 0)
                {
                    continue;
                }

                var rows = selectedStarters
                    .Select(starter => StarterMapper.Map(
                        starter with { KmNmClass = classGroup.Key },
                        siusGroupOverride: null,
                        includeClubTeam: true,
                        startNumber: startNumbers[starter.DeltakerId]))
                    .ToList();

                ExportValidator.ValidateShooterGroups(rows, shooterGroupsTemplate);

                var warnings = ExportValidator.Validate(rows).ToList();
                var outputPath = Path.Combine(
                    outputDirectory,
                    OutputFileName.ForImport(eventExport.Stevne, eventExport.Ovelse, classGroup.Key));
                SiusRankCsvWriter.Write(outputPath, rows, options.EncodingName);

                results.Add(new BulkExportFileResult(
                    eventExport.Stevne,
                    eventExport.Ovelse,
                    classGroup.Key,
                    rows.Count,
                    outputPath,
                    warnings));
            }
        }

        return new BulkExportResult(outputDirectory, options.ShooterGroupsTemplatePath, results);
    }

    public static BibMapCreateResult CreateBibMap(AppOptions options)
    {
        if (options.OutputDirectory is null)
        {
            throw new ArgumentException("Creating bib-map.csv requires --output-dir.");
        }

        using var repository = new InrxRepository(options.DatabasePath);
        var stevner = ResolveStevner(repository, options);
        var outputDirectory = Path.GetFullPath(options.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var eventExports = ResolveEventExports(repository, options, stevner);
        var bibMapPath = Path.Combine(outputDirectory, ChampionshipStartNumbers.BibMapFileName);
        var startNumbers = ChampionshipStartNumbers.Create(
            eventExports.SelectMany(eventExport => eventExport.Starters),
            eventExports.Select(eventExport => eventExport.Stevne),
            bibMapPath);

        return new BibMapCreateResult(
            outputDirectory,
            bibMapPath,
            eventExports.Count,
            eventExports.Sum(eventExport => eventExport.Starters.Count),
            startNumbers.Count);
    }

    private static IReadOnlyList<StevneInfo> ResolveStevner(InrxRepository repository, AppOptions options)
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

    private static IReadOnlyList<OvelseInfo> ResolveOvelser(InrxRepository repository, AppOptions options, StevneInfo stevne)
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
        AppOptions options,
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
