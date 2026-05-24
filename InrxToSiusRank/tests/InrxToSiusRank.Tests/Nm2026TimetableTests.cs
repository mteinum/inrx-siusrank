namespace InrxToSiusRank.Tests;

public sealed class Nm2026TimetableTests
{
    [Fact]
    public void Desired_times_include_extra_silhouette_and_shifted_hurtig_blocks()
    {
        var silhouette = Nm2026TimetablePlan.EventByStevneId(406);
        var hurtigFin = Nm2026TimetablePlan.EventByStevneId(410);
        var hurtigGrov = Nm2026TimetablePlan.EventByStevneId(411);

        Assert.Equal("2026-07-07 14:00:00", Format(silhouette.StartTimes[^1]));
        Assert.Equal("2026-07-11 12:20:00", Format(hurtigFin.StartTimes[^1]));
        Assert.Equal(
            ["2026-07-11 13:35:00", "2026-07-11 14:45:00", "2026-07-11 15:55:00"],
            hurtigGrov.StartTimes.Select(Format).ToArray());
    }

    [Fact]
    public void Silhouette_side_targets_map_to_vh_filters_and_sius_start_numbers()
    {
        var slots = Enumerable.Range(0, 4)
            .Select(Nm2026TimetablePlan.SilhouetteSlotForIndex)
            .ToList();

        Assert.Equal(
            [
                new Nm2026SilhouetteSlot(2, "V", 1000),
                new Nm2026SilhouetteSlot(4, "H", 1000),
                new Nm2026SilhouetteSlot(7, "V", 2000),
                new Nm2026SilhouetteSlot(9, "H", 2000)
            ],
            slots);
    }

    [Fact]
    public void Sequential_target_plan_caps_finpistol_to_thirty_five_per_relay()
    {
        var assignments = Nm2026TimetablePlan.PlanSequentialTargets(
            Enumerable.Range(1, 137).ToArray(),
            Enumerable.Range(1, 35).ToArray());

        Assert.Equal(4, assignments.Max(assignment => assignment.RelayNumber));
        Assert.All(
            assignments.GroupBy(assignment => assignment.RelayNumber),
            group => Assert.True(group.Count() <= 35));
        Assert.Equal(35, assignments.Count(assignment => assignment.RelayNumber == 1));
        Assert.Equal(32, assignments.Count(assignment => assignment.RelayNumber == 4));
    }

    private static string Format(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
}
