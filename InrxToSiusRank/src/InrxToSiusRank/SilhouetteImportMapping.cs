using System.Globalization;

namespace InrxToSiusRank;

public sealed record SilhouetteImportFields(string ImportShotFilter, int SiusDataStartNumber);

public static class SilhouetteImportMapping
{
    public static SilhouetteImportFields? ForTarget(string targetNumber)
    {
        if (!int.TryParse(targetNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var target))
        {
            return null;
        }

        return ForTarget(target);
    }

    public static SilhouetteImportFields? ForTarget(int targetNumber)
    {
        var targets = SeedStartLagRepository.ResolveSilhouetteTargets(2);
        var index = -1;
        for (var i = 0; i < targets.Count; i++)
        {
            if (targets[i] == targetNumber)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return null;
        }

        var filter = index % 2 == 0 ? "V" : "H";
        var siusDataStartNumber = (index / 2 + 1) * 1000;
        return new SilhouetteImportFields(filter, siusDataStartNumber);
    }
}
