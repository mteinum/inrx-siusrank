namespace InrxToSiusRank.Tests;

public sealed class ChampionshipStartNumbersTests
{
    [Fact]
    public void Create_assigns_year_prefixed_sequence_per_shooter_across_events()
    {
        var firstEventStarter = CreateStarter(resultatId: 1, deltakerId: 20);
        var secondEventSameShooter = CreateStarter(resultatId: 2, deltakerId: 20);
        var lowerDeltakerId = CreateStarter(resultatId: 3, deltakerId: 10);
        var higherDeltakerId = CreateStarter(resultatId: 4, deltakerId: 30);

        var numbers = ChampionshipStartNumbers.Create(
            [firstEventStarter, secondEventSameShooter, lowerDeltakerId, higherDeltakerId],
            [
                new StevneInfo(413, "Skjaergards-smellen", "2026-05-23 10:00:00", 1),
                new StevneInfo(414, "Skjaergards-smellen", "2026-05-24 10:00:00", 1)
            ]);

        Assert.Equal("26001", numbers[10]);
        Assert.Equal("26002", numbers[20]);
        Assert.Equal("26003", numbers[30]);
    }

    [Fact]
    public void Create_rejects_numbers_that_would_exceed_six_digits()
    {
        var starters = Enumerable
            .Range(1, 10_000)
            .Select(index => CreateStarter(resultatId: index, deltakerId: index));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ChampionshipStartNumbers.Create(
                starters,
                [new StevneInfo(413, "Skjaergards-smellen", "2026-05-23", 1)]));

        Assert.Contains("maximum 6 digits", ex.Message);
    }

    private static InrxStarter CreateStarter(int resultatId, int deltakerId) =>
        new(
            ResultatId: resultatId,
            DeltakerId: deltakerId,
            Standplass: 1,
            SkivenrFra: string.Empty,
            SkivenrTil: string.Empty,
            Relay: 1,
            RelayDate: "2026-05-23 10:00:00",
            NsfId: "905380",
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
            StevneName: "Skjaergards-smellen");
}
