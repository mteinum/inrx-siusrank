namespace InrxToSiusRank;

public static class EffectiveKmNmClass
{
    public static string Resolve(InrxStarter starter, OvelseInfo? ovelse = null)
    {
        var configuredClass = starter.KmNmClass.Trim();
        if (!IsMissing(configuredClass))
        {
            if (GroupNormalizer.Normalize(configuredClass).Equals("Apen", StringComparison.OrdinalIgnoreCase))
            {
                return "Apen";
            }

            return configuredClass;
        }

        if (ovelse is not null &&
            !IsMissing(starter.InrxClass) &&
            ApprobertPistolEventCodes.IsSupportedClass(ovelse, starter.InrxClass))
        {
            return GroupNormalizer.Normalize(starter.InrxClass);
        }

        if (ovelse is not null && IsOpenCombinedExercise(ovelse))
        {
            return "Apen";
        }

        return ResolveFromGender(starter.Gender) ?? configuredClass;
    }

    public static int SortKey(string kmNmClass)
    {
        var group = GroupNormalizer.Normalize(kmNmClass);
        return group switch
        {
            "Jrk" => 30,
            "Jrm" => 40,
            "Jr" or "Jr-NM" => 50,
            "Kvinner" => 60,
            "Menn" => 70,
            "Apen" => 80,
            "V55" => 90,
            "V65" => 100,
            "V73" => 110,
            "SH1" => 140,
            "SH2" => 150,
            _ => 999
        };
    }

    private static string? ResolveFromGender(string gender)
    {
        return gender.Trim().ToUpperInvariant() switch
        {
            "M" or "MALE" or "MANN" => "M",
            "F" or "K" or "FEMALE" or "KVINNE" => "K",
            _ => null
        };
    }

    private static bool IsOpenCombinedExercise(OvelseInfo ovelse)
    {
        if (ovelse.Id is 6 or 8 or 11 or 18)
        {
            return true;
        }

        return ovelse.Name.Contains("Hurtig Grov", StringComparison.OrdinalIgnoreCase) ||
               ovelse.Name.Contains("Grovpistol", StringComparison.OrdinalIgnoreCase) ||
               ovelse.Name.Contains("Silhuett", StringComparison.OrdinalIgnoreCase) ||
               ovelse.Name.Contains("Fripistol", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMissing(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Equals("-", StringComparison.Ordinal);
}
