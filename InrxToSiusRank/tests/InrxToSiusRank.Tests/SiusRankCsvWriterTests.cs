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
        var row = StarterMapper.Map(starter, siusGroupOverride: null, includeClubTeam: false, startNumber: "26001");

        var csv = SiusRankCsvWriter.ToCsv([row]);

        Assert.StartsWith(SiusRankCsvWriter.HeaderLine + "\r\n", csv);
        Assert.Contains("BJØRNSEN Pål", csv);
        Assert.Contains(";Apen;", csv);
        Assert.EndsWith("\r\n", csv);
    }

    [Fact]
    public void Csv_can_include_silhouette_import_columns_for_two_shooters_per_stand()
    {
        var starter = CreateStarter(firstName: "Rune", lastName: "Wold", standplass: 12);
        var row = StarterMapper.Map(starter, siusGroupOverride: null, includeClubTeam: false, startNumber: "26001");

        var csv = SiusRankCsvWriter.ToCsv([row], includeSilhouetteImportColumns: true);

        Assert.StartsWith(SiusRankCsvWriter.SilhouetteImportHeaderLine + "\r\n", csv);
        Assert.Contains(";12;", csv);
        Assert.Contains(";V;3000\r\n", csv);
    }

    [Theory]
    [InlineData(2, "V", 1000)]
    [InlineData(4, "H", 1000)]
    [InlineData(12, "V", 3000)]
    [InlineData(14, "H", 3000)]
    public void Silhouette_import_mapping_uses_side_targets(int target, string filter, int startNumber)
    {
        var mapping = SilhouetteImportMapping.ForTarget(target);

        Assert.NotNull(mapping);
        Assert.Equal(filter, mapping.ImportShotFilter);
        Assert.Equal(startNumber, mapping.SiusDataStartNumber);
    }

    [Fact]
    public void Mapper_sets_required_sius_rank_fields_from_inrx_result()
    {
        var starter = CreateStarter(resultatId: 7270, nsfId: "905380", standplass: 17, relay: 1);

        var row = StarterMapper.Map(starter, siusGroupOverride: null, includeClubTeam: false, startNumber: "26001");

        Assert.Equal("26001", row.StartNumber);
        Assert.Equal("26001", row.AccreditationNumber);
        Assert.Equal("26001", row.BibNumber);
        Assert.Equal("26001", row.StarterId);
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

        var row = StarterMapper.Map(starter, siusGroupOverride: null, includeClubTeam: false, startNumber: "26001");

        Assert.Equal("SIUS-7270", row.AccreditationNumber);
    }

    [Fact]
    public void Mapper_requires_assigned_start_number()
    {
        var starter = CreateStarter(resultatId: 7270);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            StarterMapper.Map(starter, siusGroupOverride: null, includeClubTeam: false, startNumber: ""));
        Assert.Contains("has no assigned start number", ex.Message);
    }

    [Fact]
    public void Mapper_uses_explicit_sius_group_without_normalizing()
    {
        var starter = CreateStarter(resultatId: 7270);

        var row = StarterMapper.Map(starter, siusGroupOverride: "Å", includeClubTeam: false, startNumber: "26001");

        Assert.Equal("Å", row.Groups);
    }

    [Fact]
    public void Mapper_can_fill_team_from_club_short_name()
    {
        var starter = CreateStarter(clubShortName: "KPS", clubName: "Kristiansand Pistolskyttere");

        var row = StarterMapper.Map(starter, siusGroupOverride: null, includeClubTeam: true, startNumber: "26001");

        Assert.Equal("KPS", row.Team);
        Assert.Equal("KPS", row.TeamDisplay);
    }

    private static InrxStarter CreateStarter(
        int resultatId = 1,
        int standplass = 5,
        int? relay = 2,
        string firstName = "Morten",
        string lastName = "Teinum",
        string nsfId = "905380",
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
            NsfId: nsfId,
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
