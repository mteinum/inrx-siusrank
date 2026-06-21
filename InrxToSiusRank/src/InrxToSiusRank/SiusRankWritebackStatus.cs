namespace InrxToSiusRank;

public enum SiusRankClassWritebackStatusKind
{
    MissingExport,
    ReadyForWriteback,
    WrittenBack,
    Error,
    NoCompleteResults
}

public sealed record SiusRankClassWritebackStatus(
    SiusRankClassWritebackStatusKind Kind,
    string Text,
    SiusRankWritebackResult? Result,
    IReadOnlyList<string> Messages)
{
    public bool CanApply => Kind == SiusRankClassWritebackStatusKind.ReadyForWriteback;
}

public static class SiusRankClassWritebackStatusResolver
{
    private const string IncompleteResultReason = "No complete result with shots in SIUS Rank export.";

    public static SiusRankClassWritebackStatus Validate(SiusRankWritebackOptions options)
    {
        if (!Directory.Exists(options.ExportsDirectory))
        {
            return new SiusRankClassWritebackStatus(
                SiusRankClassWritebackStatusKind.MissingExport,
                "Mangler export",
                null,
                [$"Exports directory does not exist: {options.ExportsDirectory}"]);
        }

        var exports = SiusRankOdfExportReader.ReadLatestIndividualResults(
            options.ExportsDirectory,
            options.EventFilters);
        if (exports.Count == 0)
        {
            return new SiusRankClassWritebackStatus(
                SiusRankClassWritebackStatusKind.MissingExport,
                "Mangler export",
                null,
                ["No SIUS Rank IndividualResults ODF export matched this exercise/class."]);
        }

        using var repository = new SiusRankWritebackRepository(options.DatabasePath);
        var input = repository.GetInput(options.StevneIds);
        var bibMap = BibMapReader.Read(options.BibMapPath);
        var plans = SiusRankWritebackPlanner.Plan(exports, input, bibMap, out var warnings);
        var result = new SiusRankWritebackResult(
            Applied: false,
            BackupPath: null,
            Events: plans,
            Warnings: warnings);

        return FromDryRun(exports, result);
    }

    public static SiusRankClassWritebackStatus FromDryRun(
        IReadOnlyList<SiusRankExportCompetition> exports,
        SiusRankWritebackResult result)
    {
        if (exports.Count == 0)
        {
            return new SiusRankClassWritebackStatus(
                SiusRankClassWritebackStatusKind.MissingExport,
                "Mangler export",
                result,
                ["No SIUS Rank IndividualResults ODF export matched this exercise/class."]);
        }

        var messages = BuildMessages(result);
        if (exports.All(export => export.ShotResultCount == 0))
        {
            return new SiusRankClassWritebackStatus(
                SiusRankClassWritebackStatusKind.NoCompleteResults,
                "Ingen komplette resultater",
                result,
                messages.Count == 0 ? ["Export exists, but no athletes have complete shot results."] : messages);
        }

        var hasBlockingMessages = HasBlockingMessages(result);
        if (result.UpdateCount > 0 && !hasBlockingMessages)
        {
            return new SiusRankClassWritebackStatus(
                SiusRankClassWritebackStatusKind.ReadyForWriteback,
                "Klar for writeback",
                result,
                [$"{result.UpdateCount} result row(s) can be written to inrX.", ..messages]);
        }

        if (hasBlockingMessages || result.SkippedCount > 0)
        {
            return new SiusRankClassWritebackStatus(
                SiusRankClassWritebackStatusKind.Error,
                "Feil",
                result,
                messages.Count == 0 ? ["One or more SIUS Rank rows were skipped."] : messages);
        }

        if (result.UpdateCount > 0)
        {
            return new SiusRankClassWritebackStatus(
                SiusRankClassWritebackStatusKind.ReadyForWriteback,
                "Klar for writeback",
                result,
                [$"{result.UpdateCount} result row(s) can be written to inrX."]);
        }

        if (result.UnchangedCount > 0)
        {
            return new SiusRankClassWritebackStatus(
                SiusRankClassWritebackStatusKind.WrittenBack,
                "Allerede oppdatert",
                result,
                [$"{result.UnchangedCount} resultat(er) er allerede oppdatert i inrX."]);
        }

        return new SiusRankClassWritebackStatus(
            SiusRankClassWritebackStatusKind.NoCompleteResults,
            "Ingen komplette resultater",
            result,
            ["Export exists, but no writable or up-to-date complete results were found."]);
    }

    private static bool HasBlockingMessages(SiusRankWritebackResult result)
    {
        if (result.Warnings.Count > 0)
        {
            return true;
        }

        foreach (var eventPlan in result.Events)
        {
            if (eventPlan.Warnings.Count > 0)
            {
                return true;
            }

            if (eventPlan.Skipped.Any(skipped => !IsIncompleteResultSkip(skipped)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsIncompleteResultSkip(SkippedSiusRankWriteback skipped) =>
        skipped.Reason.Equals(IncompleteResultReason, StringComparison.Ordinal);

    private static IReadOnlyList<string> BuildMessages(SiusRankWritebackResult result)
    {
        var messages = new List<string>();
        messages.AddRange(result.Warnings);
        foreach (var eventPlan in result.Events)
        {
            messages.AddRange(eventPlan.Warnings);
            messages.AddRange(eventPlan.Skipped.Select(skipped =>
                $"{eventPlan.Export.ShortName}: {skipped.DisplayName} bib={skipped.BibNumber}: {skipped.Reason}"));
        }

        return messages;
    }
}
