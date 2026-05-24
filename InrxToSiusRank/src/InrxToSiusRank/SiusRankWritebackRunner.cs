namespace InrxToSiusRank;

public static class SiusRankWritebackRunner
{
    public static SiusRankWritebackResult Run(SiusRankWritebackOptions options)
    {
        var exports = SiusRankOdfExportReader.ReadLatestIndividualResults(
            options.ExportsDirectory,
            options.EventFilters);
        var warnings = new List<string>();
        if (exports.Count == 0)
        {
            warnings.Add("No SIUS Rank IndividualResults ODF XML exports were found.");
        }

        using var repository = new SiusRankWritebackRepository(options.DatabasePath);
        var input = repository.GetInput(options.StevneIds);
        var bibMap = BibMapReader.Read(options.BibMapPath);
        var eventPlans = SiusRankWritebackPlanner.Plan(exports, input, bibMap, out var planWarnings);
        warnings.AddRange(planWarnings);

        string? backupPath = null;
        if (options.Apply && eventPlans.Sum(plan => plan.Updates.Count) > 0)
        {
            backupPath = SiusRankWritebackRepository.CreateBackup(options.DatabasePath);
            SiusRankWritebackRepository.Apply(
                options.DatabasePath,
                eventPlans.SelectMany(plan => plan.Updates).ToList());
        }

        return new SiusRankWritebackResult(options.Apply, backupPath, eventPlans, warnings);
    }
}

public static class SiusRankWritebackReporter
{
    public static void Print(SiusRankWritebackResult result)
    {
        Console.WriteLine(result.Applied
            ? "SIUS Rank results written back to inrX."
            : "Dry run only. No database changes written.");
        if (!string.IsNullOrWhiteSpace(result.BackupPath))
        {
            Console.WriteLine($"Backup: {Path.GetFullPath(result.BackupPath)}");
        }

        Console.WriteLine($"Planned updates: {result.UpdateCount}");
        Console.WriteLine($"Unchanged rows: {result.UnchangedCount}");
        Console.WriteLine($"Skipped rows: {result.SkippedCount}");

        foreach (var warning in result.Warnings)
        {
            Console.WriteLine($"WARNING: {warning}");
        }

        foreach (var eventPlan in result.Events)
        {
            PrintEvent(eventPlan);
        }
    }

    private static void PrintEvent(SiusRankWritebackEventPlan eventPlan)
    {
        Console.WriteLine();
        Console.WriteLine(FormatEventSummary(eventPlan));

        foreach (var update in eventPlan.Updates)
        {
            Console.WriteLine(
                $"  UPDATE Resultat.Id={update.ResultatId}, Stevne.Id={update.StevneId}: " +
                $"{update.DisplayName} bib={update.BibNumber}, " +
                $"{update.ExistingTotal}-{update.ExistingInnerTens}x/{update.ExistingShotCount} shots -> " +
                $"{update.NewTotal}-{update.NewInnerTens}x/{update.NewShotCount} shots");
        }

        foreach (var skipped in eventPlan.Skipped)
        {
            Console.WriteLine(
                $"  SKIP {skipped.DisplayName} bib={skipped.BibNumber} nsf={skipped.AccreditationNumber}: " +
                skipped.Reason);
        }

        foreach (var warning in eventPlan.Warnings)
        {
            Console.WriteLine($"  WARNING: {warning}");
        }
    }

    public static string FormatEventSummary(SiusRankWritebackEventPlan eventPlan) =>
        $"{eventPlan.Export.ShortName}: OvelseDef.Id={eventPlan.OvelseDefId?.ToString() ?? "?"}, " +
        $"source={Path.GetFileName(eventPlan.Export.SourcePath)}, " +
        $"file={Path.GetFullPath(eventPlan.Export.SourcePath)}, " +
        $"results={eventPlan.Export.ResultCount}, with shots={eventPlan.Export.ShotResultCount}, " +
        $"updates={eventPlan.Updates.Count}, unchanged={eventPlan.Unchanged.Count}, skipped={eventPlan.Skipped.Count}";
}
