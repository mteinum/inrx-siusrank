using System.Globalization;

namespace InrxToSiusRank;

public static class ExportSscUsersCommand
{
    public const string Name = "export-ssc-users";
    private const string DefaultOrganizationName = "Legacy";
    private const string DefaultOrganizationId = "f95a2bc3-79bd-4c24-98b6-4e17f99bbfaf";

    public static bool IsCommand(IReadOnlyList<string> args) =>
        args.Count > 0 && args[0].Equals(Name, StringComparison.OrdinalIgnoreCase);

    public static ExportSscUsersOptions Parse(IReadOnlyList<string> args)
    {
        string? settingsPath = null;
        string? databasePath = null;
        string? stevneId = null;
        string? stevneIds = null;
        string? bibMapPath = null;
        string? outputPath = null;
        var organizationName = DefaultOrganizationName;
        var organizationId = DefaultOrganizationId;
        var encodingName = CsvEncoding.Utf8Bom;

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--settings":
                    settingsPath = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                case "--db":
                    databasePath = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                case "--stevne-id":
                    stevneId = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                case "--stevne-ids":
                    stevneIds = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                case "--bib-map":
                    bibMapPath = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                case "--output":
                    outputPath = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                case "--organization-name":
                    organizationName = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                case "--organization-id":
                    organizationId = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                case "--encoding":
                    encodingName = CsvEncoding.NormalizeName(SscCommandParsing.ReadValue(args, ref index, arg));
                    break;
                default:
                    throw new ArgumentException($"Unknown option for {Name}: {arg}");
            }
        }

        return new ExportSscUsersOptions(
            SscCommandParsing.ResolveDatabasePath(settingsPath, databasePath),
            SscCommandParsing.ResolveStevneIds(stevneId, stevneIds),
            string.IsNullOrWhiteSpace(bibMapPath) ? null : bibMapPath,
            string.IsNullOrWhiteSpace(outputPath) ? null : outputPath,
            organizationName,
            organizationId,
            encodingName);
    }
}

public static class ValidateSscCommand
{
    public const string Name = "validate-ssc";

    public static bool IsCommand(IReadOnlyList<string> args) =>
        args.Count > 0 && args[0].Equals(Name, StringComparison.OrdinalIgnoreCase);

    public static ValidateSscOptions Parse(IReadOnlyList<string> args)
    {
        string? settingsPath = null;
        string? databasePath = null;
        string? stevneId = null;
        string? stevneIds = null;
        string? bibMapPath = null;
        string? usersCsvPath = null;

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--settings":
                    settingsPath = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                case "--db":
                    databasePath = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                case "--stevne-id":
                    stevneId = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                case "--stevne-ids":
                    stevneIds = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                case "--bib-map":
                    bibMapPath = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                case "--users-csv":
                    usersCsvPath = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                default:
                    throw new ArgumentException($"Unknown option for {Name}: {arg}");
            }
        }

        if (string.IsNullOrWhiteSpace(usersCsvPath))
        {
            throw new ArgumentException("Use --users-csv to select the SSC users CSV.");
        }

        var resolvedUsersCsvPath = Path.GetFullPath(usersCsvPath);
        if (!File.Exists(resolvedUsersCsvPath))
        {
            throw new ArgumentException($"SSC users CSV does not exist: {resolvedUsersCsvPath}");
        }

        return new ValidateSscOptions(
            SscCommandParsing.ResolveDatabasePath(settingsPath, databasePath),
            SscCommandParsing.ResolveStevneIds(stevneId, stevneIds),
            string.IsNullOrWhiteSpace(bibMapPath) ? null : bibMapPath,
            resolvedUsersCsvPath);
    }
}

public static class ExportSscLanesCommand
{
    public const string Name = "export-ssc-lanes";

    public static bool IsCommand(IReadOnlyList<string> args) =>
        args.Count > 0 && args[0].Equals(Name, StringComparison.OrdinalIgnoreCase);

