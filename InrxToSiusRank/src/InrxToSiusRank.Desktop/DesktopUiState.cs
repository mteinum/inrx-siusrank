using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace InrxToSiusRank.Desktop;

public static class DesktopEventPaths
{
    public static string GetEventDirectory(string? eventJsonPath) =>
        string.IsNullOrWhiteSpace(eventJsonPath)
            ? Environment.CurrentDirectory
            : EventProjectFile.GetEventDirectory(eventJsonPath);

    public static string ToEventDisplayPath(string? eventJsonPath, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim();
        if (!Path.IsPathRooted(trimmed) && !EventProjectFile.IsWindowsRootedPath(trimmed))
        {
            return NormalizeRelativeDisplayPath(trimmed);
        }

        var eventDirectory = GetEventDirectory(eventJsonPath);
        if (!EventProjectFile.IsInsideDirectory(eventDirectory, trimmed))
        {
            return trimmed;
        }

        return EventProjectFile.ToStoredPath(
            Path.Combine(eventDirectory, EventProjectFile.FileName),
            trimmed);
    }

    public static string ResolveEventPath(string? eventJsonPath, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim();
        if (Path.IsPathRooted(trimmed) || EventProjectFile.IsWindowsRootedPath(trimmed))
        {
            return trimmed;
        }

        var eventDirectory = GetEventDirectory(eventJsonPath);
        return EventProjectFile.ResolvePath(
            Path.Combine(eventDirectory, EventProjectFile.FileName),
            trimmed);
    }

    public static bool IsInsideEventDirectory(string? eventJsonPath, string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        EventProjectFile.IsInsideDirectory(GetEventDirectory(eventJsonPath), ResolveEventPath(eventJsonPath, path));

    private static string NormalizeRelativeDisplayPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.StartsWith("./", StringComparison.Ordinal) ||
               normalized.StartsWith("../", StringComparison.Ordinal) ||
               normalized.Equals(".", StringComparison.Ordinal) ||
               normalized.Equals("..", StringComparison.Ordinal)
            ? normalized
            : "./" + normalized;
    }
}

public static class DesktopUiParsing
{
    public static IReadOnlyList<int> ParseIdList(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.");
        }

        var ids = new List<int>();
        foreach (var item in value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var range = item.Split('-', 2, StringSplitOptions.TrimEntries);
            if (range.Length == 2)
            {
                if (!int.TryParse(range[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var from) ||
                    !int.TryParse(range[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var to) ||
                    from > to)
                {
                    throw new ArgumentException($"{name} has invalid range '{item}'.");
                }

                ids.AddRange(Enumerable.Range(from, to - from + 1));
                continue;
            }

            if (!int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                throw new ArgumentException($"{name} must contain comma-separated ids or ranges.");
            }

            ids.Add(id);
        }

        return ids.Distinct().ToList();
    }

    public static IReadOnlyList<int> ParseOptionalIdList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : ParseIdList(value, "Stevne ids");

    public static bool TryParseOptionalIds(string? value, out IReadOnlyList<int> ids)
    {
        try
        {
            ids = ParseOptionalIdList(value);
            return true;
        }
        catch (ArgumentException)
        {
            ids = [];
            return false;
        }
    }
}

public sealed record CsvExerciseSelection(
    bool IsAll,
    int? OvelseId,
    string Name)
{
    public static CsvExerciseSelection All { get; } = new(true, null, "Alle øvelser i valgte stevner");
}

public sealed record CsvPreflightExerciseInput(
    int Id,
    string Name,
    string ShortName,
    int HovedOvelseId,
    int StarterCount);

public sealed record CsvPreflightEventInput(
    int Id,
    string Name,
    string Date,
    string EventType,
    IReadOnlyList<CsvPreflightExerciseInput> Exercises);

public sealed record CsvPreflightRow(
    bool Include,
    string Date,
    int StevneId,
    string StevneName,
    string EventType,
    string OvelseName,
    int? OvelseId,
    int StarterCount,
    string Status);

public sealed record CsvPreflightResult(IReadOnlyList<CsvPreflightRow> Rows, CsvExerciseSelection Selection)
{
    public bool CanExport => Rows.Any(row => row.Include);

    public IReadOnlyList<int> IncludedStevneIds => Rows
        .Where(row => row.Include)
        .Select(row => row.StevneId)
        .Distinct()
        .Order()
        .ToList();

    public IReadOnlyList<int> ExcludedStevneIds => Rows
        .Where(row => !row.Include)
        .Select(row => row.StevneId)
        .Distinct()
        .Order()
        .ToList();

    public string EmptyMessage => Selection.IsAll
        ? "Ingen startere i valgte stevner."
        : $"Ingen startere for {Selection.Name} i valgte stevner.";

