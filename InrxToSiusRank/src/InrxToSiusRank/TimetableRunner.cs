using System.Globalization;

namespace InrxToSiusRank;

public static class TimetableRunner
{
    private const string NmPrecisionDate = "2026-07-09";
    private const string NmRapidDate = "2026-07-10";

    public static TimetableResult Run(TimetableOptions options)
    {
        using var repository = new SeedStartLagRepository(options.DatabasePath);
        var inputs = repository.GetEventInputs(options.StevneIds);
        return new TimetableResult(inputs.Select(BuildEvent).ToList());
    }

    public static TimetableEvent BuildEvent(SeedStartLagEventInput input)
    {
        var baseRelays = input.StartLags
            .OrderBy(startLag => startLag.Nr)
            .Select(startLag => BuildRelay(input, startLag))
            .ToList();

        var relays = IsFinOrGrovPistol(input.Ovelse)
            ? BuildPrecisionRapidRelays(baseRelays)
            : baseRelays;

        return new TimetableEvent(
            input.Stevne,
            input.Ovelse,
            input.Targets,
            relays);
    }

    private static TimetableRelay BuildRelay(SeedStartLagEventInput input, StartLagInfo startLag)
    {
        var shooters = input.Shooters
            .Where(shooter => shooter.OldRelay == startLag.Nr)
            .ToList();
        return new TimetableRelay(
            startLag.Nr,
            startLag.Date,
            shooters.Count,
            input.Targets.Count,
            FormatClassSummary(shooters));
    }

    private static IReadOnlyList<TimetableRelay> BuildPrecisionRapidRelays(
        IReadOnlyList<TimetableRelay> baseRelays)
    {
        return baseRelays
            .Select(relay => relay with
            {
                Date = MoveRelayToDate(relay.Date, NmPrecisionDate),
                StageName = "Precision"
            })
            .Concat(baseRelays.Select(relay => relay with
            {
                Date = MoveRelayToDate(relay.Date, NmRapidDate),
                StageName = "Rapid"
            }))
            .OrderBy(relay => ParseDate(relay.Date))
            .ThenBy(relay => relay.Number)
            .ToList();
    }

    private static string FormatClassSummary(IReadOnlyList<SeedStartLagShooter> shooters)
    {
        if (shooters.Count == 0)
        {
            return "-";
        }

        return string.Join(
            ", ",
            shooters
                .GroupBy(shooter => shooter.KmNmClass)
                .Select(group => $"{group.Key} {group.Count()}"));
    }

    private static string MoveRelayToDate(string relayDate, string date)
    {
        if (!DateTime.TryParse(relayDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return relayDate;
        }

        return $"{date} {parsed:HH:mm:ss}";
    }

    private static DateTime ParseDate(string relayDate) =>
        DateTime.TryParse(relayDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : DateTime.MaxValue;

    private static bool IsFinOrGrovPistol(OvelseInfo ovelse) =>
        ovelse.Id is 8 or 9 ||
        ovelse.Name.Equals("Finpistol", StringComparison.OrdinalIgnoreCase) ||
        ovelse.Name.Equals("Grovpistol", StringComparison.OrdinalIgnoreCase);
}

public static class TimetableReporter
{
    public static void Print(TimetableResult result)
    {
        Console.WriteLine("NM timetable");
        foreach (var timetableEvent in result.Events)
        {
            PrintEvent(timetableEvent);
        }
    }

    private static void PrintEvent(TimetableEvent timetableEvent)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"{FormatDate(timetableEvent.Stevne.Date)}  " +
            $"Stevne.Id={timetableEvent.Stevne.Id}  " +
            $"{timetableEvent.Ovelse.Name}  " +
            $"{timetableEvent.Stevne.Name}");
        Console.WriteLine($"Targets: {FormatTargets(timetableEvent.Targets)}");

        foreach (var relay in timetableEvent.Relays)
        {
            var capacityWarning = relay.ShooterCount > relay.Capacity
                ? "  OVER CAPACITY"
                : string.Empty;
            var stage = string.IsNullOrWhiteSpace(relay.StageName)
                ? string.Empty
                : $"{relay.StageName} ";
            Console.WriteLine(
                $"  {FormatDate(relay.Date)}  {stage}Lag {relay.Number}: " +
                $"{relay.ShooterCount}/{relay.Capacity} skyttere  " +
                $"{relay.ClassSummary}{capacityWarning}");
        }
    }

    private static string FormatTargets(IReadOnlyList<int> targets)
    {
        if (targets.Count == 0)
        {
            return "-";
        }

        var ranges = new List<string>();
        var start = targets[0];
        var previous = targets[0];
        foreach (var target in targets.Skip(1))
        {
            if (target == previous + 1)
            {
                previous = target;
                continue;
            }

            ranges.Add(FormatRange(start, previous));
            start = target;
            previous = target;
        }

        ranges.Add(FormatRange(start, previous));
        return string.Join(", ", ranges);
    }

    private static string FormatRange(int start, int end) =>
        start == end
            ? start.ToString(CultureInfo.InvariantCulture)
            : $"{start}-{end}";

    private static string FormatDate(string value)
    {
        var formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-dd"
        };

        return DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ||
               DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
            ? parsed.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : value;
    }
}
