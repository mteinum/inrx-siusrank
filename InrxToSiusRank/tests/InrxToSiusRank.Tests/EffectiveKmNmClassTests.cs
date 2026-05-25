namespace InrxToSiusRank.Tests;

public sealed class EffectiveKmNmClassTests
{
    [Theory]
    [InlineData("-", "M", "M")]
    [InlineData("", "Mann", "M")]
    [InlineData("-", "F", "K")]
    [InlineData("-", "K", "K")]
    [InlineData("", "Female", "K")]
    public void Missing_class_uses_gender(string kmNmClass, string gender, string expected)
    {
        var starter = CreateStarter(kmNmClass, gender);

        Assert.Equal(expected, EffectiveKmNmClass.Resolve(starter, CreateOvelse(10, "Standard")));
    }

    [Theory]
    [InlineData(6, "Hurtig Grov")]
    [InlineData(8, "Grovpistol")]
    [InlineData(11, "Silhuett")]
    [InlineData(18, "Fripistol")]
    public void Missing_class_uses_open_class_for_combined_exercises(int ovelseId, string ovelseName)
    {
        var maleStarter = CreateStarter("-", "M");
        var femaleStarter = CreateStarter("-", "F");
        var ovelse = CreateOvelse(ovelseId, ovelseName);

        Assert.Equal("Apen", EffectiveKmNmClass.Resolve(maleStarter, ovelse));
        Assert.Equal("Apen", EffectiveKmNmClass.Resolve(femaleStarter, ovelse));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("D")]
    public void Missing_km_nm_class_uses_inrx_class_for_approbert_pistol_events(string inrxClass)
    {
        var starter = CreateStarter("-", "M", inrxClass);

        Assert.Equal(inrxClass, EffectiveKmNmClass.Resolve(starter, CreateOvelse(18, "Fripistol")));
    }

    [Fact]
    public void Existing_class_is_preserved()
    {
        var starter = CreateStarter("V55", "F");

        Assert.Equal("V55", EffectiveKmNmClass.Resolve(starter, CreateOvelse(18, "Fripistol")));
    }

    [Theory]
    [InlineData("Å")]
    [InlineData("Åpen")]
    [InlineData("Apen")]
    public void Open_class_aliases_are_canonicalized(string kmNmClass)
    {
        var starter = CreateStarter(kmNmClass, "M");

        Assert.Equal("Apen", EffectiveKmNmClass.Resolve(starter, CreateOvelse(18, "Fripistol")));
    }

    [Fact]
    public void Unknown_gender_keeps_missing_marker()
    {
        var starter = CreateStarter("-", "");

        Assert.Equal("-", EffectiveKmNmClass.Resolve(starter, CreateOvelse(10, "Standard")));
    }

    private static OvelseInfo CreateOvelse(int id, string name) =>
        new(id, name, string.Empty, 0);

    private static InrxStarter CreateStarter(string kmNmClass, string gender, string inrxClass = "-") =>
        new(
            ResultatId: 1,
            DeltakerId: 100,
            Standplass: 5,
            SkivenrFra: string.Empty,
            SkivenrTil: string.Empty,
            Relay: 1,
            RelayDate: "2026-05-23 10:00:00",
            NsfId: "900001",
            AccreditationNumber: string.Empty,
            FirstName: "Test",
            LastName: "Shooter",
            BirthDay: "1980-01-01",
            Gender: gender,
            Land: "NOR",
            ClubName: "Club",
            ClubShortName: "CLB",
            InrxClass: inrxClass,
            KmNmClass: kmNmClass,
            DmClass: "-",
            OvelseName: "Standard",
            StevneName: "Test");
}
