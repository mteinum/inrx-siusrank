namespace InrxToSiusRank.Tests;

public sealed class SeedStartLagPlannerTests
{
    [Fact]
    public void Planner_keeps_class_blocks_and_places_seed_group_in_own_relay()
    {
        var input = CreateInput(
        [
            Shooter(1, "Å regular one", "Å", "p1", 1, 3),
            Shooter(2, "Å ranked outside seed", "Å", "p2", 1, 8),
            Shooter(3, "Å seed one", "Å", "p3", 1, 13),
            Shooter(4, "Å seed two", "Å", "p4", 2, 3),
            Shooter(5, "Å seed three", "Å", "p5", 2, 8),
            Shooter(6, "Junior regular", "Jr-NM", "p6", 2, 13),
            Shooter(7, "Junior seed", "Jr-NM", "p7", 3, 3),
            Shooter(8, "Veteran", "V55", "p8", 3, 8)
        ]);
        var rankings = new[]
        {
            Ranking("p3", 1, 560),
            Ranking("p4", 2, 555),
            Ranking("p5", 3, 550),
            Ranking("p2", 4, 545),
            Ranking("p7", 1, 530)
        };

        var plan = SeedStartLagPlanner.Plan(input, rankings);

        var seedAssignments = plan.Assignments.Where(assignment => assignment.KmNmClass == "Å" && assignment.IsSeed).ToList();
        Assert.Equal(new[] { 3, 4, 5 }, seedAssignments.Select(assignment => assignment.ResultatId));
        Assert.All(seedAssignments, assignment => Assert.Equal(2, assignment.RelayNumber));
        Assert.Equal(new[] { 2, 4, 7 }, seedAssignments.Select(assignment => assignment.TargetNumber));

        Assert.Equal(3, plan.Assignments.Single(assignment => assignment.ResultatId == 6).RelayNumber);
        Assert.Equal(3, plan.Assignments.Single(assignment => assignment.ResultatId == 7).RelayNumber);
        Assert.False(plan.Assignments.Single(assignment => assignment.ResultatId == 6).IsSeed);
        Assert.True(plan.Assignments.Single(assignment => assignment.ResultatId == 7).IsSeed);
        Assert.Equal(3, plan.Assignments.Single(assignment => assignment.ResultatId == 8).RelayNumber);
    }

    [Fact]
    public void Planner_caps_seed_count_to_one_relay_capacity()
    {
        var shooters = Enumerable.Range(1, 10)
            .Select(id => Shooter(id, $"Shooter {id}", "Å", $"p{id}", 1, id))
            .ToList();
        var rankings = Enumerable.Range(1, 10)
            .Select(id => Ranking($"p{id}", id, 600 - id))
            .ToList();

        var plan = SeedStartLagPlanner.Plan(CreateInput(shooters), rankings);

        Assert.Equal(3, plan.ClassSummaries.Single().SeedCount);
        Assert.Equal(3, plan.Assignments.Count(assignment => assignment.IsSeed));
    }

    [Fact]
    public void Planner_fills_remaining_targets_after_sparse_seed_group()
    {
        var input = CreateInput(
        [
            Shooter(1, "Å seed one", "Å", "p1", 1, 3),
            Shooter(2, "Å seed two", "Å", "p2", 1, 8),
            Shooter(3, "Veteran", "V55", "p3", 2, 3)
        ]);
        var rankings = new[]
        {
            Ranking("p1", 1, 560),
            Ranking("p2", 2, 555)
        };

        var plan = SeedStartLagPlanner.Plan(input, rankings);

        Assert.Equal(1, plan.RequiredRelayCount);
        Assert.Equal(new[] { 2, 4 }, plan.Assignments.Where(assignment => assignment.IsSeed).Select(assignment => assignment.TargetNumber));
        Assert.Equal(7, plan.Assignments.Single(assignment => assignment.ResultatId == 3).TargetNumber);
    }

    [Fact]
    public void Planner_places_non_silhouette_seed_group_to_avoid_holes()
    {
        var input = CreateInput(
        [
            Shooter(1, "Å regular one", "Å", "p1", 1, 1),
            Shooter(2, "Å regular two", "Å", "p2", 1, 2),
            Shooter(3, "Å regular three", "Å", "p3", 1, 3),
            Shooter(4, "Å regular four", "Å", "p4", 1, 4),
            Shooter(5, "Å seed one", "Å", "p5", 2, 1),
            Shooter(6, "Å seed two", "Å", "p6", 2, 2)
        ],
        new OvelseInfo(10, "Standard", "Std", 9),
        [1, 2, 3, 4, 5]);
        var rankings = new[]
        {
            Ranking("p5", 1, 560),
            Ranking("p6", 2, 555)
        };

        var plan = SeedStartLagPlanner.Plan(input, rankings);

        var seedAssignments = plan.Assignments.Where(assignment => assignment.IsSeed).ToList();
        Assert.All(seedAssignments, assignment => Assert.Equal(1, assignment.RelayNumber));
        Assert.Equal(new[] { 4, 5 }, seedAssignments.Select(assignment => assignment.TargetNumber));
        Assert.Equal(2, plan.Assignments.Single(assignment => assignment.ResultatId == 4).RelayNumber);
        Assert.Equal(1, plan.Assignments.Single(assignment => assignment.ResultatId == 4).TargetNumber);
    }

    private static SeedStartLagEventInput CreateInput(
        IReadOnlyList<SeedStartLagShooter> shooters,
        OvelseInfo? ovelse = null,
        IReadOnlyList<int>? targets = null) =>
        new(
            new StevneInfo(406, "Silhuett", "2026-07-07 09:00:00", 378),
            ovelse ?? new OvelseInfo(11, "Silhuett", "Sil", 8),
            "discipline",
        targets ?? [2, 4, 7],
            TimeSpan.FromMinutes(45),
            [],
            shooters);

    private static SeedStartLagShooter Shooter(
        int id,
        string name,
        string kmNmClass,
        string sa2Id,
        int oldRelay,
        int oldTarget) =>
        new(id, name, "KPS", sa2Id, kmNmClass, oldRelay, oldTarget);

    private static RankingEntry Ranking(string personId, int position, decimal score) =>
        new(personId, personId, position, score);
}
