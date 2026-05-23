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

    [Fact]
    public void Create_preserves_existing_bib_map_and_allocates_next_free_number()
    {
        var directory = CreateTempDirectory();
        var bibMapPath = Path.Combine(directory, ChampionshipStartNumbers.BibMapFileName);
        File.WriteAllText(
            bibMapPath,
            "nsfId,bibNumber,deltakerId,name,source\r\n" +
            "905380,26005,20,TEINUM Morten,manual\r\n");

        try
        {
            var numbers = ChampionshipStartNumbers.Create(
                [
                    CreateStarter(resultatId: 1, deltakerId: 20, nsfId: "905380", firstName: "Morten", lastName: "Teinum"),
                    CreateStarter(resultatId: 2, deltakerId: 10, nsfId: "123456", firstName: "Kari", lastName: "Nordmann")
                ],
                [CreateStevne()],
                bibMapPath);

            Assert.Equal("26001", numbers[10]);
            Assert.Equal("26005", numbers[20]);

            var bibMap = File.ReadAllText(bibMapPath);
            Assert.Contains("123456,26001,10,NORDMANN Kari,allocated by InrxToSiusRank", bibMap);
            Assert.Contains("905380,26005,20,TEINUM Morten,manual", bibMap);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Create_uses_deltaker_id_as_bib_map_key_not_nsf_id()
    {
        var directory = CreateTempDirectory();
        var bibMapPath = Path.Combine(directory, ChampionshipStartNumbers.BibMapFileName);
        File.WriteAllText(
            bibMapPath,
            "nsfId,bibNumber,deltakerId,name,source\r\n" +
            "905380,26005,20,TEINUM Morten,manual\r\n");

        try
        {
            var numbers = ChampionshipStartNumbers.Create(
                [CreateStarter(resultatId: 1, deltakerId: 99, nsfId: "905380")],
                [CreateStevne()],
                bibMapPath);

            Assert.Equal("26001", numbers[99]);

            var bibMap = File.ReadAllText(bibMapPath);
            Assert.Contains("905380,26001,99,TEINUM Morten,allocated by InrxToSiusRank", bibMap);
            Assert.Contains("905380,26005,20,TEINUM Morten,manual", bibMap);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "InrxToSiusRank.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static StevneInfo CreateStevne() =>
        new(413, "Skjaergards-smellen", "2026-05-23", 1);

    private static InrxStarter CreateStarter(
        int resultatId,
        int deltakerId,
        string nsfId = "905380",
        string firstName = "Morten",
        string lastName = "Teinum") =>
        new(
            ResultatId: resultatId,
            DeltakerId: deltakerId,
            Standplass: 1,
            SkivenrFra: string.Empty,
            SkivenrTil: string.Empty,
            Relay: 1,
            RelayDate: "2026-05-23 10:00:00",
            NsfId: nsfId,
            AccreditationNumber: string.Empty,
            FirstName: firstName,
            LastName: lastName,
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
