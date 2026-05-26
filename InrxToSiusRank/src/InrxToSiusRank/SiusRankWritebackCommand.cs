using System.Globalization;

namespace InrxToSiusRank;

public static class SiusRankWritebackCommand
{
    public const string Name = "writeback-siusrank";
    public const string Alias = "import-siusrank-results";

    public static bool IsCommand(IReadOnlyList<string> args) =>
        args.Count > 0 &&
        (args[0].Equals(Name, StringComparison.OrdinalIgnoreCase) ||
         args[0].Equals(Alias, StringComparison.OrdinalIgnoreCase));

    public static SiusRankWritebackOptions Parse(IReadOnlyList<string> args)
    {
        string? settingsPath = null;
        string? databasePath = null;
        string? exportsDirectory = null;
        string? stevneId = null;
        string? stevneIds = null;
        string? bibMapPath = null;
        string? eventFilters = null;
        var apply = false;

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--apply":
                    apply = true;
                    break;
                case "--settings":
                    settingsPath = ReadValue(args, ref index, arg);
                    break;
                case "--db":
                    databasePath = ReadValue(args, ref index, arg);
                    break;
                case "--exports":
                case "--exports-dir":
                    exportsDirectory = ReadValue(args, ref index, arg);
                    break;
                case "--stevne-id":
                    stevneId = ReadValue(args, ref index, arg);
                    break;
                case "--stevne-ids":
                    stevneIds = ReadValue(args, ref index, arg);
                    break;
                case "--bib-map":
                    bibMapPath = ReadValue(args, ref index, arg);
                    break;
                case "--event":
                case "--events":
                    eventFilters = ReadValue(args, ref index, arg);
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

        if (string.IsNullOrWhiteSpace(exportsDirectory))
        {
            throw new ArgumentException("Use --exports to choose the SIUS Rank Exports directory.");
        }

        var resolvedExportsDirectory = Path.GetFullPath(exportsDirectory);
        if (!Directory.Exists(resolvedExportsDirectory))
        {
            throw new ArgumentException($"SIUS Rank Exports directory does not exist: {resolvedExportsDirectory}");
        }

        if (!string.IsNullOrWhiteSpace(stevneId) && !string.IsNullOrWhiteSpace(stevneIds))
        {
            throw new ArgumentException("Use either --stevne-id or --stevne-ids, not both.");
        }

        var selectedStevneIds = !string.IsNullOrWhiteSpace(stevneIds)
            ? ParseIdList(stevneIds, "stevne-ids")
            : !string.IsNullOrWhiteSpace(stevneId)
                ? ParseIdList(stevneId, "stevne-id")
                : [];
        if (selectedStevneIds.Count == 0)
        {
            throw new ArgumentException("Use --stevne-id or --stevne-ids to select inrX event(s).");
        }

        var resolvedBibMapPath = ResolveBibMapPath(
            bibMapPath,
            resolvedExportsDirectory,
            requireExplicitPath: true);
        var parsedEventFilters = ParseEventFilters(eventFilters);

        return new SiusRankWritebackOptions(
            resolvedDatabasePath,
            resolvedExportsDirectory,
            selectedStevneIds,
            resolvedBibMapPath,
            parsedEventFilters,
            apply);
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

    public static string? ResolveBibMapPath(
        string? bibMapPath,
        string exportsDirectory,
        bool requireExplicitPath)
    {
        if (!string.IsNullOrWhiteSpace(bibMapPath))
        {
            var resolved = Path.GetFullPath(bibMapPath);
            if (File.Exists(resolved))
            {
                return resolved;
            }

            if (requireExplicitPath)
            {
                throw new ArgumentException($"bib-map.csv file does not exist: {resolved}");
            }
        }

        foreach (var candidate in DefaultBibMapCandidates(exportsDirectory))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> DefaultBibMapCandidates(string exportsDirectory)
    {
        yield return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "siusrank-import", ChampionshipStartNumbers.BibMapFileName));

        var exports = new DirectoryInfo(exportsDirectory);
        if (exports.Parent?.Parent is not null)
        {
            yield return Path.Combine(exports.Parent.Parent.FullName, "siusrank-import", ChampionshipStartNumbers.BibMapFileName);
        }

        if (exports.Parent is not null)
        {
            yield return Path.Combine(exports.Parent.FullName, "siusrank-import", ChampionshipStartNumbers.BibMapFileName);
        }
    }

    private static IReadOnlySet<string> ParseEventFilters(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return value
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(SiusRankEventDiscipline.NormalizeFilter)
            .Where(item => item.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
