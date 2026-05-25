namespace InrxToSiusRank.Tests;

public sealed class OutputFileNameTests
{
    [Theory]
    [InlineData(18, "Fripistol", "Fri", 2, "Å", "20260711_Fri_Apen.csv")]
    [InlineData(18, "Fripistol", "Fri", 2, "SH1-P4", "20260711_Fri_SH1-P4.csv")]
    [InlineData(10, "Standard", "Std", 9, "M", "20260711_Standard_M.csv")]
    [InlineData(8, "Grovpistol", "Grov", 10, "Å", "20260711_Grov_Apen.csv")]
    public void Uses_norwegian_event_code_for_nm_pistol_events(
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
    [InlineData(18, "Fripistol", "A", "20260711_2A_A.csv")]
    [InlineData(18, "Fripistol", "D", "20260711_2A_D.csv")]
    [InlineData(18, "50m pistol, skyting med støtte, vet.", "V55", "20260711_2C_V55.csv")]
    [InlineData(9, "Finpistol", "SH Å", "20260711_6F_SH-Apen.csv")]
    [InlineData(7, "Hurtig Fin", "U16", "20260711_7F_U16.csv")]
    public void Uses_approbert_event_code_when_class_matches_approbert_template(
        int id,
        string name,
        string kmNmClass,
        string expected)
    {
        var fileName = OutputFileName.ForImport(
            CreateStevne(),
            new OvelseInfo(id, name, string.Empty, 0),
            kmNmClass);

        Assert.Equal(expected, fileName);
    }

    [Fact]
    public void Keeps_nm_fripistol_code_when_class_is_not_an_approbert_2a_class()
    {
        var fileName = OutputFileName.ForImport(
            CreateStevne(),
            new OvelseInfo(18, "Fripistol", "Fri", 2),
            "V55");

        Assert.Equal("20260711_Fri_V55.csv", fileName);
    }

    [Theory]
    [InlineData("K", "20260711_Fin_K.csv")]
    [InlineData("Kvinner", "20260711_Fin_K.csv")]
    [InlineData("Jk", "20260711_Fin_Jk.csv")]
    [InlineData("Jrk", "20260711_Fin_Jk.csv")]
    [InlineData("SH1-P3", "20260711_Fin_SH1-P3.csv")]
    [InlineData("M", "20260711_Fin_M.csv")]
    public void Uses_class_specific_norwegian_finpistol_event_code(string kmNmClass, string expected)
    {
        var fileName = OutputFileName.ForImport(
            CreateStevne(),
            new OvelseInfo(9, "Finpistol", "Fin", 10),
            kmNmClass);

        Assert.Equal(expected, fileName);
    }

    [Theory]
    [InlineData("Å", "20260711_Silhuett_Apen.csv")]
    [InlineData("Apen", "20260711_Silhuett_Apen.csv")]
    [InlineData("Jr-NM", "20260711_Silhuett_Jr-NM.csv")]
    [InlineData("V55", "20260711_Silhuett_V55.csv")]
    public void Uses_final_silhouette_event_code_for_open_and_junior_classes(string kmNmClass, string expected)
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

        Assert.Equal("20260711_HurtigFin_M.csv", fileName);
    }

    [Fact]
    public void Uses_sius_event_code_for_hurtig_grov()
    {
        var fileName = OutputFileName.ForImport(
            CreateStevne(),
            new OvelseInfo(6, "Hurtig Grov", "Grov", 11),
            "Å");

        Assert.Equal("20260711_HurtigGrov_Apen.csv", fileName);
    }

    private static StevneInfo CreateStevne() =>
        new(410, "20260711 NM Hurtigpistol fin 2026", "2026-07-11 09:00:00", 377);
}
