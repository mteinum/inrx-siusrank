namespace InrxToSiusRank;

public sealed record StevneInfo(int Id, string Name, string Date, int ArrangementId);

public sealed record OvelseInfo(int Id, string Name, string ShortName, int HovedOvelseId);

public sealed record OvelseSummary(int Id, string Name, string ShortName, int HovedOvelseId, int StarterCount);

public sealed record KmNmClassSummary(string Name, int StarterCount, string Relays);

public sealed record SiusRankCsvExportFileResult(
    StevneInfo Stevne,
    OvelseInfo Ovelse,
    string KmNmClass,
    int StarterCount,
    string OutputPath,
    IReadOnlyList<string> Warnings);

public sealed record SiusRankCsvExportResult(
    string OutputDirectory,
    string? ShooterGroupsTemplatePath,
    IReadOnlyList<SiusRankCsvExportFileResult> Files);

public sealed record InrxStarter(
    int ResultatId,
    int DeltakerId,
    int Standplass,
    string SkivenrFra,
    string SkivenrTil,
    int? Relay,
    string RelayDate,
    string NsfId,
    string AccreditationNumber,
    string FirstName,
    string LastName,
    string BirthDay,
    string Gender,
    string Land,
    string ClubName,
    string ClubShortName,
    string InrxClass,
    string KmNmClass,
    string DmClass,
    string OvelseName,
    string StevneName,
    string Comment = "");

public sealed record SiusRankStarter(
    string StartNumber,
    string AccreditationNumber,
    string IssfId,
    string DisplayNameLong,
    string DisplayName,
    string FirstName,
    string Name,
    string BirthDay,
    string Gender,
    string Nation,
    string BibNumber,
    string TargetNumber,
    string Relay,
    string TeamIndex,
    string DuellIndex,
    string Groups,
    string Comment,
    string StarterId,
    string TeamPosition,
    string Team,
    string TeamDisplay,
    string TeamDuellIndex,
    string TeamComment)
{
    public string[] ToFields() =>
    [
        StartNumber,
        AccreditationNumber,
        IssfId,
        DisplayNameLong,
        DisplayName,
        FirstName,
        Name,
        BirthDay,
        Gender,
        Nation,
        BibNumber,
        TargetNumber,
        Relay,
        TeamIndex,
        DuellIndex,
        Groups,
        Comment,
        StarterId,
        TeamPosition,
        Team,
        TeamDisplay,
        TeamDuellIndex,
        TeamComment
    ];
}
