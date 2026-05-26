namespace InrxToSiusRank.Tests;

public sealed class SscLanePayloadTests
{
    [Fact]
    public void Reset_payload_generates_all_lanes_1_to_40()
    {
        var payload = SscLanePayloadBuilder.BuildReset(40, DateTimeOffset.UnixEpoch);

        Assert.Equal("reset", payload.Kind);
        Assert.Equal(40, payload.Lanes.Count);
        Assert.Equal(Enumerable.Range(1, 40), payload.Lanes.Select(lane => lane.Lane));
        Assert.All(payload.Lanes, lane => Assert.False(lane.Active));
    }

    [Fact]
    public void Active_payload_contains_only_selected_startlag()
    {
        var eventExport = new SscEventExport(
            new StevneInfo(405, "NM Fripistol", "2026-07-06 09:00:00", 1),
            new OvelseInfo(18, "Fripistol", "Fri", 2),
            [
                CreateStarter(resultatId: 1, deltakerId: 100, standplass: 1, relayDate: "2026-07-06 09:00:00"),
                CreateStarter(resultatId: 2, deltakerId: 200, standplass: 2, relayDate: "2026-07-06 11:00:00")
            ]);

        var payload = SscLanePayloadBuilder.BuildActive(
            [eventExport],
            new DateTime(2026, 7, 6, 9, 0, 0),
            new Dictionary<int, string> { [100] = "26001", [200] = "26002" },
            40,
            DateTimeOffset.UnixEpoch,
            out var messages);

        Assert.DoesNotContain(messages, message => message.Severity == SscValidationSeverity.Error);
        var lane = Assert.Single(payload.Lanes);
        Assert.Equal(1, lane.Lane);
        Assert.Equal("26001", lane.UserId);
        Assert.Equal("50m Fripistol", lane.ExerciseName);
    }

    private static InrxStarter CreateStarter(
        int resultatId,
        int deltakerId,
        int standplass,
        string relayDate) =>
        new(
            ResultatId: resultatId,
            DeltakerId: deltakerId,
            Standplass: standplass,
            SkivenrFra: string.Empty,
            SkivenrTil: string.Empty,
            Relay: 1,
            RelayDate: relayDate,
            NsfId: string.Empty,
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
            StevneName: "NM Fripistol");
}
