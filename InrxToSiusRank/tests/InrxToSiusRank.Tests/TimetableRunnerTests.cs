namespace InrxToSiusRank.Tests;

public sealed class TimetableRunnerTests
{
    [Fact]
    public void BuildEvent_counts_classes_per_startlag()
    {
        var input = new SeedStartLagEventInput(
            new StevneInfo(406, "NM Silhuett", "2026-07-07 09:00:00", 378),
            new OvelseInfo(11, "Silhuett", "Sil", 8),
            "discipline",
            [3, 8, 13],
            TimeSpan.FromMinutes(45),
            [
                new StartLagInfo(1, 1, "2026-07-07 09:00:00"),
                new StartLagInfo(2, 2, "2026-07-07 09:45:00")
            ],
            [
                Shooter(1, "Å One", "Å", 1, 3),
                Shooter(2, "Å Two", "Å", 1, 8),
                Shooter(3, "Junior", "Jr-NM", 1, 13),
                Shooter(4, "Veteran", "V55", 2, 3)
            ]);

        var timetableEvent = TimetableRunner.BuildEvent(input);

        Assert.Equal("Å 2, Jr-NM 1", timetableEvent.Relays[0].ClassSummary);
        Assert.Equal(3, timetableEvent.Relays[0].ShooterCount);
        Assert.Equal(3, timetableEvent.Relays[0].Capacity);
        Assert.Equal("V55 1", timetableEvent.Relays[1].ClassSummary);
    }

    [Fact]
    public void BuildEvent_duplicates_fin_and_grov_pistol_for_precision_and_rapid_days()
    {
        var input = new SeedStartLagEventInput(
            new StevneInfo(408, "NM Finpistol", "2026-07-09 09:00:00", 380),
            new OvelseInfo(9, "Finpistol", "Fin", 10),
            "discipline",
            [1, 2, 3],
            TimeSpan.FromMinutes(75),
            [
                new StartLagInfo(1, 1, "2026-07-09 09:00:00"),
                new StartLagInfo(2, 2, "2026-07-09 10:15:00")
            ],
            [
                Shooter(1, "K One", "K", 1, 1),
                Shooter(2, "K Two", "K", 2, 1)
            ]);

        var timetableEvent = TimetableRunner.BuildEvent(input);

        Assert.Equal(4, timetableEvent.Relays.Count);
        Assert.Equal(
            ["2026-07-09 09:00:00", "2026-07-09 10:15:00", "2026-07-10 09:00:00", "2026-07-10 10:15:00"],
            timetableEvent.Relays.Select(relay => relay.Date));
        Assert.Equal(["Precision", "Precision", "Rapid", "Rapid"], timetableEvent.Relays.Select(relay => relay.StageName));
        Assert.Equal([1, 2, 1, 2], timetableEvent.Relays.Select(relay => relay.Number));
    }

    private static SeedStartLagShooter Shooter(
        int id,
        string name,
        string kmNmClass,
        int oldRelay,
        int oldTarget) =>
        new(id, name, "KPS", $"p{id}", kmNmClass, oldRelay, oldTarget);
}
