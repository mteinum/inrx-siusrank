namespace InrxToSiusRank.Tests;

public sealed class SiusRankResultConverterTests
{
    [Fact]
    public void Convert_builds_inrx_series_and_ranking_fields_from_exported_shots()
    {
        var athlete = new SiusRankExportAthlete(
            "26008",
            "1273763",
            "VRÅLSTAD",
            "Tore",
            "VRÅLSTAD Tore",
            Result: 29,
            InnerTens: 1,
            [
                new SiusRankExportShot(1, 10, 0.1m, 0.1m, "t1"),
                new SiusRankExportShot(2, 10, 5m, 5m, "t2"),
                new SiusRankExportShot(3, 9, 1m, 2m, "t3"),
                new SiusRankExportShot(4, 0, null, null, "t4")
            ]);
        var ovelse = new InrxOvelseDefinition(
            7,
            "Hurtig Fin",
            "HFin",
            SkuddPerSerie: 2,
            SeriePerRang: 1,
            [2, 0, 0, 0, 0, 0, 0, 0],
            MlTarget: 100);

        var fields = SiusRankResultConverter.Convert(athlete, ovelse);

        Assert.Equal(29, fields.TotalScore);
        Assert.Equal(1, fields.InnerTens);
        Assert.Equal("OX;9-;", fields.SeriesPerPart[0]);
        Assert.Equal("20;9;", fields.PartSumsText[0]);
        Assert.Equal("1;0;", fields.InnerTensPerPart[0]);
        Assert.Equal(29, fields.SumPerPart[0]);
        Assert.Equal([20, 9], fields.SumRank.Take(2).ToArray());
        Assert.Equal([1, 0], fields.InnerRank.Take(2).ToArray());
        Assert.Equal("TCBA", fields.PerShotRanking);
        Assert.Contains("0.1#0.1%", fields.XyPerPart[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Convert_marks_closest_ten_shots_as_inner_tens_when_export_only_has_total_inner_count()
    {
        var athlete = new SiusRankExportAthlete(
            "26008",
            "1273763",
            "VRÅLSTAD",
            "Tore",
            "VRÅLSTAD Tore",
            Result: 20,
            InnerTens: 1,
            [
                new SiusRankExportShot(1, 10, 9m, 9m, "t1"),
                new SiusRankExportShot(2, 10, 1m, 1m, "t2")
            ]);
        var ovelse = new InrxOvelseDefinition(
            7,
            "Hurtig Fin",
            "HFin",
            SkuddPerSerie: 2,
            SeriePerRang: 1,
            [1, 0, 0, 0, 0, 0, 0, 0],
            MlTarget: 100);

        var fields = SiusRankResultConverter.Convert(athlete, ovelse);

        Assert.Equal("XO;", fields.SeriesPerPart[0]);
        Assert.Equal("AB", fields.PerShotRanking);
    }
}
