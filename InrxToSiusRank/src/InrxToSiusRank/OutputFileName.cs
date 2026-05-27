namespace InrxToSiusRank;

public static class OutputFileName
{
    public static string ForImport(StevneInfo stevne, OvelseInfo ovelse, string kmNmClass)
    {
        var date = stevne.Date.Length >= 10
            ? stevne.Date[..10].Replace("-", string.Empty, StringComparison.Ordinal)
            : "event";
        var eventCode = EventFilterForImport(ovelse, kmNmClass);
        return $"{SanitizeFilePart(date)}_{SanitizeFilePart(eventCode)}.csv";
    }

    public static string EventFilterForImport(OvelseInfo ovelse, string kmNmClass)
    {
        var classSuffix = ClassSuffix(kmNmClass);
        return ApprobertPistolEventCodes.TryResolveEventCode(ovelse, kmNmClass, out var approbertEventCode)
            ? approbertEventCode
            : $"{ExerciseSuffix(ovelse, classSuffix)}_{classSuffix}";
    }

    public static string ForImport(StevneInfo stevne, OvelseSummary ovelse, KmNmClassSummary kmNmClass)
    {
        var ovelseInfo = new OvelseInfo(ovelse.Id, ovelse.Name, ovelse.ShortName, ovelse.HovedOvelseId);
        return ForImport(stevne, ovelseInfo, kmNmClass.Name);
    }

    private static string ClassSuffix(string value)
    {
        var trimmed = value.Trim();
        return GroupNormalizer.Normalize(trimmed) switch
        {
            var group when group.Equals("Apen", StringComparison.OrdinalIgnoreCase) => "Apen",
            var group when group.Equals("Menn", StringComparison.OrdinalIgnoreCase) => "M",
            var group when group.Equals("Kvinner", StringComparison.OrdinalIgnoreCase) => "K",
            var group when group.Equals("Jrm", StringComparison.OrdinalIgnoreCase) => "Jm",
            var group when group.Equals("Jrk", StringComparison.OrdinalIgnoreCase) => "Jk",
            _ => trimmed
        };
    }

    private static string ExerciseSuffix(OvelseInfo ovelse, string classSuffix)
    {
        return ovelse.Id switch
        {
            18 => "Fri",
            11 => "Silhuett",
            10 => "Standard",
            9 => "Fin",
            8 => "Grov",
            7 => "HurtigFin",
            6 => "HurtigGrov",
            _ => ExerciseSuffixFromName(ovelse, classSuffix)
        };
    }

    private static string ExerciseSuffixFromName(OvelseInfo ovelse, string classSuffix)
    {
        if (ovelse.Name.Contains("Hurtig", StringComparison.OrdinalIgnoreCase))
        {
            if (ovelse.Name.Contains("Fin", StringComparison.OrdinalIgnoreCase))
            {
                return "HurtigFin";
            }

            if (ovelse.Name.Contains("Grov", StringComparison.OrdinalIgnoreCase))
            {
                return "HurtigGrov";
            }
        }

        if (ovelse.Name.Contains("Fripistol", StringComparison.OrdinalIgnoreCase))
        {
            return "Fri";
        }

        if (ovelse.Name.Contains("Silhuett", StringComparison.OrdinalIgnoreCase))
        {
            return "Silhuett";
        }

        if (ovelse.Name.Contains("Standard", StringComparison.OrdinalIgnoreCase))
        {
            return "Standard";
        }

        if (ovelse.Name.Contains("Finpistol", StringComparison.OrdinalIgnoreCase))
        {
            return "Fin";
        }

        if (ovelse.Name.Contains("Grovpistol", StringComparison.OrdinalIgnoreCase))
        {
            return "Grov";
        }

        return string.IsNullOrWhiteSpace(ovelse.ShortName) ? ovelse.Name : ovelse.ShortName;
    }

    private static string SanitizeFilePart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value
            .Trim()
            .Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '_' : ch)
            .ToArray();

        return new string(chars).Trim('_');
    }
}
