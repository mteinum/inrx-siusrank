namespace InrxToSiusRank;

public sealed record TimetableOptions(
    string DatabasePath,
    IReadOnlyList<int> StevneIds);

public sealed record TimetableResult(IReadOnlyList<TimetableEvent> Events);

public sealed record TimetableEvent(
    StevneInfo Stevne,
    OvelseInfo Ovelse,
    IReadOnlyList<int> Targets,
    IReadOnlyList<TimetableRelay> Relays);

public sealed record TimetableRelay(
    int Number,
    string Date,
    int ShooterCount,
    int Capacity,
    string ClassSummary,
    string StageName = "");
