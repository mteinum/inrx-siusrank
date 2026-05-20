namespace InrxToSiusRank;

public static class OutputFileName
{
    public static string ForImport(StevneInfo stevne, OvelseInfo ovelse, string kmNmClass)
    {
        var date = stevne.Date.Length >= 10
            ? stevne.Date[..10].Replace("-", string.Empty, StringComparison.Ordinal)
            : "event";
        var exercise = string.IsNullOrWhiteSpace(ovelse.ShortName) ? ovelse.Name : ovelse.ShortName;
        return $"{SanitizeFilePart(date)}_{SanitizeFilePart(exercise)}_{SanitizeFilePart(ClassSuffix(kmNmClass))}.csv";
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
