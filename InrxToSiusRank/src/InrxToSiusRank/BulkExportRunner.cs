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

        var results = new List<BulkExportFileResult>();
        foreach (var stevne in stevner)
        {
            var ovelse = ResolveOvelse(repository, options, stevne);
            var rawStarters = repository.GetStarters(stevne.Id, ovelse.Id);
            if (rawStarters.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No starters found for Stevne.Id={stevne.Id} and OvelseDef.Id={ovelse.Id}.");
            }

            var classes = repository.GetKmNmClasses(stevne.Id, ovelse.Id);
            foreach (var kmNmClass in classes)
            {
                var selectedStarters = rawStarters
                    .Where(starter => KmNmClassMatcher.Matches(starter.KmNmClass, kmNmClass.Name))
                    .ToList();
                if (selectedStarters.Count == 0)
                {
                    continue;
                }

                var rows = selectedStarters
                    .Select(starter => StarterMapper.Map(starter, siusGroupOverride: null, includeClubTeam: true))
                    .ToList();

                ExportValidator.ValidateShooterGroups(rows, shooterGroupsTemplate);

                var warnings = ExportValidator.Validate(rows).ToList();
                var outputPath = Path.Combine(outputDirectory, OutputFileName.ForImport(stevne, ovelse, kmNmClass.Name));
                SiusRankCsvWriter.Write(outputPath, rows, options.EncodingName);

                results.Add(new BulkExportFileResult(
                    stevne,
                    ovelse,
                    kmNmClass.Name,
                    rows.Count,
                    outputPath,
                    warnings));
            }
        }

        return new BulkExportResult(outputDirectory, options.ShooterGroupsTemplatePath, results);
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

    private static OvelseInfo ResolveOvelse(InrxRepository repository, AppOptions options, StevneInfo stevne)
    {
        if (options.OvelseId is not null || !string.IsNullOrWhiteSpace(options.OvelseName))
        {
            return repository.ResolveOvelse(options);
        }

        var ovelser = repository.GetOvelserForStevne(stevne.Id);
        return ovelser.Count switch
        {
            0 => throw new InvalidOperationException($"No exercises found for Stevne.Id={stevne.Id}."),
            1 => new OvelseInfo(
                ovelser[0].Id,
                ovelser[0].Name,
                ovelser[0].ShortName,
                ovelser[0].HovedOvelseId),
            _ => throw new InvalidOperationException(
                $"Stevne.Id={stevne.Id} has multiple exercises. Use --ovelse or --ovelse-id. Matches: " +
                string.Join(", ", ovelser.Select(ovelse => $"{ovelse.Id}:{ovelse.Name}")))
        };
    }
}
