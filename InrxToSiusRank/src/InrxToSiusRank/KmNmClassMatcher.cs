namespace InrxToSiusRank;

public static class KmNmClassMatcher
{
    public static bool Matches(string actual, string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return true;
        }

        return NormalizeForCompare(actual) == NormalizeForCompare(requested);
    }

    private static string NormalizeForCompare(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Equals("Å", StringComparison.OrdinalIgnoreCase))
        {
            return "APEN";
        }

        var folded = trimmed
            .Replace("å", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("ä", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("ø", "o", StringComparison.OrdinalIgnoreCase)
            .Replace("ö", "o", StringComparison.OrdinalIgnoreCase)
            .ToUpperInvariant();

        return folded is "APEN" or "OPEN" ? "APEN" : folded;
    }
}