    public static ExportSscLanesOptions Parse(IReadOnlyList<string> args)
    {
        string? settingsPath = null;
        string? databasePath = null;
        string? stevneId = null;
        string? startlag = null;
        string? bibMapPath = null;
        string? outputDirectory = null;
        var laneCount = 40;

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--settings":
                    settingsPath = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                case "--db":
                    databasePath = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                case "--stevne-id":
                    stevneId = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                case "--startlag":
                    startlag = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                case "--bib-map":
                    bibMapPath = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                case "--output-dir":
                    outputDirectory = SscCommandParsing.ReadValue(args, ref index, arg);
                    break;
                case "--lane-count":
                    laneCount = SscCommandParsing.ParseInt(SscCommandParsing.ReadValue(args, ref index, arg), "lane-count");
                    break;
                default:
                    throw new ArgumentException($"Unknown option for {Name}: {arg}");
            }
        }

        if (string.IsNullOrWhiteSpace(stevneId))
        {
            throw new ArgumentException("Use --stevne-id to select one inrX event.");
        }

        if (string.IsNullOrWhiteSpace(startlag))
        {
            throw new ArgumentException("Use --startlag to select the active startlag date/time.");
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Use --output-dir to choose where SSC lane files should be written.");
        }

        return new ExportSscLanesOptions(
            SscCommandParsing.ResolveDatabasePath(settingsPath, databasePath),
            SscCommandParsing.ParseInt(stevneId, "stevne-id"),
            startlag,
            string.IsNullOrWhiteSpace(bibMapPath) ? null : bibMapPath,
            outputDirectory,
            laneCount);
    }
}

public static class SscReporter
{
    public static void PrintUsers(SscUsersExportResult result)
    {
        var status = result.Written
            ? "SSC users CSV created."
            : string.IsNullOrWhiteSpace(result.OutputPath)
                ? "SSC users CSV dry-run."
                : "SSC users CSV not written because validation errors were found.";
        Console.WriteLine(status);
        if (!string.IsNullOrWhiteSpace(result.OutputPath))
        {
            Console.WriteLine($"Output: {result.OutputPath}");
        }

        if (!string.IsNullOrWhiteSpace(result.BibMapPath))
        {
            Console.WriteLine($"bib-map.csv: {result.BibMapPath}");
        }

        Console.WriteLine($"Users: {result.UserCount}");
        PrintMessages(result.Messages);
    }

    public static void PrintValidation(SscValidationResult result)
    {
        Console.WriteLine("SSC validation complete.");
        Console.WriteLine($"Starters: {result.StarterCount}");
        Console.WriteLine($"Users: {result.UserCount}");
        PrintMessages(result.Messages);
    }

    public static void PrintLanes(SscLanesExportResult result)
    {
        Console.WriteLine("SSC lane payload files created.");
        Console.WriteLine($"Output directory: {result.OutputDirectory}");
        Console.WriteLine($"Reset: {result.ResetPath}");
        Console.WriteLine($"Active lanes: {result.ActiveLanesPath}");
        Console.WriteLine($"Lane count: {result.LaneCount}");
        Console.WriteLine($"Active lanes: {result.ActiveLaneCount}");
        PrintMessages(result.Messages);
    }

    private static void PrintMessages(IReadOnlyList<SscValidationMessage> messages)
    {
        foreach (var message in messages)
        {
            Console.WriteLine($"{SscValidationMessageFormatter.Prefix(message)}: {message.Message}");
        }
    }
}

internal static class SscCommandParsing
{
    public static string ReadValue(IReadOnlyList<string> args, ref int index, string optionName)
    {
        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Option '{optionName}' requires a value.");
        }

        index++;
        return args[index];
    }

    public static string ResolveDatabasePath(string? settingsPath, string? databasePath)
    {
        var settings = AppSettings.Load(settingsPath);
        var resolvedDatabasePath = !string.IsNullOrWhiteSpace(databasePath)
            ? databasePath
            : settings.ResolveDatabasePath();
        if (!File.Exists(resolvedDatabasePath))
        {
            throw new ArgumentException(
                $"Database file does not exist: {resolvedDatabasePath}. " +
                "Use --db or configure Paths:Inrx/Paths:Database in appsettings.json.");
        }

        return resolvedDatabasePath;
    }

    public static IReadOnlyList<int> ResolveStevneIds(string? stevneId, string? stevneIds)
    {
        if (!string.IsNullOrWhiteSpace(stevneId) && !string.IsNullOrWhiteSpace(stevneIds))
        {
            throw new ArgumentException("Use either --stevne-id or --stevne-ids, not both.");
        }

        var selected = !string.IsNullOrWhiteSpace(stevneIds)
            ? ParseIdList(stevneIds, "stevne-ids")
            : !string.IsNullOrWhiteSpace(stevneId)
                ? ParseIdList(stevneId, "stevne-id")
                : [];
        if (selected.Count == 0)
        {
            throw new ArgumentException("Use --stevne-id or --stevne-ids to select inrX event(s).");
        }

        return selected;
    }

    public static int ParseInt(string value, string name)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new ArgumentException($"Option '--{name}' must be an integer.");
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
