namespace InrxToSiusRank.Tests;

public sealed class SiusRankWritebackReporterTests
{
    [Fact]
    public void FormatEventSummary_includes_odf_xml_file_path()
    {
        var sourcePath = Path.Combine(
            Path.GetTempPath(),
            "Rank_A",
            "Exports",
            "I200000IA2405261300.10000000000000000000000000.SPM_M.0.001.odf.xml");
        var export = new SiusRankExportCompetition(
            sourcePath,
            "SPM_M",
            "Fin_M",
            "25m Finpistol M",
            "Individual",
            "Unofficial",
            DateTime.UtcNow,
            []);
        var eventPlan = new SiusRankWritebackEventPlan(
            export,
            9,
            [],
            [],
            [],
            []);

        var summary = SiusRankWritebackReporter.FormatEventSummary(eventPlan);

        Assert.Contains("source=I200000IA2405261300.10000000000000000000000000.SPM_M.0.001.odf.xml", summary);
        Assert.Contains($"file={Path.GetFullPath(sourcePath)}", summary);
    }
}
