namespace InrxToSiusRank.Tests;

public sealed class SiusRankWritebackStatusTests
{
    [Fact]
    public void Missing_export_status_is_returned_when_no_export_matches()
    {
        var status = SiusRankClassWritebackStatusResolver.FromDryRun(
            [],
            new SiusRankWritebackResult(false, null, [], []));

        Assert.Equal(SiusRankClassWritebackStatusKind.MissingExport, status.Kind);
    }

    [Fact]
    public void Ready_status_is_returned_when_plan_has_updates()
    {
        var export = CreateExport(hasShots: true);
        var result = new SiusRankWritebackResult(
            false,
            null,
            [new SiusRankWritebackEventPlan(export, 9, [CreateUpdate()], [], [], [])],
            []);

        var status = SiusRankClassWritebackStatusResolver.FromDryRun([export], result);

        Assert.Equal(SiusRankClassWritebackStatusKind.ReadyForWriteback, status.Kind);
        Assert.True(status.CanApply);
    }

    [Fact]
    public void Written_back_status_is_returned_when_no_updates_remain()
    {
        var export = CreateExport(hasShots: true);
        var unchanged = new UnchangedSiusRankWriteback(
            "Fin_M",
            "26001",
            "123",
            "Test Shooter",
            1001,
            408,
            570,
            12,
            60);
        var result = new SiusRankWritebackResult(
            false,
            null,
            [new SiusRankWritebackEventPlan(export, 9, [], [unchanged], [], [])],
            []);

        var status = SiusRankClassWritebackStatusResolver.FromDryRun([export], result);

        Assert.Equal(SiusRankClassWritebackStatusKind.WrittenBack, status.Kind);
    }

    [Fact]
    public void No_complete_results_status_is_returned_when_export_has_no_shots()
    {
        var export = CreateExport(hasShots: false);
        var result = new SiusRankWritebackResult(
            false,
            null,
            [new SiusRankWritebackEventPlan(export, 9, [], [], [], [])],
            []);

        var status = SiusRankClassWritebackStatusResolver.FromDryRun([export], result);

        Assert.Equal(SiusRankClassWritebackStatusKind.NoCompleteResults, status.Kind);
    }

    [Fact]
    public void Error_status_is_returned_when_rows_are_skipped()
    {
        var export = CreateExport(hasShots: true);
        var skipped = new SkippedSiusRankWriteback(
            "Fin_M",
            "26001",
            "123",
            "Test Shooter",
            "Could not match SIUS Rank bib/NSF-id/name to a selected inrX Resultat row.");
        var result = new SiusRankWritebackResult(
            false,
            null,
            [new SiusRankWritebackEventPlan(export, 9, [], [], [skipped], [])],
            []);

        var status = SiusRankClassWritebackStatusResolver.FromDryRun([export], result);

        Assert.Equal(SiusRankClassWritebackStatusKind.Error, status.Kind);
    }

    private static SiusRankExportCompetition CreateExport(bool hasShots) =>
        new(
            Path.Combine(Path.GetTempPath(), "Fin_M.odf.xml"),
            "SPM_M",
            "Fin_M",
            "25m Finpistol M",
            "IndividualResults",
            "Unofficial",
            DateTime.UtcNow,
            [
                new SiusRankExportAthlete(
                    "26001",
                    "123",
                    "Shooter",
                    "Test",
                    "Test Shooter",
                    hasShots ? 570 : null,
                    hasShots ? 12 : null,
                    hasShots ? [new SiusRankExportShot(1, 10, null, null, string.Empty)] : [])
            ]);

    private static PlannedSiusRankWriteback CreateUpdate() =>
        new(
            "Fin_M",
            Path.Combine(Path.GetTempPath(), "Fin_M.odf.xml"),
            1001,
            408,
            9,
            2001,
            "26001",
            "123",
            "Test Shooter",
            0,
            0,
            0,
            new InrxResultFields(
                ["101010101010"],
                ["120"],
                ["2"],
                [string.Empty],
                [120],
                [10, 10, 10],
                [1, 1, 0],
                120,
                2,
                "120",
                0));
}
