namespace InrxToSiusRank;

public sealed record SiusRankCsvExportOptions(
    string DatabasePath,
    int? StevneId,
    IReadOnlyList<int> StevneIds,
    DateOnly? EventDate,
    string? EventName,
    int? OvelseId,
    string? OvelseName,
    string? ShooterGroupsTemplatePath,
    string OutputDirectory,
    string EncodingName,
    int SilhouetteShootersPerStand = 2);
