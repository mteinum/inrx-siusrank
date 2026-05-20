namespace InrxToSiusRank;

public static class GroupNormalizer
{
    public static string Normalize(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "Apen";
        }

        if (trimmed.Equals("Å", StringComparison.OrdinalIgnoreCase))
        {
            return "Apen";
        }

        var folded = trimmed
            .Replace("å", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("ä", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("ö", "o", StringComparison.OrdinalIgnoreCase)
            .Replace("ø", "o", StringComparison.OrdinalIgnoreCase)
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        return folded switch
        {
            "APEN" or "OPEN" => "Apen",
            "M" or "MENN" => "Menn",
            "K" or "KVINNER" => "Kvinner",
            "JM" or "JRM" or "JUNIORMENN" => "Jrm",
            "JK" or "JRK" or "JUNIORKVINNER" => "Jrk",
            "J" or "JR" or "JUNIOR" => "Jr",
            _ when folded.StartsWith("SH1", StringComparison.Ordinal) => "SH1",
            _ when folded.StartsWith("SH2", StringComparison.Ordinal) => "SH2",
            _ => trimmed
        };
    }
}
