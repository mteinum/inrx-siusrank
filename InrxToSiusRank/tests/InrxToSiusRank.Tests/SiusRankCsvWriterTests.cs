namespace InrxToSiusRank.Tests;

public sealed class SiusRankCsvWriterTests
{
    [Fact]
    public void Header_matches_sius_rank_example()
    {
        const string expected =
            "StartNumber;AccreditationNumber;IssfId;DisplayNameLong;DisplayName;FirstName;Name;BirthDay;Gender;Nation;BibNumber;TargetNumber;Relay;TeamIndex;DuellIndex;Groups;Comment;StarterId;TeamPosition;Team;TeamDisplay;TeamDuellIndex;TeamComment";

        Assert.Equal(expected, SiusRankCsvWriter.HeaderLine);
    }

    [Fact]
    public void Csv_uses_semicolon_crlf_and_preserves_norwegian_characters()
    {
        var starter = CreateStarter(firstName: "Pål", lastName: "Bjørnsen");
        var row = StarterMapper.Map(starter, siusGroupOverride: null, includeClubTeam: false);

        var csv = SiusRankCsvWriter.ToCsv([row]);

        Assert.StartsWith(SiusRankCsvWriter.HeaderLine + "\r\n", csv);
        Assert.Contains("BJØRNSEN Pål", csv);
        Assert.Contains(";Apen;", csv);
        Assert.EndsWith("\r\n", csv);
    }

    [Fact]
    public void Mapper_sets_required_sius_rank_fields_from_inrx_result()
    {
        var starter = CreateStarter(resultatId: 7270, standplass: 17, relay: 1);

        var row = StarterMapper.Map(starter, siusGroupOverride: null, includeClubTeam: false);

        Assert.Equal("7270", row.StartNumber);
        Assert.Equal("7270", row.AccreditationNumber);
        Assert.Equal("7270", row.BibNumber);
        Assert.Equal("7270", row.StarterId);
        Assert.Equal("17", row.TargetNumber);
        Assert.Equal("1", row.Relay);
        Assert.Equal("Apen", row.Groups);
        Assert.Equal("NOR", row.Nation);
        Assert.Equal("M", row.Gender);
        Assert.Equal("23.06.1973", row.BirthDay);
        Assert.Equal(string.Empty, row.Team);
    }

    [Fact]
    public void Mapper_uses_existing_accreditation_number_when_present()
    {
        var starter = CreateStarter(resultatId: 7270, accreditationNumber: "SIUS-7270");

        var row = StarterMapper.Map(starter, siusGroupOverride: null, includeClubTeam: false);

        Assert.Equal("SIUS-7270", row.AccreditationNumber);
    }

    [Fact]
    public void Mapper_uses_bib_number_override_without_changing_start_identity()
    {
        var starter = CreateStarter(resultatId: 7270, accreditationNumber: "SIUS-7270");

        var row = StarterMapper.Map(
            starter,
            siusGroupOverride: null,
            includeClubTeam: false,
            bibNumberOverride: "42");

        Assert.Equal("7270", row.StartNumber);
        Assert.Equal("7270", row.StarterId);
        Assert.Equal("SIUS-7270", row.AccreditationNumber);
        Assert.Equal("42", row.BibNumber);
    }

    [Fact]
    public void Mapper_uses_explicit_sius_group_without_normalizing()
    {
        var starter = CreateStarter(resultatId: 7270);

        var row = StarterMapper.Map(starter, siusGroupOverride: "Å", includeClubTeam: false);

        Assert.Equal("Å", row.Groups);
    }

    [Fact]
    public void Mapper_can_fill_team_from_club_short_name()
    {
        var starter = CreateStarter(clubShortName: "KPS", clubName: "Kristiansand Pistolskyttere");

        var row = StarterMapper.Map(starter, siusGroupOverride: null, includeClubTeam: true);

        Assert.Equal("KPS", row.Team);
        Assert.Equal("KPS", row.TeamDisplay);
    }

    private static InrxStarter CreateStarter(
        int resultatId = 1,
        int standplass = 5,
        int? relay = 2,
        string firstName = "Morten",
        string lastName = "Teinum",
        string accreditationNumber = "",
        string clubShortName = "KPS",
        string clubName = "Kristiansand Pistolskyttere") =>
        new(
            ResultatId: resultatId,
            DeltakerId: 100,
            Standplass: standplass,
            SkivenrFra: string.Empty,
            SkivenrTil: string.Empty,
            Relay: relay,
            RelayDate: "2026-07-06 09:00:00",
            AccreditationNumber: accreditationNumber,
            FirstName: firstName,
            LastName: lastName,
            BirthDay: "1973-06-23",
            Gender: "M",
            Land: "Norge",
            ClubName: clubName,
            ClubShortName: clubShortName,
            InrxClass: "-",
            KmNmClass: "Å",
            DmClass: "-",
            OvelseName: "Fripistol",
            StevneName: "20260706 NM Fripistol 2026");
}
