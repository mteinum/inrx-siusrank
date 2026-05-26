namespace InrxToSiusRank;

public static class SiusRankEventDiscipline
{
    private static readonly (string BaseCode, int OvelseDefId)[] ApprobertBaseCodeMap =
    [
        ("2A", 18),
        ("7G", 6),
        ("7F", 7),
        ("6G", 8),
        ("6F", 9),
        ("5", 10),
        ("4", 11)
    ];

    private static readonly string[] ApprobertClassSuffixes =
    [
        "A",
        "B",
        "C",
        "D",
        "U16",
        "U14",
        "V55",
        "V65",
        "V73",
        "SH1",
        "SHAPEN"
    ];

    public static int? ResolveOvelseDefId(string shortName, string eventCode)
    {
        var normalizedShortName = NormalizeFilter(shortName);
        var normalizedEventCode = NormalizeFilter(eventCode);

        var approbertId = ResolveApprobertOvelseDefId(normalizedShortName) ??
            ResolveApprobertOvelseDefId(normalizedEventCode);
        if (approbertId is not null)
        {
            return approbertId;
        }

        if (normalizedShortName.Contains("HURTIGFIN", StringComparison.Ordinal) ||
            normalizedEventCode.StartsWith("SPRF", StringComparison.Ordinal))
        {
            return 7;
        }

        if (normalizedShortName.Contains("HURTIGGROV", StringComparison.Ordinal) ||
            normalizedShortName.Contains("CFPRF", StringComparison.Ordinal) ||
            normalizedEventCode.StartsWith("CFPRF", StringComparison.Ordinal))
        {
            return 6;
        }

        if (normalizedShortName.Contains("STANDARD", StringComparison.Ordinal) ||
            normalizedEventCode.StartsWith("STP", StringComparison.Ordinal))
        {
            return 10;
        }

        if (normalizedShortName.Contains("SILHUETT", StringComparison.Ordinal))
        {
            return 11;
        }

        if (normalizedShortName.StartsWith("RFP", StringComparison.Ordinal) ||
            normalizedEventCode.StartsWith("RFP", StringComparison.Ordinal))
        {
            return 11;
        }

        if (normalizedShortName.StartsWith("FRI", StringComparison.Ordinal) ||
            normalizedShortName.Contains("FRIPISTOL", StringComparison.Ordinal))
        {
            return 18;
        }

        if (normalizedShortName.StartsWith("FIN", StringComparison.Ordinal) ||
            normalizedShortName.Contains("FINPISTOL", StringComparison.Ordinal))
        {
            return 9;
        }

        if (normalizedShortName.StartsWith("GROV", StringComparison.Ordinal) ||
            normalizedShortName.Contains("GROVPISTOL", StringComparison.Ordinal) ||
            normalizedEventCode.StartsWith("CFP", StringComparison.Ordinal))
        {
            return 8;
        }

        return null;
    }

    private static int? ResolveApprobertOvelseDefId(string normalizedCode)
    {
        foreach (var (baseCode, ovelseDefId) in ApprobertBaseCodeMap)
        {
            if (HasApprobertBaseCode(normalizedCode, baseCode))
            {
                return ovelseDefId;
            }
        }

        return null;
    }

    private static bool HasApprobertBaseCode(string normalizedCode, string baseCode)
    {
        if (!normalizedCode.StartsWith(baseCode, StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = normalizedCode[baseCode.Length..];
        return suffix.Length == 0 ||
            ApprobertClassSuffixes.Contains(suffix, StringComparer.Ordinal);
    }

    public static bool MatchesFilters(
        string shortName,
        string eventCode,
        IReadOnlySet<string> eventFilters)
    {
        if (eventFilters.Count == 0)
        {
            return true;
        }

        var normalizedShortName = NormalizeFilter(shortName);
        var normalizedEventCode = NormalizeFilter(eventCode);
        var normalizedBase = NormalizeFilter(BaseName(shortName));

        return eventFilters.Contains(normalizedShortName) ||
               eventFilters.Contains(normalizedEventCode) ||
               eventFilters.Contains(normalizedBase);
    }

    public static string NormalizeFilter(string value) =>
        new(value
            .Trim()
            .Where(ch => char.IsLetterOrDigit(ch))
            .Select(char.ToUpperInvariant)
            .ToArray());

    private static string BaseName(string shortName)
    {
        var index = shortName.IndexOf('_', StringComparison.Ordinal);
        return index <= 0
            ? shortName
            : shortName[..index];
    }
}
