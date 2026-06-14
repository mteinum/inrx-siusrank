namespace InrxToSiusRank;

public static class SiusRankShooterGroupName
{
    public static string ForImport(string value)
    {
        var normalized = GroupNormalizer.Normalize(value);

        return normalized switch
        {
            "Jr-NM" => "JrNM",
            "U-NM" => "UNM",
            _ => normalized
        };
    }
}
