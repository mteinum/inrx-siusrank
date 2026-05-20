namespace InrxToSiusRank;

public static class ExportRunner
{
    public static ExportResult Run(AppOptions options)
    {
        using var repository = new InrxRepository(options.DatabasePath);
        var stevne = repository.ResolveStevne(options);
        var ovelse = repository.ResolveOvelse(options);
        var rawStarters = repository.GetStarters(stevne.Id, ovelse.Id);

        if (rawStarters.Count == 0)
        {
            throw new InvalidOperationException(
                $"No starters found for Stevne.Id={stevne.Id} and OvelseDef.Id={ovelse.Id}.");
        }

        var selectedStarters = rawStarters
            .Where(starter => KmNmClassMatcher.Matches(starter.KmNmClass, options.KmNmClass))
            .ToList();

        if (selectedStarters.Count == 0)
        {
            var availableClasses = rawStarters
                .GroupBy(starter => string.IsNullOrWhiteSpace(starter.KmNmClass) ? "(empty)" : starter.KmNmClass)
                .Select(group => $"{group.Key}={group.Count()}")
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);

            throw new InvalidOperationException(
                $"No starters found for KM/NM class '{options.KmNmClass}'. Available KM/NM classes: " +
                string.Join(", ", availableClasses));
        }

        var rows = selectedStarters
            .Select(starter => StarterMapper.Map(starter, options.SiusGroupOverride, options.IncludeClubTeam))
            .ToList();

        var shooterGroupsTemplate = options.ShooterGroupsTemplatePath is null
            ? null
            : ShooterGroupsTemplate.Load(options.ShooterGroupsTemplatePath);
        ExportValidator.ValidateShooterGroups(rows, shooterGroupsTemplate);

        var warnings = ExportValidator.Validate(rows).ToList();
        var csv = SiusRankCsvWriter.ToCsv(rows);
        if (options.OutputPath is not null)
        {
            SiusRankCsvWriter.Write(options.OutputPath, csv, options.EncodingName);
        }

        if (options.CopyToClipboard)
        {
            ClipboardService.SetText(csv);
        }

        return new ExportResult(
            stevne,
            ovelse,
            options.KmNmClass ?? "All",
            options.SiusGroupOverride,
            options.ShooterGroupsTemplatePath,
            rows.Count,
            options.OutputPath,
            options.CopyToClipboard,
            warnings);
    }
}
