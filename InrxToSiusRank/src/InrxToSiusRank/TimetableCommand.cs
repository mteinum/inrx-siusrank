using System.Globalization;

namespace InrxToSiusRank;

public static class TimetableCommand
{
    public const string Name = "show-timetable";
    public const string Alias = "timetable";
    private static readonly int[] DefaultNmStevneIds = [405, 406, 407, 408, 409, 410, 411];

    public static bool IsCommand(IReadOnlyList<string> args) =>
        args.Count > 0 &&
        (args[0].Equals(Name, StringComparison.OrdinalIgnoreCase) ||
         args[0].Equals(Alias, StringComparison.OrdinalIgnoreCase));

    public static TimetableOptions Parse(IReadOnlyList<string> args)
    {
        string? settingsPath = null;
        string? databasePath = null;
        string? stevneId = null;
        string? stevneIds = null;

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--settings":
                    settingsPath = ReadValue(args, ref index, arg);
                    break;
                case "--db":
                    databasePath = ReadValue(args, ref index, arg);
                    break;
                case "--stevne-id":
                    stevneId = ReadValue(args, ref index, arg);
                    break;
                case "--stevne-ids":
                    stevneIds = ReadValue(args, ref index, arg);
                    break;
                default:
                    throw new ArgumentException($"Unknown option for {Name}: {arg}");
            }
        }

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

        if (!string.IsNullOrWhiteSpace(stevneId) && !string.IsNullOrWhiteSpace(stevneIds))
        {
            throw new ArgumentException("Use either --stevne-id or --stevne-ids, not both.");
        }

        var selectedStevneIds = !string.IsNullOrWhiteSpace(stevneIds)
            ? ParseIdList(stevneIds, "stevne-ids")
            : !string.IsNullOrWhiteSpace(stevneId)
                ? ParseIdList(stevneId, "stevne-id")
                : DefaultNmStevneIds;

        return new TimetableOptions(resolvedDatabasePath, selectedStevneIds);
    }

    private static string ReadValue(IReadOnlyList<string> args, ref int index, string optionName)
    {
        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Option '{optionName}' requires a value.");
        }

        index++;
        return args[index];
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
