namespace InrxToSiusRank.Tests;

public sealed class KmNmClassMatcherTests
{
    [Theory]
    [InlineData("Å")]
    [InlineData("Åpen")]
    [InlineData("Apen")]
    [InlineData("APEN")]
    public void Open_aliases_match_inrx_km_nm_class_aa(string requested)
    {
        Assert.True(KmNmClassMatcher.Matches("Å", requested));
    }

    [Theory]
    [InlineData("V55")]
    [InlineData("v55")]
    public void Veteran_class_matches_case_insensitively(string requested)
    {
        Assert.True(KmNmClassMatcher.Matches("V55", requested));
    }

    [Fact]
    public void Different_class_does_not_match()
    {
        Assert.False(KmNmClassMatcher.Matches("V55", "Å"));
    }
}
