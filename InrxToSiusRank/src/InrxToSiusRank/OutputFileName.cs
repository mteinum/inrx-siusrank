namespace InrxToSiusRank;

public static class OutputFileName
{
    public static string ForImport(StevneInfo stevne, OvelseInfo ovelse, string kmNmClass)
    {
        var date = stevne.Date.Length >= 10
            ? stevne.Date[..10].Replace("-", string.Empty, StringComparison.Ordinal)
            : "event";
        var classSuffix = ClassSuffix(kmNmClass);
        var exercise = ExerciseSuffix(ovelse, classSuffix);
        return $"{SanitizeFilePart(date)}_{SanitizeFilePart(exercise)}_{SanitizeFilePart(classSuffix)}.csv";
    }

    public static string ForImport(StevneInfo stevne, OvelseSummary ovelse, KmNmClassSummary kmNmClass)
    {
        var ovelseInfo = new OvelseInfo(ovelse.Id, ovelse.Name, ovelse.ShortName, ovelse.HovedOvelseId);
        return ForImport(stevne, ovelseInfo, kmNmClass.Name);
    }

    private static string ClassSuffix(string value)
    {
        var trimmed = value.Trim();
        return GroupNormalizer.Normalize(trimmed).Equals("Apen", StringComparison.OrdinalIgnoreCase)
            ? "Apen"
            : trimmed;
    }

    private static string ExerciseSuffix(OvelseInfo ovelse, string classSuffix)
    {
        return ovelse.Id switch
        {
            18 => "FP",
            11 => SilhouetteSuffix(classSuffix),
            10 => "STP",
            9 => "SPM",
            8 => "CFP",
            7 => "SPRF",
            6 => "CFPRF",
            _ => ExerciseSuffixFromName(ovelse, classSuffix)
        };
    }

    private static string ExerciseSuffixFromName(OvelseInfo ovelse, string classSuffix)
    {
        if (ovelse.Name.Contains("Hurtig", StringComparison.OrdinalIgnoreCase))
        {
            if (ovelse.Name.Contains("Fin", StringComparison.OrdinalIgnoreCase))
            {
                return "SPRF";
            }

            if (ovelse.Name.Contains("Grov", StringComparison.OrdinalIgnoreCase))
            {
                return "CFPRF";
            }
        }

        if (ovelse.Name.Contains("Fripistol", StringComparison.OrdinalIgnoreCase))
        {
            return "FP";
        }

        if (ovelse.Name.Contains("Silhuett", StringComparison.OrdinalIgnoreCase))
        {
            return SilhouetteSuffix(classSuffix);
        }

        if (ovelse.Name.Contains("Standard", StringComparison.OrdinalIgnoreCase))
        {
            return "STP";
        }

        if (ovelse.Name.Contains("Finpistol", StringComparison.OrdinalIgnoreCase))
        {
            return "SPM";
        }

        if (ovelse.Name.Contains("Grovpistol", StringComparison.OrdinalIgnoreCase))
        {
            return "CFP";
        }

        return string.IsNullOrWhiteSpace(ovelse.ShortName) ? ovelse.Name : ovelse.ShortName;
    }

    private static string SilhouetteSuffix(string classSuffix)
    {
        return classSuffix.Equals("Apen", StringComparison.OrdinalIgnoreCase) ? "RFP" : "RFP_NF";
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
