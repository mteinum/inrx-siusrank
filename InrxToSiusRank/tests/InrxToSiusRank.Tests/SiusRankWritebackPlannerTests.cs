namespace InrxToSiusRank.Tests;

public sealed class SiusRankWritebackPlannerTests
{
    [Fact]
    public void Plan_treats_missing_export_result_as_unchanged_when_inrx_row_is_dns()
    {
        var export = CreateExport(
            "HurtigGrov_Apen",
            "CFPRF_Apen",
            new SiusRankExportAthlete(
                "26015",
                "26015",
                "BARSTAD",
                "Elena",
                "BARSTAD Elena",
                Result: null,
                InnerTens: null,
                []));
        var input = CreateInput(
            CreateRow(
                resultId: 7079,
                ovelseId: 6,
                deltakerId: 15,
                nsfId: "1394608",
                firstName: "Elena",
                lastName: "BARSTAD",
                existingValues: new Dictionary<string, object?> { ["statdns"] = 1 }),
            CreateOvelse(6, expectedShots: 60));
        var bibMap = new[] { new BibMapEntry("1394608", "26015", 15, "BARSTAD Elena", "test") };

        var plans = SiusRankWritebackPlanner.Plan([export], input, bibMap, out var warnings);

        Assert.Empty(warnings);
        var plan = Assert.Single(plans);
        Assert.Empty(plan.Updates);
        Assert.Empty(plan.Skipped);
        var unchanged = Assert.Single(plan.Unchanged);
        Assert.Equal(7079, unchanged.ResultatId);
    }

    [Fact]
    public void Plan_treats_incomplete_export_result_as_unchanged_when_inrx_row_is_dnf()
    {
        var export = CreateExport(
            "Standard_M",
            "STP_M",
            new SiusRankExportAthlete(
                "26019",
                "26019",
                "GRANT",
                "Nicolas Ryan",
                "GRANT Nicolas Ryan",
                Result: 300,
                InnerTens: 0,
                Enumerable.Range(1, 30)
                    .Select(position => new SiusRankExportShot(position, 10, null, null, $"t{position}"))
                    .ToList()));
        var input = CreateInput(
            CreateRow(
                resultId: 7395,
                ovelseId: 10,
                deltakerId: 19,
                nsfId: "1663867",
                firstName: "Nicolas Ryan",
                lastName: "GRANT",
                existingValues: new Dictionary<string, object?> { ["statdnf"] = 1 }),
            CreateOvelse(10, expectedShots: 60));
        var bibMap = new[] { new BibMapEntry("1663867", "26019", 19, "GRANT Nicolas Ryan", "test") };

        var plans = SiusRankWritebackPlanner.Plan([export], input, bibMap, out var warnings);

        Assert.Empty(warnings);
        var plan = Assert.Single(plans);
        Assert.Empty(plan.Updates);
        Assert.Empty(plan.Skipped);
        var unchanged = Assert.Single(plan.Unchanged);
        Assert.Equal(7395, unchanged.ResultatId);
    }

    [Fact]
    public void Plan_treats_incomplete_export_result_as_unchanged_when_sius_export_is_dnf()
    {
        var export = CreateExport(
            "Silhuett_Apen",
            "RFP_Apen",
            new SiusRankExportAthlete(
                "26019",
                "26019",
                "GRANT",
                "Nicolas Ryan",
                "GRANT Nicolas Ryan",
                Result: 300,
                InnerTens: 0,
                Enumerable.Range(1, 30)
                    .Select(position => new SiusRankExportShot(position, 10, null, null, $"t{position}"))
                    .ToList(),
                ResultStatus: "IRM DNF"));
        var input = CreateInput(
            CreateRow(
                resultId: 7395,
                ovelseId: 11,
                deltakerId: 19,
                nsfId: "1663867",
                firstName: "Nicolas Ryan",
                lastName: "GRANT",
                existingValues: new Dictionary<string, object?>()),
            CreateOvelse(11, expectedShots: 60));
        var bibMap = new[] { new BibMapEntry("1663867", "26019", 19, "GRANT Nicolas Ryan", "test") };

        var plans = SiusRankWritebackPlanner.Plan([export], input, bibMap, out var warnings);

        Assert.Empty(warnings);
        var plan = Assert.Single(plans);
        Assert.Empty(plan.Updates);
        Assert.Empty(plan.Skipped);
        var unchanged = Assert.Single(plan.Unchanged);
        Assert.Equal(7395, unchanged.ResultatId);
    }

    private static SiusRankExportCompetition CreateExport(
        string shortName,
        string eventCode,
        SiusRankExportAthlete athlete) =>
        new(
            $"/tmp/{shortName}.odf.xml",
            eventCode,
            shortName,
            shortName,
            "IndividualResults",
            "INTERIM",
            DateTime.UtcNow,
            [athlete]);

    private static InrxWritebackInput CreateInput(InrxResultRow row, InrxOvelseDefinition ovelse) =>
        new([row], new Dictionary<int, InrxOvelseDefinition> { [ovelse.Id] = ovelse });

    private static InrxResultRow CreateRow(
        int resultId,
        int ovelseId,
        int deltakerId,
        string nsfId,
        string firstName,
        string lastName,
        IReadOnlyDictionary<string, object?> existingValues) =>
        new(
            ResultatId: resultId,
            StevneId: 413,
            OvelseDefId: ovelseId,
            DeltakerId: deltakerId,
            NsfId: nsfId,
            Medlemsnummer: string.Empty,
            FirstName: firstName,
            LastName: lastName,
            ExistingTotal: 0,
            ExistingInnerTens: 0,
            ExistingShotCount: 0,
            ExistingValues: existingValues);

    private static InrxOvelseDefinition CreateOvelse(int id, int expectedShots) =>
        new(
            id,
            Name: "Test",
            ShortName: "Test",
            SkuddPerSerie: 10,
            SeriePerRang: 1,
            SeriesPerPart: [expectedShots / 10, 0, 0, 0, 0, 0, 0, 0],
            MlTarget: 100);
}
