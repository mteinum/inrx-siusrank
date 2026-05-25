namespace InrxToSiusRank;

public static class ApprobertPistolEventCodes
{
    private static readonly IReadOnlyDictionary<string, string[]> SupportedClassesByBaseCode =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["2A"] = ["A", "B", "C", "D", "U16", "U14", "SH1"],
            ["2C"] = ["V55", "V65", "V73"],
            ["2B"] = ["A", "B", "C", "D", "U16", "U14"],
            ["4"] = ["A", "B", "C", "D", "U16", "U14"],
            ["5"] = ["A", "B", "C", "D", "U16", "U14", "SH1"],
            ["6F"] = ["A", "B", "C", "D", "U16", "U14", "SH1", "SH-Apen"],
            ["6G"] = ["A", "B", "C", "D"],
            ["7F"] = ["A", "B", "C", "D", "U16", "U14", "SH1"],
            ["7G"] = ["A", "B", "C", "D"]
        };

    public static bool IsSupportedClass(OvelseInfo ovelse, string className) =>
        TryResolveEventCode(ovelse, className, out _);

    public static bool TryResolveEventCode(OvelseInfo ovelse, string className, out string eventCode)
    {
        eventCode = string.Empty;
        var classCode = NormalizeClassCode(className);
        if (classCode is null)
        {
            return false;
        }

        var baseCode = ResolveBaseCode(ovelse);
        if (baseCode is null ||
            !SupportedClassesByBaseCode.TryGetValue(baseCode, out var supportedClasses) ||
            !supportedClasses.Contains(classCode, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        eventCode = $"{baseCode}_{classCode}";
        return true;
    }

    private static string? ResolveBaseCode(OvelseInfo ovelse)
    {
        var normalizedName = Fold(ovelse.Name);
        if (normalizedName.Contains("STOTTE", StringComparison.Ordinal) ||
            normalizedName.Contains("SUPPORT", StringComparison.Ordinal))
        {
            return "2C";
        }

        if (normalizedName.Contains("COLT", StringComparison.Ordinal))
        {
            return "2B";
        }

        return ovelse.Id switch
        {
            18 => "2A",
            11 => "4",
            10 => "5",
            9 => "6F",
            8 => "6G",
            7 => "7F",
            6 => "7G",
            _ => ResolveBaseCodeFromName(normalizedName)
        };
    }

    private static string? ResolveBaseCodeFromName(string normalizedName)
    {
        if (normalizedName.Contains("HURTIG", StringComparison.Ordinal))
        {
            if (normalizedName.Contains("FIN", StringComparison.Ordinal))
            {
                return "7F";
            }

            if (normalizedName.Contains("GROV", StringComparison.Ordinal))
            {
                return "7G";
            }
        }

        if (normalizedName.Contains("FRIPISTOL", StringComparison.Ordinal))
        {
            return normalizedName.Contains("COLT", StringComparison.Ordinal) ? "2B" : "2A";
        }

        if (normalizedName.Contains("SILHUETT", StringComparison.Ordinal))
        {
            return "4";
        }

        if (normalizedName.Contains("STANDARD", StringComparison.Ordinal))
        {
            return "5";
        }

        if (normalizedName.Contains("FINPISTOL", StringComparison.Ordinal))
        {
            return "6F";
        }

        if (normalizedName.Contains("GROVPISTOL", StringComparison.Ordinal))
        {
            return "6G";
        }

        return null;
    }

    private static string? NormalizeClassCode(string className)
    {
        var normalizedGroup = GroupNormalizer.Normalize(className);
        if (normalizedGroup.Equals("Apen", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var folded = Fold(className);
        return folded switch
        {
            "A" => "A",
            "B" => "B",
            "C" => "C",
            "D" => "D",
            "U16" => "U16",
            "U14" => "U14",
            "V55" => "V55",
            "V65" => "V65",
            "V73" => "V73",
            "SH1" => "SH1",
            "SHA" or "SHAPEN" or "SHOPEN" => "SH-Apen",
            _ => null
        };
    }

    private static string Fold(string value) =>
        value
            .Trim()
            .Replace("å", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("ä", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("ö", "o", StringComparison.OrdinalIgnoreCase)
            .Replace("ø", "o", StringComparison.OrdinalIgnoreCase)
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
}
