namespace InrxToSiusRank;

public sealed record SiusRankWritebackOptions(
    string DatabasePath,
    string ExportsDirectory,
    IReadOnlyList<int> StevneIds,
    string? BibMapPath,
    IReadOnlySet<string> EventFilters,
    bool Apply);

public sealed record BibMapEntry(
    string NsfId,
    string BibNumber,
    int DeltakerId,
    string Name,
    string Source);

public sealed record SiusRankExportCompetition(
    string SourcePath,
    string EventCode,
    string ShortName,
    string EventUnitName,
    string ProductType,
    string ResultStatus,
    DateTime LastWriteTime,
    IReadOnlyList<SiusRankExportAthlete> Athletes)
{
    public int StarterCount => Athletes.Count;

    public int ResultCount => Athletes.Count(athlete => athlete.HasResult);

    public int ShotResultCount => Athletes.Count(athlete => athlete.Shots.Count > 0);
}

public sealed record SiusRankExportAthlete(
    string BibNumber,
    string AccreditationNumber,
    string FamilyName,
    string GivenName,
    string DisplayName,
    int? Result,
    int? InnerTens,
    IReadOnlyList<SiusRankExportShot> Shots)
{
    public bool HasResult => Result is not null && Shots.Count > 0;

    public string NameForDisplay =>
        !string.IsNullOrWhiteSpace(DisplayName)
            ? DisplayName
            : $"{FamilyName} {GivenName}".Trim();
}

public sealed record SiusRankExportShot(
    int Position,
    int Score,
    decimal? X,
    decimal? Y,
    string TimeStamp);

public sealed record InrxWritebackInput(
    IReadOnlyList<InrxResultRow> Results,
    IReadOnlyDictionary<int, InrxOvelseDefinition> Ovelser);

public sealed record InrxResultRow(
    int ResultatId,
    int StevneId,
    int OvelseDefId,
    int DeltakerId,
    string NsfId,
    string Medlemsnummer,
    string FirstName,
    string LastName,
    int ExistingTotal,
    int ExistingInnerTens,
    int ExistingShotCount);

public sealed record InrxOvelseDefinition(
    int Id,
    string Name,
    string ShortName,
    int SkuddPerSerie,
    int SeriePerRang,
    IReadOnlyList<int> SeriesPerPart,
    int MlTarget)
{
    public int TotalSeries => SeriesPerPart.Sum();

    public int ExpectedShots => SkuddPerSerie * TotalSeries;
}

public sealed record InrxResultFields(
    IReadOnlyList<string> SeriesPerPart,
    IReadOnlyList<string> PartSumsText,
    IReadOnlyList<string> InnerTensPerPart,
    IReadOnlyList<string> XyPerPart,
    IReadOnlyList<int> SumPerPart,
    IReadOnlyList<int> SumRank,
    IReadOnlyList<int> InnerRank,
    int TotalScore,
    int InnerTens,
    string PerShotRanking,
    int MlTarget);

public sealed record PlannedSiusRankWriteback(
    string ShortName,
    string SourcePath,
    int ResultatId,
    int StevneId,
    int OvelseDefId,
    int DeltakerId,
    string BibNumber,
    string AccreditationNumber,
    string DisplayName,
    int ExistingTotal,
    int ExistingInnerTens,
    int ExistingShotCount,
    InrxResultFields Fields)
{
    public int NewTotal => Fields.TotalScore;

    public int NewInnerTens => Fields.InnerTens;

    public int NewShotCount => Fields.SeriesPerPart.Sum(series =>
        series.Split(';', StringSplitOptions.RemoveEmptyEntries).Sum(item => item.Length));
}

public sealed record SkippedSiusRankWriteback(
    string ShortName,
    string BibNumber,
    string AccreditationNumber,
    string DisplayName,
    string Reason);

public sealed record SiusRankWritebackEventPlan(
    SiusRankExportCompetition Export,
    int? OvelseDefId,
    IReadOnlyList<PlannedSiusRankWriteback> Updates,
    IReadOnlyList<SkippedSiusRankWriteback> Skipped,
    IReadOnlyList<string> Warnings);

public sealed record SiusRankWritebackResult(
    bool Applied,
    string? BackupPath,
    IReadOnlyList<SiusRankWritebackEventPlan> Events,
    IReadOnlyList<string> Warnings)
{
    public int UpdateCount => Events.Sum(item => item.Updates.Count);

    public int SkippedCount => Events.Sum(item => item.Skipped.Count);
}
