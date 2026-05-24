namespace InrxToSiusRank.Tests;

public sealed class SiusRankEventDisciplineTests
{
    [Theory]
    [InlineData("RFP_Apen", "RFP_Apen", 11)]
    [InlineData("RFP_Apen", "", 11)]
    [InlineData("Silhuettpistol_Apen", "RFP_Apen", 11)]
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
}
