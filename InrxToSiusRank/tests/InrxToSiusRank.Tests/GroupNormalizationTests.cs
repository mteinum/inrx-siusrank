namespace InrxToSiusRank.Tests;

public sealed class GroupNormalizationTests
{
    [Theory]
    [InlineData("Å")]
    [InlineData("Åpen")]
    [InlineData("Apen")]
    [InlineData("APEN")]
    [InlineData("")]
    public void Open_class_aliases_normalize_to_sius_apen(string value)
    {
        Assert.Equal("Apen", GroupNormalizer.Normalize(value));
    }

    [Fact]
    public void Letter_a_is_preserved_as_distinct_class()
    {
        Assert.Equal("A", GroupNormalizer.Normalize("A"));
    }

    [Theory]
    [InlineData("M", "Menn")]
    [InlineData("K", "Kvinner")]
    [InlineData("Jm", "Jrm")]
    [InlineData("Jr.m", "Jrm")]
    [InlineData("Jk", "Jrk")]
    [InlineData("Jr.k", "Jrk")]
    [InlineData("SH1-P4", "SH1")]
    [InlineData("SH1-P3", "SH1")]
    [InlineData("SH2-R5", "SH2")]
    public void Km_nm_classes_normalize_to_shooter_group_template_names(string value, string expected)
    {
        Assert.Equal(expected, GroupNormalizer.Normalize(value));
    }
}
