namespace InrxToSiusRank;

public sealed record ExportSscUsersOptions(
    string DatabasePath,
    IReadOnlyList<int> StevneIds,
    string? BibMapPath,
    string? OutputPath,
    string OrganizationName,
    string OrganizationId,
    string EncodingName);

public sealed record ValidateSscOptions(
    string DatabasePath,
    IReadOnlyList<int> StevneIds,
    string? BibMapPath,
    string UsersCsvPath);

public sealed record ExportSscLanesOptions(
    string DatabasePath,
    int StevneId,
    string Startlag,
    string? BibMapPath,
    string OutputDirectory,
    int LaneCount);

public enum SscValidationSeverity
{
    Warning,
    Error
}

public sealed record SscValidationMessage(
    SscValidationSeverity Severity,
    string Message);

public sealed record SscUser(
    string OrganizationName,
    string OrganizationId,
    string UserId,
    string Name,
    string FirstName,
    string DisplayName,
    string NationName,
    string DisplayNationName,
    string ISOCode,
    string IOCCode,
    string UserClassName,
    string UserClassId,
    string UserGroupName,
    string UserGroupId,
    string ShootingSportsCloudUserId,
    string DateOfBirth,
    string Gender,
    string UserPictureId,
    string UserPreferredLanguage)
{
    public string[] ToFields() =>
    [
        OrganizationName,
        OrganizationId,
        UserId,
        Name,
        FirstName,
        DisplayName,
        NationName,
        DisplayNationName,
        ISOCode,
        IOCCode,
        UserClassName,
        UserClassId,
        UserGroupName,
        UserGroupId,
        ShootingSportsCloudUserId,
        DateOfBirth,
        Gender,
        UserPictureId,
        UserPreferredLanguage
    ];
}

public sealed record SscUsersExportResult(
    string? OutputPath,
    string? BibMapPath,
    bool Written,
    int UserCount,
    IReadOnlyList<SscValidationMessage> Messages)
{
    public bool HasErrors => Messages.Any(message => message.Severity == SscValidationSeverity.Error);
}

public sealed record SscValidationResult(
    int StarterCount,
    int UserCount,
    IReadOnlyList<SscValidationMessage> Messages)
{
    public bool HasErrors => Messages.Any(message => message.Severity == SscValidationSeverity.Error);
}

public sealed record SscLanesExportResult(
    string OutputDirectory,
    string ResetPath,
    string ActiveLanesPath,
    int LaneCount,
    int ActiveLaneCount,
    IReadOnlyList<SscValidationMessage> Messages)
{
    public bool HasErrors => Messages.Any(message => message.Severity == SscValidationSeverity.Error);
}

public sealed record SscEventExport(
    StevneInfo Stevne,
    OvelseInfo Ovelse,
    IReadOnlyList<InrxStarter> Starters);

public sealed record SscLanePayload(
    string Spec,
    string Kind,
    DateTimeOffset GeneratedAtUtc,
    int LaneCount,
    int? StevneId,
    string? Startlag,
    string? Source,
    string Warning,
    IReadOnlyList<SscLaneAssignment> Lanes);

public sealed record SscLaneAssignment(
    int Lane,
    bool Active,
    string UserId,
    string DisplayName,
    string ExerciseName,
    int? DeltakerId,
    int? ResultatId);
