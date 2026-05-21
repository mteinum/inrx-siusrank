namespace InrxToSiusRank.Tests;

public sealed class ChampionshipBibNumbersTests
{
    [Fact]
    public void Create_assigns_one_sequential_bib_per_deltaker_id()
    {
        var starters = new[]
        {
            Starter(resultatId: 1001, deltakerId: 20),
            Starter(resultatId: 1002, deltakerId: 10),
            Starter(resultatId: 1003, deltakerId: 20),
            Starter(resultatId: 1004, deltakerId: 30)
        };

        var bibNumbers = ChampionshipBibNumbers.Create(starters);

        Assert.Equal("1", bibNumbers[10]);
        Assert.Equal("2", bibNumbers[20]);
        Assert.Equal("3", bibNumbers[30]);
        Assert.Equal(3, bibNumbers.Count);
    }

    [Fact]
    public void Shared_bib_number_changes_only_bib_number_for_duplicate_shooter_starts()
    {
        var firstStart = Starter(resultatId: 1001, deltakerId: 20);
        var secondStart = Starter(resultatId: 1003, deltakerId: 20);
        var bibNumbers = ChampionshipBibNumbers.Create([firstStart, secondStart]);

        var firstRow = StarterMapper.Map(
            firstStart,
            siusGroupOverride: null,
            includeClubTeam: false,
            bibNumberOverride: bibNumbers[firstStart.DeltakerId]);
        var secondRow = StarterMapper.Map(
            secondStart,
            siusGroupOverride: null,
            includeClubTeam: false,
            bibNumberOverride: bibNumbers[secondStart.DeltakerId]);

        Assert.Equal("1", firstRow.BibNumber);
        Assert.Equal(firstRow.BibNumber, secondRow.BibNumber);
        Assert.Equal("1001", firstRow.StartNumber);
        Assert.Equal("1003", secondRow.StartNumber);
        Assert.Equal("1001", firstRow.StarterId);
        Assert.Equal("1003", secondRow.StarterId);
    }

    private static InrxStarter Starter(int resultatId, int deltakerId) =>
        new(
            ResultatId: resultatId,
            DeltakerId: deltakerId,
            Standplass: 5,
            SkivenrFra: string.Empty,
            SkivenrTil: string.Empty,
            Relay: 2,
            RelayDate: "2026-07-06 09:00:00",
            AccreditationNumber: string.Empty,
            FirstName: "Morten",
            LastName: "Teinum",
            BirthDay: "1973-06-23",
            Gender: "M",
            Land: "Norge",
            ClubName: "Kristiansand Pistolskyttere",
            ClubShortName: "KPS",
            InrxClass: "-",
            KmNmClass: "Å",
            DmClass: "-",
            OvelseName: "Fripistol",
            StevneName: "20260706 NM Fripistol 2026");
}
