using System.Globalization;

namespace InrxToSiusRank;

public static class SeedStartLagRunner
{
    public static async Task<SeedStartLagResult> RunAsync(
        SeedStartLagOptions options,
        ISeedStartLagRankingProvider? rankingProvider = null,
        CancellationToken cancellationToken = default)
    {
        using var repository = new SeedStartLagRepository(options.DatabasePath);
        var inputs = repository.GetEventInputs(options.StevneIds, options.SilhouetteShootersPerStand);
        using var ownedRankingClient = rankingProvider is null ? new NsfRankingClient() : null;
        var provider = rankingProvider ?? ownedRankingClient!;

        var plans = new List<SeedStartLagEventPlan>();
        foreach (var input in inputs)
        {
            var rankings = await provider.GetRankingAsync(
                input.DisciplineId,
                options.RankingPeriodStart,
                options.RankingPeriodEnd,
                cancellationToken);
            plans.Add(SeedStartLagPlanner.Plan(input, rankings));
        }

        string? backupPath = null;
        if (options.Apply)
        {
            backupPath = SeedStartLagRepository.CreateBackup(options.DatabasePath);
            SeedStartLagRepository.Apply(options.DatabasePath, plans);
        }

        return new SeedStartLagResult(options.Apply, backupPath, plans);
    }
}

public static class SeedStartLagReporter
{
    public static void Print(SeedStartLagResult result)
    {
        Console.WriteLine(result.Applied
            ? "NM startlag updates applied."
            : "Dry run only. No database changes written.");
        if (!string.IsNullOrWhiteSpace(result.BackupPath))
        {
            Console.WriteLine($"Backup: {Path.GetFullPath(result.BackupPath)}");
        }

        foreach (var plan in result.Plans)
        {
            PrintPlan(plan);
        }
    }

    private static void PrintPlan(SeedStartLagEventPlan plan)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"{plan.Stevne.Id}: {plan.Stevne.Name} - {plan.Ovelse.Name} " +
            $"({plan.Assignments.Count} starters, {plan.RequiredRelayCount} startlag, " +
            $"{plan.Targets.Count} targets/startlag)");
        Console.WriteLine($"Targets: {string.Join(", ", plan.Targets)}");
        Console.WriteLine($"Moves: {plan.MoveCount}");

        foreach (var summary in plan.ClassSummaries)
        {
            var marker = summary.IsSeedEligible ? ", seed eligible" : string.Empty;
            Console.WriteLine(
                $"  {summary.ClassName}: starters={summary.StarterCount}, ranked={summary.RankedCount}, " +
                $"seeded={summary.SeedCount}, unranked={summary.UnrankedCount}{marker}");
        }

        foreach (var seedGroup in plan.SeedGroups)
        {
            Console.WriteLine($"  Seed group {seedGroup.ClassName}:");
            foreach (var seed in seedGroup.Seeds)
            {
                var rank = seed.RankingPosition is null
                    ? string.Empty
                    : $" rank {seed.RankingPosition}, score {seed.RankingScore?.ToString("0.###", CultureInfo.InvariantCulture)}";
                Console.WriteLine(
                    $"    Lag {seed.RelayNumber}, target {seed.TargetNumber}: " +
                    $"{seed.DisplayName} ({seed.ClubShortName}){rank}");
            }
        }

        if (plan.Ovelse.Name.Equals("Silhuett", StringComparison.OrdinalIgnoreCase))
        {
            PrintRelayLayout(plan);
        }

        foreach (var warning in plan.Warnings)
        {
            Console.WriteLine($"  WARNING: {warning}");
        }
    }

    private static void PrintRelayLayout(SeedStartLagEventPlan plan)
    {
        Console.WriteLine("  Layout:");
        for (var relay = 1; relay <= plan.RequiredRelayCount; relay++)
        {
            Console.WriteLine($"    Startlag {relay}:");
            foreach (var target in plan.Targets)
            {
                var assignment = plan.Assignments.FirstOrDefault(item =>
                    item.RelayNumber == relay && item.TargetNumber == target);
                if (assignment is null)
                {
                    Console.WriteLine($"      {target}: <ledig>");
                    continue;
                }

                var seed = assignment.IsSeed ? " *seed" : string.Empty;
                Console.WriteLine(
                    $"      {target}: {assignment.KmNmClass} {assignment.DisplayName} " +
                    $"({assignment.ClubShortName}){seed}");
            }
        }
    }
}
