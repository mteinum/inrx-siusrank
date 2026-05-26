using System.Globalization;
using System.Text.Json;

namespace InrxToSiusRank;

public static class SscLanePayloadBuilder
{
    public const string PayloadSpec = "InrxToSiusRank.SSC.Lanes.v1";
    public const string PayloadWarning =
        "Payload spec candidate. These files are deterministic SSC setup inputs only; no live forwarding is implemented. Watchtower RLR on SA951/Kanopus 2026.1.3 is not a reliable source.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static SscLanePayload BuildReset(int laneCount, DateTimeOffset generatedAtUtc)
    {
        ValidateLaneCount(laneCount);
        return new SscLanePayload(
            Spec: PayloadSpec,
            Kind: "reset",
            GeneratedAtUtc: generatedAtUtc,
            LaneCount: laneCount,
            StevneId: null,
            Startlag: null,
            Source: null,
            Warning: PayloadWarning,
            Lanes: Enumerable.Range(1, laneCount)
                .Select(lane => new SscLaneAssignment(
                    Lane: lane,
                    Active: false,
                    UserId: string.Empty,
                    DisplayName: string.Empty,
                    ExerciseName: string.Empty,
                    DeltakerId: null,
                    ResultatId: null))
                .ToList());
    }

    public static SscLanePayload BuildActive(
        IReadOnlyList<SscEventExport> events,
        DateTime startlag,
        IReadOnlyDictionary<int, string> startNumbersByDeltakerId,
        int laneCount,
        DateTimeOffset generatedAtUtc,
        out IReadOnlyList<SscValidationMessage> messages)
    {
        ValidateLaneCount(laneCount);
        var activeLanes = new List<SscLaneAssignment>();
        var validationMessages = new List<SscValidationMessage>();

        foreach (var eventExport in events)
        {
            string exerciseName;
            try
            {
                exerciseName = SscExerciseMapper.Resolve(eventExport.Ovelse);
            }
            catch (InvalidOperationException ex)
            {
                validationMessages.Add(new SscValidationMessage(SscValidationSeverity.Error, ex.Message));
                continue;
            }

            foreach (var starter in eventExport.Starters.Where(starter => IsSameStartlag(starter.RelayDate, startlag)))
            {
                var lane = ResolveLaneNumber(starter);
                if (lane is null)
                {
                    validationMessages.Add(new SscValidationMessage(
                        SscValidationSeverity.Error,
                        $"Resultat.Id={starter.ResultatId} ({starter.FirstName} {starter.LastName}) has no lane/target number."));
                    continue;
                }

                if (lane < 1 || lane > laneCount)
                {
                    validationMessages.Add(new SscValidationMessage(
                        SscValidationSeverity.Error,
                        $"Resultat.Id={starter.ResultatId} ({starter.FirstName} {starter.LastName}) has lane {lane}, outside 1-{laneCount}."));
                    continue;
                }

                if (!startNumbersByDeltakerId.TryGetValue(starter.DeltakerId, out var userId) ||
                    string.IsNullOrWhiteSpace(userId))
                {
                    validationMessages.Add(new SscValidationMessage(
                        SscValidationSeverity.Error,
                        $"Resultat.Id={starter.ResultatId} ({starter.FirstName} {starter.LastName}) has no UserId/bib-map assignment."));
                    continue;
                }

                activeLanes.Add(new SscLaneAssignment(
                    Lane: lane.Value,
                    Active: true,
                    UserId: userId,
                    DisplayName: SscUserMapper.BuildDisplayName(starter.FirstName, starter.LastName),
                    ExerciseName: exerciseName,
                    DeltakerId: starter.DeltakerId,
                    ResultatId: starter.ResultatId));
            }
        }

        foreach (var duplicate in activeLanes.GroupBy(lane => lane.Lane).Where(group => group.Count() > 1))
        {
            validationMessages.Add(new SscValidationMessage(
                SscValidationSeverity.Error,
                $"Duplicate active SSC lane {duplicate.Key}: " +
                string.Join(", ", duplicate.Select(lane => $"{lane.UserId}/{lane.DisplayName}"))));
        }

        if (activeLanes.Count == 0)
        {
            validationMessages.Add(new SscValidationMessage(
                SscValidationSeverity.Warning,
                $"No active lanes found for startlag {startlag:yyyy-MM-dd HH:mm:ss}."));
        }

        messages = SscValidationMessageFormatter.Distinct(validationMessages);
        var stevneIds = events
            .Select(eventExport => eventExport.Stevne.Id.ToString(CultureInfo.InvariantCulture))
            .Distinct()
            .ToList();
        return new SscLanePayload(
            Spec: PayloadSpec,
            Kind: "active-lanes",
            GeneratedAtUtc: generatedAtUtc,
            LaneCount: laneCount,
            StevneId: events.Count == 1 ? events[0].Stevne.Id : null,
            Startlag: startlag.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            Source: $"Stevne.Id={string.Join(",", stevneIds)}",
            Warning: PayloadWarning,
            Lanes: activeLanes.OrderBy(lane => lane.Lane).ToList());
    }

    public static int? ResolveLaneNumber(InrxStarter starter)
    {
        if (starter.Standplass > 0)
        {
            return starter.Standplass;
        }

        return int.TryParse(starter.SkivenrFra.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var lane)
            ? lane
            : null;
    }

    public static void Write(string path, SscLanePayload payload)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(payload, JsonOptions) + Environment.NewLine);
    }

    public static void ValidateLaneCount(int laneCount)
    {
        if (laneCount is not (10 or 25 or 40))
        {
            throw new ArgumentException("--lane-count must be 10, 25, or 40.");
        }
    }

    private static bool IsSameStartlag(string relayDate, DateTime startlag)
    {
        if (string.IsNullOrWhiteSpace(relayDate))
        {
            return false;
        }

        return DateTime.TryParse(relayDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) &&
               parsed == startlag;
    }
}
