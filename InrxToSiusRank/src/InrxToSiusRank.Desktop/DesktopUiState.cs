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

    public static string ToEventLocalDisplayPath(string? eventJsonPath, string? path, string fallbackRelativePath)
    {
        var fallback = NormalizeRelativeDisplayPath(fallbackRelativePath);
        if (string.IsNullOrWhiteSpace(path))
        {
            return fallback;
        }

        var resolved = ResolveEventPath(eventJsonPath, path);
        return IsInsideEventDirectory(eventJsonPath, resolved)
            ? ToEventDisplayPath(eventJsonPath, resolved)
            : fallback;
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
    int StarterCount,
    IReadOnlyList<string> Classes,
    string? ValidationStatus = null);

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
    string Classes,
    IReadOnlyList<string> ClassNames,
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
                    Classes: "-",
                    ClassNames: [],
                    Status: "Ingen øvelser med startere"));
                continue;
            }

            foreach (var exercise in item.Exercises.OrderBy(exercise => exercise.Name, StringComparer.OrdinalIgnoreCase))
            {
                var include = exercise.StarterCount > 0 && string.IsNullOrWhiteSpace(exercise.ValidationStatus);
                rows.Add(new CsvPreflightRow(
                    Include: include,
                    Date: item.Date,
                    StevneId: item.Id,
                    StevneName: item.Name,
                    EventType: item.EventType,
                    OvelseName: exercise.Name,
                    OvelseId: exercise.Id,
                    StarterCount: exercise.StarterCount,
                    Classes: FormatClasses(exercise.Classes),
                    ClassNames: SortClasses(exercise.Classes),
                    Status: !string.IsNullOrWhiteSpace(exercise.ValidationStatus)
                        ? exercise.ValidationStatus
                        : include ? "Klar" : "Ingen startere for denne øvelsen"));
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
                var availableExercises = FormatAvailableExercises(item.Exercises);
                rows.Add(new CsvPreflightRow(
                    Include: false,
                    Date: item.Date,
                    StevneId: item.Id,
                    StevneName: item.Name,
                    EventType: item.EventType,
                    OvelseName: availableExercises,
                    OvelseId: null,
                    StarterCount: 0,
                    Classes: "-",
                    ClassNames: [],
                    Status: "Øvelsen finnes ikke i dette stevnet"));
                continue;
            }

            var include = exercise.StarterCount > 0 && string.IsNullOrWhiteSpace(exercise.ValidationStatus);
            rows.Add(new CsvPreflightRow(
                Include: include,
                Date: item.Date,
                StevneId: item.Id,
                StevneName: item.Name,
                EventType: item.EventType,
                OvelseName: exercise.Name,
                OvelseId: exercise.Id,
                StarterCount: exercise.StarterCount,
                Classes: FormatClasses(exercise.Classes),
                ClassNames: SortClasses(exercise.Classes),
                Status: !string.IsNullOrWhiteSpace(exercise.ValidationStatus)
                    ? exercise.ValidationStatus
                    : include ? "Klar" : "Ingen startere for denne øvelsen"));
        }

        return rows;
    }

    private static string FormatAvailableExercises(IReadOnlyList<CsvPreflightExerciseInput> exercises) =>
        exercises.Count == 0
            ? "-"
            : string.Join(
                ", ",
                exercises
                    .OrderBy(exercise => exercise.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(exercise => exercise.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase));

    private static string FormatClasses(IReadOnlyList<string> classes) =>
        classes.Count == 0
            ? "-"
            : string.Join(
                ", ",
                SortClasses(classes));

    private static IReadOnlyList<string> SortClasses(IReadOnlyList<string> classes) =>
        classes
            .Where(className => !string.IsNullOrWhiteSpace(className))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(EffectiveKmNmClass.SortKey)
            .ThenBy(className => className, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    public static string ResolveEffective(
        int stevneId,
        IReadOnlyDictionary<int, string> rememberedSelections,
        IReadOnlyDictionary<int, string?> visibleSelections,
        IReadOnlyDictionary<string, string>? loadedSelections,
        string? repositoryFallback)
    {
        if (rememberedSelections.TryGetValue(stevneId, out var remembered))
        {
            return Normalize(remembered);
        }

        if (visibleSelections.TryGetValue(stevneId, out var visible))
        {
            return Normalize(visible);
        }

        var idText = stevneId.ToString(CultureInfo.InvariantCulture);
        if (loadedSelections?.TryGetValue(idText, out var loaded) == true)
        {
            return Normalize(loaded);
        }

        return Normalize(repositoryFallback);
    }

    public static string Normalize(string? eventType) =>
        eventType == EventProjectPlanner.ChampionshipEventType
            ? EventProjectPlanner.ChampionshipEventType
            : EventProjectPlanner.ApprovedEventType;
}

public enum DesktopWritebackDiscoveryStatus
{
    Ready,
    Assumed,
    NoFiles,
    NoMatch,
    Ambiguous
}

public sealed record DesktopDiscoveredExports(
    string DisplayPath,
    string FullPath,
    int FileCount,
    DateTime? NewestFileTime,
    IReadOnlyList<SiusRankExportCompetition> MatchingResults)
{
    public override string ToString() => DisplayPath;
}

public sealed record DesktopWritebackDiscoveryRow(
    string Class,
    string EventFilter,
    string? ExportsDisplayPath,
    string? ExportsFullPath,
    int FileCount,
    DesktopWritebackDiscoveryStatus Status,
    IReadOnlyList<DesktopDiscoveredExports> Candidates)
{
    public bool CanRun => !string.IsNullOrWhiteSpace(ExportsFullPath) &&
        FileCount > 0 &&
        Status is DesktopWritebackDiscoveryStatus.Ready or DesktopWritebackDiscoveryStatus.Assumed;
}

public sealed record DesktopWritebackDiscoveryResult(
    IReadOnlyList<DesktopDiscoveredExports> FoundExports,
    IReadOnlyList<DesktopWritebackDiscoveryRow> Rows)
{
    public int ReadyCount => Rows.Count(row => row.CanRun);

    public string Summary =>
        FoundExports.Count == 0
            ? "Ingen resultater funnet. Trykk Rank List Main i SIUS Rank og prøv igjen."
            : $"Fant {FoundExports.Count} Exports-mapper. {ReadyCount} resultatsett klare.";
}

public static class DesktopSiusRankExportsScanner
{
    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        "bin",
        "obj",
        "siusrank-import",
        "ssc-setup",
        "Templates"
    };

    public static DesktopWritebackDiscoveryResult Scan(string eventJsonPath, EventProjectConfig config)
    {
        var eventDirectory = EventProjectFile.GetEventDirectory(eventJsonPath);
        var found = FindExportsDirectories(eventJsonPath, eventDirectory, config);
        return Match(eventJsonPath, config, found);
    }

    public static DesktopWritebackDiscoveryResult Match(
        string eventJsonPath,
        EventProjectConfig config,
        IReadOnlyList<DesktopDiscoveredExports> foundExports)
    {
        var rows = foundExports
            .SelectMany(found => found.MatchingResults
                .Select(result => ToRow(result, found)))
            .OrderBy(row => row.Class, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.ExportsDisplayPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new DesktopWritebackDiscoveryResult(foundExports, rows);
    }

    private static DesktopWritebackDiscoveryRow ToRow(
        SiusRankExportCompetition result,
        DesktopDiscoveredExports found) =>
        new(
            DisplayClass(result),
            SiusRankEventDiscipline.NormalizeFilter(result.ShortName),
            found.DisplayPath,
            found.FullPath,
            found.FileCount,
            result.ShotResultCount == 0 ? DesktopWritebackDiscoveryStatus.NoFiles : DesktopWritebackDiscoveryStatus.Ready,
            []);

    private static IReadOnlyList<DesktopDiscoveredExports> FindExportsDirectories(
        string eventJsonPath,
        string eventDirectory,
        EventProjectConfig config)
    {
        if (!Directory.Exists(eventDirectory))
        {
            return [];
        }

        var found = new List<DesktopDiscoveredExports>();
        var pending = new Stack<string>();
        pending.Push(eventDirectory);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(directory);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var child in children)
            {
                var name = Path.GetFileName(child);
                if (IgnoredDirectoryNames.Contains(name))
                {
                    continue;
                }

                if (name.Equals("Exports", StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(BuildFoundExports(eventJsonPath, child, config));
                    continue;
                }

                pending.Push(child);
            }
        }

        return found
            .OrderBy(item => item.DisplayPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static DesktopDiscoveredExports BuildFoundExports(
        string eventJsonPath,
        string exportsDirectory,
        EventProjectConfig config)
    {
        var files = Directory.Exists(exportsDirectory)
            ? Directory.EnumerateFiles(exportsDirectory, "*.odf.xml", SearchOption.AllDirectories).ToList()
            : [];
        var newest = files.Count == 0
            ? (DateTime?)null
            : files.Max(File.GetLastWriteTime);
        var matchingResults = ReadMatchingResults(exportsDirectory, config);
        return new DesktopDiscoveredExports(
            DesktopEventPaths.ToEventDisplayPath(eventJsonPath, exportsDirectory),
            Path.GetFullPath(exportsDirectory),
            files.Count,
            newest,
            matchingResults);
    }

    private static IReadOnlyList<SiusRankExportCompetition> ReadMatchingResults(
        string exportsDirectory,
        EventProjectConfig config)
    {
        try
        {
            return SiusRankOdfExportReader.ReadLatestIndividualResults(exportsDirectory, new HashSet<string>(StringComparer.OrdinalIgnoreCase))
                .Where(result => SiusRankEventDiscipline.ResolveOvelseDefId(result.ShortName, result.EventCode) == config.Exercise.Id)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string DisplayClass(SiusRankExportCompetition result)
    {
        var name = string.IsNullOrWhiteSpace(result.ShortName) ? result.EventCode : result.ShortName;
        var suffix = name.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        return string.IsNullOrWhiteSpace(suffix) ? name : suffix;
    }

    public static int CountOdfFiles(string exportsDirectory) =>
        Directory.Exists(exportsDirectory)
            ? Directory.EnumerateFiles(exportsDirectory, "*.odf.xml", SearchOption.AllDirectories).Count()
            : 0;
}

public sealed record DesktopExportOptionsInput(
    string DatabasePath,
    IReadOnlyList<int> StevneIds,
    string OutputDirectory,
    string EncodingName,
    string? ShooterGroupsTemplatePath,
    CsvExerciseSelection Selection,
    int SilhouetteShootersPerStand = 2,
    IReadOnlyList<string>? FinalClasses = null,
    string? BibMapPath = null);

public static class DesktopExportOptionsBuilder
{
    public static SiusRankCsvExportOptions Build(DesktopExportOptionsInput input) =>
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
            SilhouetteShootersPerStand: input.SilhouetteShootersPerStand,
            FinalClasses: input.FinalClasses,
            BibMapPath: input.BibMapPath);
}

public sealed record SscActionStatusInput(
    bool DatabaseExists,
    bool StevneIdsValid,
    IReadOnlyList<int> StevneIds,
    string SelectedStevneIdsText,
    int? LaneStevneId,
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
            return common with { Action = "Lag SSC-brukere" };
        }

        if (!input.OutputDirectoryPresent)
        {
            return new SscActionStatusRow("Lag SSC-brukere", "Mangler mappe", "Velg hvor SSC-filene skal lagres.", false);
        }

        if (!input.OrganizationNamePresent)
        {
            return new SscActionStatusRow("Lag SSC-brukere", "Mangler organisasjon", "Åpne Avansert og fyll inn organisasjonsnavn.", false);
        }

        if (!input.OrganizationIdPresent)
        {
            return new SscActionStatusRow("Lag SSC-brukere", "Mangler organisasjon", "Åpne Avansert og fyll inn organisasjons-id.", false);
        }

        return new SscActionStatusRow(
            "Lag SSC-brukere",
            input.UsersCsvExists ? "Ferdig" : "Må kjøres",
            input.UsersCsvExists ? "ssc-users.csv finnes. Gå videre til kontroll." : "Klikk Lag SSC-brukere.",
            true);
    }

    private static SscActionStatusRow BuildValidateRow(SscActionStatusInput input)
    {
        var common = CommonBlockingStatus(input);
        if (common is not null)
        {
            return common with { Action = "Kontroller oppsett" };
        }

        if (!input.UsersCsvExists)
        {
            return new SscActionStatusRow(
                "Kontroller oppsett",
                "Mangler Users CSV",
                "Klikk Lag SSC-brukere først.",
                false);
        }

        if (!input.BibMapExists)
        {
            return new SscActionStatusRow(
                "Kontroller oppsett",
                "Mangler bib-map.csv",
                "Lag SSC-brukere eller opprett bib-map.csv på CSV export-fanen.",
                false);
        }

        return new SscActionStatusRow("Kontroller oppsett", "Klar", "Klikk Kontroller oppsett.", true);
    }

    private static SscActionStatusRow BuildLanesRow(SscActionStatusInput input)
    {
        var common = CommonBlockingStatus(input);
        if (common is not null)
        {
            return common with { Action = "Lag banefiler" };
        }

        if (input.LaneStevneId is null)
        {
            var selected = string.IsNullOrWhiteSpace(input.SelectedStevneIdsText)
                ? "ingen"
                : input.SelectedStevneIdsText;
            return new SscActionStatusRow(
                "Lag banefiler",
                "Velg stevne",
                $"Velg ett stevne øverst. Nå valgt i prosjektet: {selected} ({input.StevneIds.Count}).",
                false);
        }

        if (!input.StartlagPresent)
        {
            return new SscActionStatusRow("Lag banefiler", "Velg startlag", "Velg et startlag før banefiler kan lages.", false);
        }

        if (!input.StartlagValid)
        {
            return new SscActionStatusRow("Lag banefiler", "Ugyldig startlag", "Velg startlag fra listen, eller skriv ISO-tid i Avansert.", false);
        }

        if (!input.OutputDirectoryPresent)
        {
            return new SscActionStatusRow("Lag banefiler", "Mangler mappe", "Velg hvor SSC-filene skal lagres.", false);
        }

        if (!input.BibMapExists)
        {
            return new SscActionStatusRow(
                "Lag banefiler",
                "Mangler bib-map.csv",
                "Lag SSC-brukere eller opprett bib-map.csv på CSV export-fanen.",
                false);
        }

        return new SscActionStatusRow(
            "Lag banefiler",
            "Klar",
            "Klikk Lag banefiler.",
            true);
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
