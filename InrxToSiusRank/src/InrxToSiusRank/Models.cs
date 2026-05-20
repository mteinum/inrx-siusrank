using System.Globalization;

namespace InrxToSiusRank;

public sealed record AppOptions(
    string DatabasePath,
    int? StevneId,
    IReadOnlyList<int> StevneIds,
    DateOnly? EventDate,
    string? EventName,
    int? OvelseId,
    string? OvelseName,
    string? KmNmClass,
    string? SiusGroupOverride,
    string? ShooterGroupsTemplatePath,
    string? OutputDirectory,
    string? OutputPath,
    bool CopyToClipboard,
    string EncodingName,
    bool IncludeClubTeam,
    bool AllClasses,
    bool Wizard)
{
    public static AppOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count > 0 && args[0].Equals("wizard", StringComparison.OrdinalIgnoreCase))
        {
            args = args.Skip(1).Prepend("--wizard").ToArray();
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument '{token}'.");
            }

            var nameAndValue = token[2..].Split('=', 2);
            var name = nameAndValue[0];
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Empty option name.");
            }

            if (name.Equals("all-classes", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("include-club-team", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("clipboard", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("copy-to-clipboard", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("wizard", StringComparison.OrdinalIgnoreCase))
            {
                flags.Add(name);
                continue;
            }

            string value;
            if (nameAndValue.Length == 2)
            {
                value = nameAndValue[1];
            }
            else
            {
                if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Option '--{name}' requires a value.");
                }

                value = args[++i];
            }

            values[name] = value;
        }

        var databasePath = Require(values, "db");
        if (!File.Exists(databasePath))
        {
            throw new ArgumentException($"Database file does not exist: {databasePath}");
        }

        if (flags.Contains("wizard"))
        {
            return new AppOptions(
                databasePath,
                StevneId: null,
                StevneIds: [],
                EventDate: null,
                EventName: null,
                OvelseId: null,
                OvelseName: null,
                KmNmClass: null,
                SiusGroupOverride: null,
                ShooterGroupsTemplatePath: null,
                OutputDirectory: null,
                OutputPath: null,
                CopyToClipboard: false,
                EncodingName: CsvEncoding.Utf8Bom,
                IncludeClubTeam: false,
                AllClasses: false,
                Wizard: true);
        }

        var stevneId = ParseNullableInt(values, "stevne-id");
        var stevneIds = values.TryGetValue("stevne-ids", out var stevneIdsValue)
            ? ParseIdList(stevneIdsValue, "stevne-ids")
            : [];
        var eventDate = ParseNullableDate(values, "event-date");
        values.TryGetValue("event-name", out var eventName);

        var allClasses = flags.Contains("all-classes");
        if (stevneId is null && stevneIds.Count == 0 && eventDate is null)
        {
            throw new ArgumentException("Use --stevne-id, --stevne-ids, or --event-date to select an event.");
        }

        if (stevneId is not null && stevneIds.Count > 0)
        {
            throw new ArgumentException("Use either --stevne-id or --stevne-ids, not both.");
        }

        if (!allClasses && stevneIds.Count > 0)
        {
            throw new ArgumentException("Use --stevne-ids together with --all-classes.");
        }

        if (stevneIds.Count > 0 && eventDate is not null)
        {
            throw new ArgumentException("Use either --stevne-ids or --event-date, not both.");
        }

        var ovelseId = ParseNullableInt(values, "ovelse-id");
        values.TryGetValue("ovelse", out var ovelseName);
        if (!allClasses && ovelseId is null && string.IsNullOrWhiteSpace(ovelseName))
        {
            throw new ArgumentException("Use --ovelse or --ovelse-id to select an exercise.");
        }

        values.TryGetValue("km-nm-klasse", out var kmNmClass);
        if (string.IsNullOrWhiteSpace(kmNmClass))
        {
            values.TryGetValue("klasse", out kmNmClass);
        }

        if (!allClasses && string.IsNullOrWhiteSpace(kmNmClass))
        {
            throw new ArgumentException("Use --klasse, --km-nm-klasse, or --all-classes.");
        }

        if (allClasses && !string.IsNullOrWhiteSpace(kmNmClass))
        {
            throw new ArgumentException("Do not use --klasse together with --all-classes.");
        }

        values.TryGetValue("sius-group", out var siusGroupOverride);
        var normalizedGroupOverride = string.IsNullOrWhiteSpace(siusGroupOverride)
            ? null
            : siusGroupOverride.Trim();
        if (allClasses && normalizedGroupOverride is not null)
        {
            throw new ArgumentException("Do not use --sius-group together with --all-classes.");
        }

        values.TryGetValue("shooter-groups-template", out var shooterGroupsTemplatePath);
        if (string.IsNullOrWhiteSpace(shooterGroupsTemplatePath))
        {
            values.TryGetValue("shooter-groups", out shooterGroupsTemplatePath);
        }

        if (!string.IsNullOrWhiteSpace(shooterGroupsTemplatePath) && !File.Exists(shooterGroupsTemplatePath))
        {
            throw new ArgumentException($"Shooter groups template file does not exist: {shooterGroupsTemplatePath}");
        }

        values.TryGetValue("output", out var outputPath);
        values.TryGetValue("output-dir", out var outputDirectory);
        var copyToClipboard = flags.Contains("clipboard") || flags.Contains("copy-to-clipboard");
        if (allClasses)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("Use --output-dir with --all-classes.");
            }

            if (!string.IsNullOrWhiteSpace(outputPath) || copyToClipboard)
            {
                throw new ArgumentException("--all-classes writes files only; use --output-dir instead of --output or --clipboard.");
            }
        }
        else if (string.IsNullOrWhiteSpace(outputPath) && !copyToClipboard)
        {
            throw new ArgumentException("Use --output, --clipboard, or both.");
        }

        var encodingName = values.TryGetValue("encoding", out var encoding)
            ? CsvEncoding.NormalizeName(encoding)
            : CsvEncoding.Utf8Bom;

        return new AppOptions(
            databasePath,
            stevneId,
            stevneIds,
            eventDate,
            string.IsNullOrWhiteSpace(eventName) ? null : eventName,
            ovelseId,
            string.IsNullOrWhiteSpace(ovelseName) ? null : ovelseName,
            string.IsNullOrWhiteSpace(kmNmClass) ? null : kmNmClass.Trim(),
            normalizedGroupOverride,
            string.IsNullOrWhiteSpace(shooterGroupsTemplatePath) ? null : shooterGroupsTemplatePath,
            string.IsNullOrWhiteSpace(outputDirectory) ? null : outputDirectory,
            string.IsNullOrWhiteSpace(outputPath) ? null : outputPath,
            copyToClipboard,
            encodingName,
            flags.Contains("include-club-team"),
            allClasses,
            Wizard: false);
    }

    private static string Require(IReadOnlyDictionary<string, string> values, string name)
    {
        if (!values.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Missing required option '--{name}'.");
        }

        return value;
    }

    private static int? ParseNullableInt(IReadOnlyDictionary<string, string> values, string name)
    {
        if (!values.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new ArgumentException($"Option '--{name}' must be an integer.");
        }

        return parsed;
    }

    private static DateOnly? ParseNullableDate(IReadOnlyDictionary<string, string> values, string name)
    {
        if (!values.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            throw new ArgumentException($"Option '--{name}' must use yyyy-MM-dd.");
        }

        return parsed;
    }

    private static IReadOnlyList<int> ParseIdList(string value, string name)
    {
        var ids = new List<int>();
        foreach (var item in value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var range = item.Split('-', 2, StringSplitOptions.TrimEntries);
            if (range.Length == 2)
            {
                if (!int.TryParse(range[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var from) ||
                    !int.TryParse(range[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var to) ||
                    from > to)
                {
                    throw new ArgumentException($"Option '--{name}' has invalid id range '{item}'.");
                }

                ids.AddRange(Enumerable.Range(from, to - from + 1));
                continue;
            }

            if (!int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                throw new ArgumentException($"Option '--{name}' must contain comma-separated integer ids or ranges.");
            }

            ids.Add(id);
        }

        return ids.Distinct().ToList();
    }
}

public sealed record StevneInfo(int Id, string Name, string Date, int ArrangementId);

public sealed record OvelseInfo(int Id, string Name, string ShortName, int HovedOvelseId);

public sealed record OvelseSummary(int Id, string Name, string ShortName, int HovedOvelseId, int StarterCount);

public sealed record KmNmClassSummary(string Name, int StarterCount, string Relays);

public sealed record ExportResult(
    StevneInfo Stevne,
    OvelseInfo Ovelse,
    string KmNmClass,
    string? SiusGroupOverride,
    string? ShooterGroupsTemplatePath,
    int StarterCount,
    string? OutputPath,
    bool CopiedToClipboard,
    IReadOnlyList<string> Warnings);

public sealed record BulkExportFileResult(
    StevneInfo Stevne,
    OvelseInfo Ovelse,
    string KmNmClass,
    int StarterCount,
    string OutputPath,
    IReadOnlyList<string> Warnings);

public sealed record BulkExportResult(
    string OutputDirectory,
    string? ShooterGroupsTemplatePath,
    IReadOnlyList<BulkExportFileResult> Files);

public sealed record InrxStarter(
    int ResultatId,
    int Standplass,
    string SkivenrFra,
    string SkivenrTil,
    int? Relay,
    string RelayDate,
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
    string StevneName);

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
