namespace InrxToSiusRank;

public static class SeedStartLagPlanner
{
    private static readonly HashSet<string> SeedEligibleClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Å",
        "M",
        "K",
        "Jr-NM",
        "Jm",
        "Jk"
    };

    public static SeedStartLagEventPlan Plan(
        SeedStartLagEventInput input,
        IReadOnlyList<RankingEntry> rankings)
    {
        if (input.Targets.Count == 0)
        {
            throw new InvalidOperationException($"No targets configured for {input.Ovelse.Name}.");
        }

        var rankingByPersonId = rankings
            .Where(ranking => !string.IsNullOrWhiteSpace(ranking.PersonId))
            .GroupBy(ranking => ranking.PersonId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.Position).First(), StringComparer.OrdinalIgnoreCase);

        var classOrder = input.Shooters
            .Select(shooter => shooter.KmNmClass)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var slots = new List<PendingAssignment?>();
        var classSummaries = new List<SeedStartLagClassSummary>();

        foreach (var className in classOrder)
        {
            var classShooters = input.Shooters
                .Where(shooter => className.Equals(shooter.KmNmClass, StringComparison.OrdinalIgnoreCase))
                .Select(shooter => PendingAssignment.From(shooter, rankingByPersonId))
                .ToList();
            var isSeedEligible = SeedEligibleClasses.Contains(className);
            var ranked = classShooters
                .Where(shooter => shooter.Ranking is not null)
                .OrderBy(shooter => shooter.Ranking!.Position)
                .ThenByDescending(shooter => shooter.Ranking!.TotalScore)
                .ThenBy(shooter => shooter.Shooter.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var seedCount = isSeedEligible
                ? Math.Min(Math.Min(15, input.Targets.Count), ranked.Count)
                : 0;
            var seedIds = ranked
                .Take(seedCount)
                .Select(shooter => shooter.Shooter.ResultatId)
                .ToHashSet();
            var seedShooters = classShooters
                .Where(shooter => seedIds.Contains(shooter.Shooter.ResultatId))
                .OrderBy(shooter => shooter.Ranking!.Position)
                .ThenByDescending(shooter => shooter.Ranking!.TotalScore)
                .ThenBy(shooter => shooter.Shooter.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(shooter => shooter with { IsSeed = true })
                .ToList();
            var regularShooters = classShooters
                .Where(shooter => !seedIds.Contains(shooter.Shooter.ResultatId))
                .ToList();

            AddClassShooters(slots, regularShooters, seedShooters, input.Targets.Count, input.Ovelse);

            classSummaries.Add(new SeedStartLagClassSummary(
                className,
                isSeedEligible,
                classShooters.Count,
                ranked.Count,
                seedShooters.Count,
                classShooters.Count - ranked.Count));
        }

        var assignments = BuildAssignments(slots, input.Targets);
        var seedGroups = assignments
            .Where(assignment => assignment.IsSeed)
            .GroupBy(assignment => assignment.KmNmClass, StringComparer.OrdinalIgnoreCase)
            .Select(group => new SeedStartLagSeedGroup(group.Key, group.ToList()))
            .ToList();
        var warnings = BuildWarnings(input, assignments, classSummaries);

        return new SeedStartLagEventPlan(
            input.Stevne,
            input.Ovelse,
            input.DisciplineId,
            input.Targets,
            input.RelayInterval,
            input.StartLags,
            assignments,
            classSummaries,
            seedGroups,
            warnings);
    }

    private static void PadToNextRelay(List<PendingAssignment?> slots, int relayCapacity)
    {
        while (slots.Count % relayCapacity != 0)
        {
            slots.Add(null);
        }
    }

    private static void AddSeedGroup(
        List<PendingAssignment?> slots,
        IReadOnlyList<PendingAssignment> seedShooters,
        int relayCapacity)
    {
        if (seedShooters.Count == 0)
        {
            return;
        }

        var usedInRelay = slots.Count % relayCapacity;
        var remainingInRelay = usedInRelay == 0
            ? relayCapacity
            : relayCapacity - usedInRelay;
        if (seedShooters.Count > remainingInRelay)
        {
            PadToNextRelay(slots, relayCapacity);
        }

        slots.AddRange(seedShooters);
    }

    private static void AddClassShooters(
        List<PendingAssignment?> slots,
        IReadOnlyList<PendingAssignment> regularShooters,
        IReadOnlyList<PendingAssignment> seedShooters,
        int relayCapacity,
        OvelseInfo ovelse)
    {
        if (seedShooters.Count == 0 || IsSilhouette(ovelse))
        {
            slots.AddRange(regularShooters);
            AddSeedGroup(slots, seedShooters, relayCapacity);
            return;
        }

        var prefixCount = FindBestSeedPrefixCount(
            slots.Count,
            regularShooters.Count,
            seedShooters.Count,
            relayCapacity);

        slots.AddRange(regularShooters.Take(prefixCount));
        AddSeedGroup(slots, seedShooters, relayCapacity);
        slots.AddRange(regularShooters.Skip(prefixCount));
    }

    private static int FindBestSeedPrefixCount(
        int currentSlotCount,
        int regularCount,
        int seedCount,
        int relayCapacity)
    {
        var bestPrefixCount = 0;
        var bestPadding = int.MaxValue;
        var bestFinalSlotCount = int.MaxValue;

        for (var prefixCount = 0; prefixCount <= regularCount; prefixCount++)
        {
            var slotCountBeforeSeed = currentSlotCount + prefixCount;
            var remainingInRelay = RemainingInRelay(slotCountBeforeSeed, relayCapacity);
            var padding = seedCount > remainingInRelay
                ? remainingInRelay
                : 0;
            var finalSlotCount = currentSlotCount + regularCount + seedCount + padding;

            if (padding < bestPadding ||
                (padding == bestPadding && finalSlotCount < bestFinalSlotCount) ||
                (padding == bestPadding && finalSlotCount == bestFinalSlotCount && prefixCount > bestPrefixCount))
            {
                bestPrefixCount = prefixCount;
                bestPadding = padding;
                bestFinalSlotCount = finalSlotCount;
            }
        }

        return bestPrefixCount;
    }

    private static int RemainingInRelay(int slotCount, int relayCapacity)
    {
        var usedInRelay = slotCount % relayCapacity;
        return usedInRelay == 0
            ? relayCapacity
            : relayCapacity - usedInRelay;
    }

    private static bool IsSilhouette(OvelseInfo ovelse) =>
        ovelse.Id == 11 ||
        ovelse.Name.Equals("Silhuett", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<PlannedStartLagAssignment> BuildAssignments(
        IReadOnlyList<PendingAssignment?> slots,
        IReadOnlyList<int> targets)
    {
        var assignments = new List<PlannedStartLagAssignment>();
        for (var index = 0; index < slots.Count; index++)
        {
            var pending = slots[index];
            if (pending is null)
            {
                continue;
            }

            var relayNumber = index / targets.Count + 1;
            var targetNumber = targets[index % targets.Count];
            assignments.Add(new PlannedStartLagAssignment(
                pending.Shooter.ResultatId,
                pending.Shooter.DisplayName,
                pending.Shooter.ClubShortName,
                pending.Shooter.KmNmClass,
                relayNumber,
                targetNumber,
                pending.Shooter.OldRelay,
                pending.Shooter.OldTarget,
                pending.IsSeed,
                pending.Ranking?.Position,
                pending.Ranking?.TotalScore));
        }

        return assignments;
    }

    private static IReadOnlyList<string> BuildWarnings(
        SeedStartLagEventInput input,
        IReadOnlyList<PlannedStartLagAssignment> assignments,
        IReadOnlyList<SeedStartLagClassSummary> classSummaries)
    {
        var warnings = new List<string>();
        foreach (var duplicate in assignments
                     .GroupBy(assignment => (assignment.RelayNumber, assignment.TargetNumber))
                     .Where(group => group.Count() > 1))
        {
            warnings.Add(
                $"Duplicate relay/target Relay={duplicate.Key.RelayNumber}, Target={duplicate.Key.TargetNumber}: " +
                string.Join(", ", duplicate.Select(assignment => assignment.ResultatId)));
        }

        foreach (var overflow in assignments
                     .GroupBy(assignment => assignment.RelayNumber)
                     .Where(group => group.Count() > input.Targets.Count))
        {
            warnings.Add($"Relay {overflow.Key} has {overflow.Count()} starters but only {input.Targets.Count} targets.");
        }

        foreach (var summary in classSummaries.Where(summary => summary.IsSeedEligible && summary.UnrankedCount > 0))
        {
            warnings.Add(
                $"{summary.ClassName}: {summary.UnrankedCount} of {summary.StarterCount} starter(s) had no NSF ranking match.");
        }

        return warnings;
    }

    private sealed record PendingAssignment(
        SeedStartLagShooter Shooter,
        RankingEntry? Ranking,
        bool IsSeed)
    {
        public static PendingAssignment From(
            SeedStartLagShooter shooter,
            IReadOnlyDictionary<string, RankingEntry> rankingByPersonId)
        {
            RankingEntry? ranking = null;
            if (!string.IsNullOrWhiteSpace(shooter.Sa2Id))
            {
                rankingByPersonId.TryGetValue(shooter.Sa2Id, out ranking);
            }

            return new PendingAssignment(shooter, ranking, IsSeed: false);
        }
    }
}
