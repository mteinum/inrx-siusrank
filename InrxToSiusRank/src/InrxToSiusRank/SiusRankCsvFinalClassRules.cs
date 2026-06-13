using System.Text;

namespace InrxToSiusRank;

public static class SiusRankCsvFinalClassRules
{
    private static readonly char[] ClassSeparators = [',', ';'];

    public static IReadOnlyList<string> ParseText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var lines = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Any(IsRule))
        {
            return lines
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return ParseClasses(value);
    }

    public static string FormatText(IReadOnlyList<string>? entries)
    {
        if (entries is null || entries.Count == 0)
        {
            return string.Empty;
        }

        return entries.Any(IsRule)
            ? string.Join(Environment.NewLine, entries)
            : string.Join(",", entries);
    }

    public static IReadOnlySet<string> ResolveFor(OvelseInfo ovelse, IReadOnlyList<string>? entries)
    {
        if (entries is null || entries.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var rules = entries
            .Select(ParseRule)
            .Where(rule => rule is not null)
            .Select(rule => rule!.Value)
            .ToList();
        IEnumerable<string> classNames = rules.Count == 0
            ? entries
            : rules
                .Where(rule => MatchesOvelse(ovelse, rule.Key))
                .SelectMany(rule => ParseClasses(rule.Classes));

        return BuildClassSet(classNames);
    }

    public static bool HasRules(IReadOnlyList<string>? entries) =>
        entries is not null && entries.Any(IsRule);

    private static HashSet<string> BuildClassSet(IEnumerable<string> classNames) =>
        classNames
            .SelectMany(FinalClassAliases)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string> ParseClasses(string value) =>
        value
            .Split(ClassSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IEnumerable<string> FinalClassAliases(string className)
    {
        var trimmed = className.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            yield break;
        }

        yield return trimmed;
        yield return GroupNormalizer.Normalize(trimmed);
    }

    private static bool IsRule(string value) =>
        FindRuleSeparator(value) >= 0;

    private static (string Key, string Classes)? ParseRule(string value)
    {
        var separator = FindRuleSeparator(value);
        if (separator < 0)
        {
            return null;
        }

        var key = value[..separator].Trim();
        var classes = value[(separator + 1)..].Trim();
        return string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(classes)
            ? null
            : (key, classes);
    }

    private static int FindRuleSeparator(string value)
    {
        var colon = value.IndexOf(':', StringComparison.Ordinal);
        var equals = value.IndexOf('=', StringComparison.Ordinal);
        return (colon, equals) switch
        {
            (< 0, < 0) => -1,
            (< 0, _) => equals,
            (_, < 0) => colon,
            _ => Math.Min(colon, equals)
        };
    }

    private static bool MatchesOvelse(OvelseInfo ovelse, string key)
    {
        var normalizedKey = NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return false;
        }

        return OvelseKeys(ovelse).Contains(normalizedKey, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> OvelseKeys(OvelseInfo ovelse)
    {
        yield return NormalizeKey(ovelse.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        yield return NormalizeKey(ovelse.Name);
        yield return NormalizeKey(ovelse.ShortName);
    }

    private static string NormalizeKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}
