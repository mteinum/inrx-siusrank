namespace InrxToSiusRank;

public sealed record SeedStartLagOptions(
    string DatabasePath,
    IReadOnlyList<int> StevneIds,
    string RankingPeriodStart,
    string RankingPeriodEnd,
    bool Apply,
    int SilhouetteShootersPerStand = 2);

public sealed record SeedStartLagEventInput(
    StevneInfo Stevne,
    OvelseInfo Ovelse,
    string DisciplineId,
    IReadOnlyList<int> Targets,
    TimeSpan RelayInterval,
    IReadOnlyList<StartLagInfo> StartLags,
    IReadOnlyList<SeedStartLagShooter> Shooters);

public sealed record StartLagInfo(int Id, int Nr, string Date);

public sealed record SeedStartLagShooter(
    int ResultatId,
    string DisplayName,
    string ClubShortName,
    string Sa2Id,
    string KmNmClass,
    int? OldRelay,
    int OldTarget);

public sealed record RankingEntry(
    string PersonId,
    string FullName,
    int Position,
    decimal TotalScore);

public sealed record PlannedStartLagAssignment(
    int ResultatId,
    string DisplayName,
    string ClubShortName,
    string KmNmClass,
    int RelayNumber,
    int TargetNumber,
    int? OldRelayNumber,
    int OldTargetNumber,
    bool IsSeed,
    int? RankingPosition,
    decimal? RankingScore);

public sealed record SeedStartLagClassSummary(
    string ClassName,
    bool IsSeedEligible,
    int StarterCount,
    int RankedCount,
    int SeedCount,
    int UnrankedCount);

public sealed record SeedStartLagSeedGroup(
    string ClassName,
    IReadOnlyList<PlannedStartLagAssignment> Seeds);

public sealed record SeedStartLagEventPlan(
    StevneInfo Stevne,
    OvelseInfo Ovelse,
    string DisciplineId,
    IReadOnlyList<int> Targets,
    TimeSpan RelayInterval,
    IReadOnlyList<StartLagInfo> ExistingStartLags,
    IReadOnlyList<PlannedStartLagAssignment> Assignments,
    IReadOnlyList<SeedStartLagClassSummary> ClassSummaries,
    IReadOnlyList<SeedStartLagSeedGroup> SeedGroups,
    IReadOnlyList<string> Warnings)
{
    public int RequiredRelayCount => Assignments.Count == 0
        ? 0
        : Assignments.Max(assignment => assignment.RelayNumber);

    public int MoveCount => Assignments.Count(assignment =>
        assignment.OldRelayNumber != assignment.RelayNumber ||
        assignment.OldTargetNumber != assignment.TargetNumber);
}

public sealed record SeedStartLagResult(
    bool Applied,
    string? BackupPath,
    IReadOnlyList<SeedStartLagEventPlan> Plans);