    public string SkippedMessage => ExcludedStevneIds.Count == 0
        ? string.Empty
        : Selection.IsAll
            ? $"CSV-eksport: hopper over Stevne.Id {string.Join(", ", ExcludedStevneIds)} fordi de ikke har øvelser med startere."
            : $"CSV-eksport: hopper over Stevne.Id {string.Join(", ", ExcludedStevneIds)} fordi {Selection.Name} ikke har startere der.";
}

public static class DesktopCsvPreflight
{
    public static CsvPreflightResult Build(
        IReadOnlyList<CsvPreflightEventInput> events,
        CsvExerciseSelection selection)
    {
        var rows = selection.IsAll
            ? BuildAllExerciseRows(events)
            : BuildSpecificExerciseRows(events, selection);

        return new CsvPreflightResult(rows, selection);
    }

    private static IReadOnlyList<CsvPreflightRow> BuildAllExerciseRows(IReadOnlyList<CsvPreflightEventInput> events)
    {
        var rows = new List<CsvPreflightRow>();
        foreach (var item in events)
        {
            if (item.Exercises.Count == 0)
            {
                rows.Add(new CsvPreflightRow(
                    Include: false,
                    Date: item.Date,
                    StevneId: item.Id,
                    StevneName: item.Name,
                    EventType: item.EventType,
                    OvelseName: "-",
                    OvelseId: null,
                    StarterCount: 0,
                    Status: "Ingen øvelser med startere"));
                continue;
            }

            foreach (var exercise in item.Exercises.OrderBy(exercise => exercise.Name, StringComparer.OrdinalIgnoreCase))
            {
                var include = exercise.StarterCount > 0;
                rows.Add(new CsvPreflightRow(
                    Include: include,
                    Date: item.Date,
                    StevneId: item.Id,
                    StevneName: item.Name,
                    EventType: item.EventType,
                    OvelseName: exercise.Name,
                    OvelseId: exercise.Id,
                    StarterCount: exercise.StarterCount,
                    Status: include ? "Klar" : "Ingen startere for denne øvelsen"));
            }
        }

        return rows;
    }

    private static IReadOnlyList<CsvPreflightRow> BuildSpecificExerciseRows(
        IReadOnlyList<CsvPreflightEventInput> events,
        CsvExerciseSelection selection)
    {
        var rows = new List<CsvPreflightRow>();
        foreach (var item in events)
        {
            var exercise = item.Exercises.FirstOrDefault(exercise => exercise.Id == selection.OvelseId);
            if (exercise is null)
            {
                rows.Add(new CsvPreflightRow(
                    Include: false,
                    Date: item.Date,
                    StevneId: item.Id,
                    StevneName: item.Name,
                    EventType: item.EventType,
                    OvelseName: selection.Name,
                    OvelseId: selection.OvelseId,
                    StarterCount: 0,
                    Status: "Øvelsen finnes ikke i dette stevnet"));
                continue;
            }

            var include = exercise.StarterCount > 0;
            rows.Add(new CsvPreflightRow(
                Include: include,
                Date: item.Date,
                StevneId: item.Id,
                StevneName: item.Name,
                EventType: item.EventType,
                OvelseName: exercise.Name,
                OvelseId: exercise.Id,
                StarterCount: exercise.StarterCount,
                Status: include ? "Klar" : "Ingen startere for denne øvelsen"));
        }

        return rows;
    }
}

public static class DesktopEventTypeSelections
{
    public static IReadOnlyDictionary<string, string> Build(
        IReadOnlyList<int> selectedIds,
        IReadOnlyDictionary<int, string> rememberedSelections,
        IReadOnlyDictionary<int, string> visibleDefaults,
        IReadOnlyDictionary<string, string>? loadedSelections)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in selectedIds)
        {
            var idText = id.ToString(CultureInfo.InvariantCulture);
            var selected = rememberedSelections.TryGetValue(id, out var remembered)
                ? remembered
                : loadedSelections?.TryGetValue(idText, out var loaded) == true
                    ? loaded
                    : visibleDefaults.TryGetValue(id, out var visible)
                        ? visible
                        : null;
            result[idText] = Normalize(selected);
        }

        return result;
    }

    public static string Normalize(string? eventType) =>
        eventType == EventProjectPlanner.ChampionshipEventType
            ? EventProjectPlanner.ChampionshipEventType
            : EventProjectPlanner.ApprovedEventType;
}

public sealed record DesktopExportOptionsInput(
    string DatabasePath,
    IReadOnlyList<int> StevneIds,
    string OutputDirectory,
    string EncodingName,
    string? ShooterGroupsTemplatePath,
    CsvExerciseSelection Selection);

public static class DesktopExportOptionsBuilder
{
    public static AppOptions Build(DesktopExportOptionsInput input) =>
        new(
            input.DatabasePath,
            StevneId: null,
            StevneIds: input.StevneIds,
            EventDate: null,
            EventName: null,
            OvelseId: input.Selection.IsAll ? null : input.Selection.OvelseId,
            OvelseName: null,
            ShooterGroupsTemplatePath: input.ShooterGroupsTemplatePath,
            OutputDirectory: input.OutputDirectory,
            EncodingName: input.EncodingName,
            Wizard: false);
}

public sealed record SscActionStatusInput(
    bool DatabaseExists,
    bool StevneIdsValid,
    IReadOnlyList<int> StevneIds,
    string SelectedStevneIdsText,
    bool OutputDirectoryPresent,
    bool UsersCsvExists,
    bool BibMapExists,
    bool OrganizationNamePresent,
    bool OrganizationIdPresent,
    bool StartlagPresent,
    bool StartlagValid);

