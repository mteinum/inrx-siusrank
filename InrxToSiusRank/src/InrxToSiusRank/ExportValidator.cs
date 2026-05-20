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
}
