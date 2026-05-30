using System.Globalization;

namespace InrxToSiusRank;

public static class ExportValidator
{
    public static void ValidateShooterGroups(
        IReadOnlyList<SiusRankStarter> rows,
        ShooterGroupsTemplate? shooterGroupsTemplate)
    {
        if (shooterGroupsTemplate is null)
        {
            return;
        }

        var missingGroups = rows
            .Select(row => row.Groups)
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(group => !shooterGroupsTemplate.TryGet(group, out _))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missingGroups.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Generated SIUS Rank Groups value(s) not found in shooter groups template " +
            $"{shooterGroupsTemplate.Path}: {string.Join(", ", missingGroups)}");
    }

    public static IEnumerable<string> Validate(IReadOnlyList<SiusRankStarter> rows)
    {
        foreach (var row in rows.Where(row => string.IsNullOrWhiteSpace(row.TargetNumber)))
        {
            yield return $"Starter {row.StartNumber} ({row.DisplayName}) has no target number.";
        }

        foreach (var row in rows.Where(row => string.IsNullOrWhiteSpace(row.Relay)))
        {
            yield return $"Starter {row.StartNumber} ({row.DisplayName}) has no relay.";
        }

        foreach (var duplicate in rows
                     .Where(row => !string.IsNullOrWhiteSpace(row.Relay) && !string.IsNullOrWhiteSpace(row.TargetNumber))
                     .GroupBy(row => (row.Relay, row.TargetNumber))
                     .Where(group => group.Count() > 1))
        {
            yield return
                $"Duplicate relay/target pair Relay={duplicate.Key.Relay}, Target={duplicate.Key.TargetNumber}: " +
                string.Join(", ", duplicate.Select(row => row.StartNumber));
        }

        foreach (var duplicate in rows
                     .GroupBy(row => row.StartNumber)
                     .Where(group => group.Count() > 1))
        {
            yield return
                $"Duplicate StartNumber {duplicate.Key}: " +
                string.Join(", ", duplicate.Select(row => row.DisplayName));
        }
    }

    public static void EnsureValidInrxSilhouetteTargets(
        IReadOnlyList<InrxStarter> starters,
        OvelseInfo ovelse,
        int shootersPerStand)
    {
        var errors = ValidateInrxSilhouetteTargets(starters, ovelse, shootersPerStand).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }
    }

    public static void EnsureValidSilhouetteTargets(
        IReadOnlyList<SiusRankStarter> rows,
        OvelseInfo ovelse,
        int shootersPerStand)
    {
        var errors = ValidateSilhouetteTargets(rows, ovelse, shootersPerStand).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }
    }

    public static IEnumerable<string> ValidateInrxSilhouetteTargets(
        IReadOnlyList<InrxStarter> starters,
        OvelseInfo ovelse,
        int shootersPerStand)
    {
        if (!IsSilhouette(ovelse))
        {
            yield break;
        }

        var allowedTargets = SeedStartLagRepository.ResolveSilhouetteTargets(shootersPerStand);
        var allowed = allowedTargets.ToHashSet();
        foreach (var starter in starters)
        {
            var target = ResolveTargetNumber(starter);
            if (target is not null && allowed.Contains(target.Value))
            {
                continue;
            }

            yield return FormatInvalidSilhouetteTarget(
                shootersPerStand,
                allowedTargets,
                target?.ToString(CultureInfo.InvariantCulture) ?? "<blank>",
                $"{starter.ResultatId} ({starter.FirstName} {starter.LastName})");
        }
    }

    public static IEnumerable<string> ValidateSilhouetteTargets(
        IReadOnlyList<SiusRankStarter> rows,
        OvelseInfo ovelse,
        int shootersPerStand)
    {
        if (!IsSilhouette(ovelse))
        {
            yield break;
        }

        var allowedTargets = SeedStartLagRepository.ResolveSilhouetteTargets(shootersPerStand);
        var allowed = allowedTargets.ToHashSet();
        foreach (var row in rows)
        {
            if (int.TryParse(row.TargetNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var target) &&
                allowed.Contains(target))
            {
                continue;
            }

            yield return FormatInvalidSilhouetteTarget(
                shootersPerStand,
                allowedTargets,
                string.IsNullOrWhiteSpace(row.TargetNumber) ? "<blank>" : row.TargetNumber,
                $"{row.StartNumber} ({row.DisplayName})");
        }
    }

    public static bool IsSilhouette(OvelseInfo ovelse) =>
        ovelse.Id == 11 || ovelse.Name.Contains("Silhuett", StringComparison.OrdinalIgnoreCase);

    private static int? ResolveTargetNumber(InrxStarter starter)
    {
        if (starter.Standplass > 0)
        {
            return starter.Standplass;
        }

        return int.TryParse(starter.SkivenrFra, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string FormatInvalidSilhouetteTarget(
        int shootersPerStand,
        IReadOnlyList<int> allowedTargets,
        string actualTarget,
        string starter)
    {
        var layout = shootersPerStand == 1 ? "1 skytter per stativ" : "2 skyttere per stativ";
        return
            $"Silhuett standplass {actualTarget} is invalid for {layout}. " +
            $"Allowed targets: {string.Join(", ", allowedTargets)}. Starter {starter}.";
    }
}
