namespace InrxToSiusRank.Tests;

public sealed class OutputFileNameTests
{
    [Theory]
    [InlineData(18, "Fripistol", "Fri", 2, "Å", "20260711_FP_Apen.csv")]
    [InlineData(10, "Standard", "Std", 9, "M", "20260711_STP_M.csv")]
    [InlineData(8, "Grovpistol", "Grov", 10, "Å", "20260711_CFP_Apen.csv")]
    public void Uses_sius_event_code_for_nm_pistol_events(
        int id,
        string name,
        string shortName,
        int hovedOvelseId,
        string kmNmClass,
        string expected)
    {
        var fileName = OutputFileName.ForImport(
            CreateStevne(),
            new OvelseInfo(id, name, shortName, hovedOvelseId),
            kmNmClass);

        Assert.Equal(expected, fileName);
    }

    [Theory]
    [InlineData("K", "20260711_SPW_K.csv")]
    [InlineData("Kvinner", "20260711_SPW_Kvinner.csv")]
    [InlineData("Jk", "20260711_SPW_Jk.csv")]
    [InlineData("SH1-P3", "20260711_SPSH1_SH1-P3.csv")]
    [InlineData("M", "20260711_SPM_M.csv")]
    public void Uses_class_specific_sport_pistol_event_code(string kmNmClass, string expected)
    {
        var fileName = OutputFileName.ForImport(
            CreateStevne(),
            new OvelseInfo(9, "Finpistol", "Fin", 10),
            kmNmClass);

        Assert.Equal(expected, fileName);
    }

    [Theory]
    [InlineData("Å", "20260711_RFP_Apen.csv")]
    [InlineData("Apen", "20260711_RFP_Apen.csv")]
    [InlineData("V55", "20260711_RFP_NF_V55.csv")]
    public void Uses_final_silhouette_event_code_for_open_class_only(string kmNmClass, string expected)
    {
        var fileName = OutputFileName.ForImport(
            CreateStevne(),
            new OvelseInfo(11, "Silhuett", "Sil", 8),
            kmNmClass);

        Assert.Equal(expected, fileName);
    }

    [Fact]
    public void Uses_sius_event_code_for_hurtig_fin()
    {
        var fileName = OutputFileName.ForImport(
            CreateStevne(),
            new OvelseInfo(7, "Hurtig Fin", "Fin", 11),
            "M");

        Assert.Equal("20260711_SPRF_M.csv", fileName);
    }

    [Fact]
    public void Uses_sius_event_code_for_hurtig_grov()
    {
        var fileName = OutputFileName.ForImport(
            CreateStevne(),
            new OvelseInfo(6, "Hurtig Grov", "Grov", 11),
            "Å");

        Assert.Equal("20260711_CFPRF_Apen.csv", fileName);
    }

    private static StevneInfo CreateStevne() =>
        new(410, "20260711 NM Hurtigpistol fin 2026", "2026-07-11 09:00:00", 377);
}