public sealed record SscActionStatusRow(
    string Action,
    string Status,
    string NextStep,
    bool CanRun);

public static class SscActionStatusBuilder
{
    public static IReadOnlyList<SscActionStatusRow> Build(SscActionStatusInput input) =>
    [
        BuildUsersRow(input),
        BuildValidateRow(input),
        BuildLanesRow(input)
    ];

    private static SscActionStatusRow BuildUsersRow(SscActionStatusInput input)
    {
        var common = CommonBlockingStatus(input);
        if (common is not null)
        {
            return common with { Action = "Eksporter SSC-brukere" };
        }

        if (!input.OutputDirectoryPresent)
        {
            return new SscActionStatusRow("Eksporter SSC-brukere", "Mangler Output-mappe", "Velg hvor ssc-users.csv skal lagres.", false);
        }

        if (!input.OrganizationNamePresent)
        {
            return new SscActionStatusRow("Eksporter SSC-brukere", "Mangler organisasjonsnavn", "Fyll inn organisasjonsnavn.", false);
        }

        if (!input.OrganizationIdPresent)
        {
            return new SscActionStatusRow("Eksporter SSC-brukere", "Mangler organisasjons-id", "Fyll inn organisasjons-id.", false);
        }

        return new SscActionStatusRow(
            "Eksporter SSC-brukere",
            $"Klar ({input.StevneIds.Count} Stevne.Id)",
            "Lager ssc-users.csv og oppretter/oppdaterer bib-map.csv ved behov.",
            true);
    }

    private static SscActionStatusRow BuildValidateRow(SscActionStatusInput input)
    {
        var common = CommonBlockingStatus(input);
        if (common is not null)
        {
            return common with { Action = "Valider SSC" };
        }

        if (!input.UsersCsvExists)
        {
            return new SscActionStatusRow(
                "Valider SSC",
                "Mangler Users CSV",
                "Kjør 'Eksporter SSC-brukere' først, eller velg en eksisterende Users CSV.",
                false);
        }

        if (!input.BibMapExists)
        {
            return new SscActionStatusRow(
                "Valider SSC",
                "Mangler bib-map.csv",
                "Opprett bib-map.csv på CSV export-fanen, eller velg filen her.",
                false);
        }

        return new SscActionStatusRow("Valider SSC", $"Klar ({input.StevneIds.Count} Stevne.Id)", "Validerer brukere, bib-map, skiver og øvelsesmapping.", true);
    }

    private static SscActionStatusRow BuildLanesRow(SscActionStatusInput input)
    {
        var common = CommonBlockingStatus(input);
        if (common is not null)
        {
            return common with { Action = "Eksporter SSC baner/reset" };
        }

        if (input.StevneIds.Count != 1)
        {
            var selected = string.IsNullOrWhiteSpace(input.SelectedStevneIdsText)
                ? "ingen"
                : input.SelectedStevneIdsText;
            return new SscActionStatusRow(
                "Eksporter SSC baner/reset",
                "Krever nøyaktig én Stevne.Id",
                $"Velg ett stevne i Event-fanen, for eksempel 416. Nå valgt: {selected} ({input.StevneIds.Count}).",
                false);
        }

        if (!input.StartlagPresent)
        {
            return new SscActionStatusRow("Eksporter SSC baner/reset", "Mangler startlag", "Fyll inn startlag, for eksempel 2026-07-06T09:00:00.", false);
        }

        if (!input.StartlagValid)
        {
            return new SscActionStatusRow("Eksporter SSC baner/reset", "Ugyldig startlag", "Bruk format som 2026-07-06T09:00:00.", false);
        }

        if (!input.OutputDirectoryPresent)
        {
            return new SscActionStatusRow("Eksporter SSC baner/reset", "Mangler Output-mappe", "Velg hvor lane/reset-filene skal lagres.", false);
        }

        if (!input.BibMapExists)
        {
            return new SscActionStatusRow(
                "Eksporter SSC baner/reset",
                "Mangler bib-map.csv",
                "Opprett bib-map.csv på CSV export-fanen, eller velg filen her.",
                false);
        }

        return new SscActionStatusRow("Eksporter SSC baner/reset", "Klar (1 Stevne.Id)", "Lager reset-fil og aktiv lane-fil for valgt startlag.", true);
    }

    private static SscActionStatusRow? CommonBlockingStatus(SscActionStatusInput input)
    {
        if (!input.DatabaseExists)
        {
            return new SscActionStatusRow(string.Empty, "Mangler storage.db3", "Velg en eksisterende storage.db3 i Event-fanen.", false);
        }

        if (!input.StevneIdsValid)
        {
            return new SscActionStatusRow(string.Empty, "Ugyldig Stevne.Id", "Rett Stevne.Id-feltet i Event-fanen.", false);
        }

        if (input.StevneIds.Count == 0)
        {
            return new SscActionStatusRow(string.Empty, "Mangler Stevne.Id", "Velg ett eller flere stevner i Event-fanen.", false);
        }

        return null;
    }
}
