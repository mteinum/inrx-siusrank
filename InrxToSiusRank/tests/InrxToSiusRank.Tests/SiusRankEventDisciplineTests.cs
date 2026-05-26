namespace InrxToSiusRank.Tests;

public sealed class SiusRankEventDisciplineTests
{
    [Theory]
    [InlineData("RFP_Apen", "RFP_Apen", 11)]
    [InlineData("RFP_Apen", "", 11)]
    [InlineData("Silhuettpistol_Apen", "RFP_Apen", 11)]
    [InlineData("2A_A", "2A_A", 18)]
    [InlineData("2A_C", "2A_C", 18)]
    [InlineData("2A_D", "2A_D", 18)]
    [InlineData("4_A", "4_A", 11)]
    [InlineData("5_A", "5_A", 10)]
    [InlineData("6F_A", "6F_A", 9)]
    [InlineData("6F_SH-Apen", "6F_SH-Apen", 9)]
    [InlineData("6G_D", "6G_D", 8)]
    [InlineData("7F_U16", "7F_U16", 7)]
    [InlineData("7G_A", "7G_A", 6)]
    [InlineData("HurtigFin_M", "SPRF_M", 7)]
    [InlineData("HurtigGrov_Apen", "CFPRF_Apen", 6)]
    [InlineData("Grov_Apen", "CFP_Apen", 8)]
    public void ResolveOvelseDefId_maps_sius_rank_event_codes(
        string shortName,
        string eventCode,
        int expectedOvelseDefId)
    {
        var resolved = SiusRankEventDiscipline.ResolveOvelseDefId(shortName, eventCode);

        Assert.Equal(expectedOvelseDefId, resolved);
    }

    [Fact]
    public void ResolveOvelseDefId_does_not_treat_arbitrary_numeric_prefix_as_approbert_code()
    {
        var resolved = SiusRankEventDiscipline.ResolveOvelseDefId("50M", "");

        Assert.Null(resolved);
    }
}
