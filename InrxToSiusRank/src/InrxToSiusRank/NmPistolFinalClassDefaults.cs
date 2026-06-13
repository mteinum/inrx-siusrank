namespace InrxToSiusRank;

public sealed record NmPistolFinalClassExercise(
    int OvelseId,
    string OvelseName,
    string ShortName,
    IReadOnlyDictionary<string, int> ClassCounts);

public static class NmPistolFinalClassDefaults
{
    private const int MinimumFinalParticipants = 2;

    private static readonly IReadOnlyDictionary<string, string[]> FinalClassesByExercise =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Finpistol"] = ["K", "Jk", "SH1"],
            ["Silhuett"] = ["Apen", "Jm"],
            ["Fripistol"] = ["SH1"]
        };

    public static string BuildText(IReadOnlyList<NmPistolFinalClassExercise> exercises)
    {
        var lines = exercises
            .Select(BuildLine)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return string.Join(Environment.NewLine, lines);
    }

    private static string? BuildLine(NmPistolFinalClassExercise exercise)
    {
        var exerciseName = ResolveExerciseName(exercise);
        if (exerciseName is null ||
            !FinalClassesByExercise.TryGetValue(exerciseName, out var finalClasses))
        {
            return null;
        }

        var presentFinalClasses = finalClasses
            .Where(className => CountParticipants(exercise.ClassCounts, className) >= MinimumFinalParticipants)
            .ToList();

        return presentFinalClasses.Count == 0
            ? null
            : $"{exerciseName}: {string.Join(",", presentFinalClasses)}";
    }

    private static int CountParticipants(IReadOnlyDictionary<string, int> classCounts, string className)
    {
        var count = 0;
        var normalizedClass = GroupNormalizer.Normalize(className);
        foreach (var item in classCounts)
        {
            if (item.Key.Equals(className, StringComparison.OrdinalIgnoreCase) ||
                GroupNormalizer.Normalize(item.Key).Equals(normalizedClass, StringComparison.OrdinalIgnoreCase))
            {
                count += item.Value;
            }
        }

        return count;
    }

    private static string? ResolveExerciseName(NmPistolFinalClassExercise exercise)
    {
        if (exercise.OvelseId is 9)
        {
            return "Finpistol";
        }

        if (exercise.OvelseId is 11)
        {
            return "Silhuett";
        }

        if (exercise.OvelseId is 18)
        {
            return "Fripistol";
        }

        var name = exercise.OvelseName.Trim();
        if (name.Contains("Finpistol", StringComparison.OrdinalIgnoreCase))
        {
            return "Finpistol";
        }

        if (name.Contains("Silhuett", StringComparison.OrdinalIgnoreCase))
        {
            return "Silhuett";
        }

        if (name.Contains("Fripistol", StringComparison.OrdinalIgnoreCase))
        {
            return "Fripistol";
        }

        return null;
    }
}
