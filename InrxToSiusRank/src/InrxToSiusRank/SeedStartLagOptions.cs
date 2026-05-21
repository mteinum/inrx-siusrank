using System.Globalization;

namespace InrxToSiusRank;

public static class SeedStartLagCommand
{
    public const string Name = "seed-startlag";
    public const string DefaultRankingPeriodStart = "2025-12-31T23:00:00.000Z";
    public const string DefaultRankingPeriodEnd = "2026-12-31T22:59:59.999Z";

    public static bool IsCommand(IReadOnlyList<string> args) =>
        args.Count > 0 && args[0].Equals(Name, StringComparison.OrdinalIgnoreCase);

    public static SeedStartLagOptions Parse(IReadOnlyList<string> args)
    {
        string? settingsPath = null;
        string? databasePath = null;
        string? stevneId = null;
        string? stevneIds = null;
        string rankingPeriodStart = DefaultRankingPeriodStart;
        string rankingPeriodEnd = DefaultRankingPeriodEnd;
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
                case "--stevne-id":
                    stevneId = ReadValue(args, ref index, arg);
                    break;
                case "--stevne-ids":
                    stevneIds = ReadValue(args, ref index, arg);
                    break;
                case "--ranking-period-start":
                    rankingPeriodStart = ReadValue(args, ref index, arg);
                    break;
                case "--ranking-period-end":
                    rankingPeriodEnd = ReadValue(args, ref index, arg);
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
                : [];
        if (selectedStevneIds.Count == 0)
        {
            throw new ArgumentException("Use --stevne-id or --stevne-ids to select event(s).");
        }

        ValidateDateTimeOffset(rankingPeriodStart, "ranking-period-start");
        ValidateDateTimeOffset(rankingPeriodEnd, "ranking-period-end");

        return new SeedStartLagOptions(
            resolvedDatabasePath,
            selectedStevneIds,
            rankingPeriodStart,
            rankingPeriodEnd,
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

    private static void ValidateDateTimeOffset(string value, string name)
    {
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _))
        {
            throw new ArgumentException($"Option '--{name}' must be an ISO date/time value.");
        }
    }
}
