using InrxToSiusRank.Desktop;

namespace InrxToSiusRank.Tests;

public sealed class DesktopUiStateTests
{
    [Fact]
    public void Desktop_stevne_id_parser_keeps_ranges_commas_and_distinct_order()
    {
        var ids = DesktopUiParsing.ParseIdList("413-415,415;417", "Stevne ids");

        Assert.Equal([413, 414, 415, 417], ids);
    }

    [Fact]
    public void Csv_preflight_filters_out_events_without_selected_exercise_starters()
    {
        var result = DesktopCsvPreflight.Build(
            [
                Event(413, "NM Fri", [Exercise(8, "Fripistol", 12)]),
                Event(414, "NM Sil", [Exercise(11, "Silhuett", 18)]),
                Event(415, "NM Fin", [Exercise(9, "Finpistol", 0)]),
                Event(416, "NM Fin", [Exercise(9, "Finpistol", 21)])
            ],
            new CsvExerciseSelection(IsAll: false, OvelseId: 9, Name: "Finpistol"));

        Assert.Equal([416], result.IncludedStevneIds);
        Assert.Equal([413, 414, 415], result.ExcludedStevneIds);
        Assert.Contains(result.Rows, row => row.StevneId == 413 && row.Status == "Øvelsen finnes ikke i dette stevnet");
        Assert.Contains(result.Rows, row => row.StevneId == 415 && row.Status == "Ingen startere for denne øvelsen");
        Assert.Equal("CSV-eksport: hopper over Stevne.Id 413, 414, 415 fordi Finpistol ikke har startere der.", result.SkippedMessage);
    }

    [Fact]
    public void Csv_all_exercises_options_do_not_set_exercise_filter()
    {
        var options = DesktopExportOptionsBuilder.Build(new DesktopExportOptionsInput(
            DatabasePath: "storage.db3",
            StevneIds: [413, 414],
            OutputDirectory: "siusrank-import",
            EncodingName: CsvEncoding.Utf8Bom,
            ShooterGroupsTemplatePath: null,
            Selection: CsvExerciseSelection.All));

        Assert.Null(options.OvelseId);
        Assert.Null(options.OvelseName);
        Assert.Equal([413, 414], options.StevneIds);
    }

    [Fact]
    public void Desktop_event_paths_display_and_resolve_project_relative_paths()
    {
        var eventPath = Path.Combine("/Users/me/Stevner/Pinse2026", EventProjectFile.FileName);
        var storagePath = Path.Combine("/Users/me/Stevner/Pinse2026", "storage.db3");

        Assert.Equal("./storage.db3", DesktopEventPaths.ToEventDisplayPath(eventPath, storagePath));
        Assert.Equal(storagePath, DesktopEventPaths.ResolveEventPath(eventPath, "./storage.db3"));
    }

    [Fact]
    public void Desktop_event_paths_keep_external_absolute_paths()
    {
        var eventPath = Path.Combine("/Users/me/Stevner/Pinse2026", EventProjectFile.FileName);
        const string externalPath = "/Applications/SIUS/SiusRank";

        Assert.Equal(externalPath, DesktopEventPaths.ToEventDisplayPath(eventPath, externalPath));
        Assert.Equal(externalPath, DesktopEventPaths.ResolveEventPath(eventPath, externalPath));
    }

    [Fact]
    public void Ssc_status_rows_provide_actionable_next_steps()
    {
        var rows = SscActionStatusBuilder.Build(new SscActionStatusInput(
            DatabaseExists: true,
            StevneIdsValid: true,
            StevneIds: [413, 414, 415, 416, 417],
            SelectedStevneIdsText: "413-417",
            OutputDirectoryPresent: true,
            UsersCsvExists: false,
            BibMapExists: false,
            OrganizationNamePresent: true,
            OrganizationIdPresent: true,
            StartlagPresent: true,
            StartlagValid: true));

        var validate = rows.Single(row => row.Action == "Valider SSC");
        Assert.False(validate.CanRun);
        Assert.Equal("Mangler Users CSV", validate.Status);
        Assert.Equal("Kjør 'Eksporter SSC-brukere' først, eller velg en eksisterende Users CSV.", validate.NextStep);

        var lanes = rows.Single(row => row.Action == "Eksporter SSC baner/reset");
        Assert.False(lanes.CanRun);
        Assert.Equal("Krever nøyaktig én Stevne.Id", lanes.Status);
        Assert.Contains("Nå valgt: 413-417 (5).", lanes.NextStep);
    }

    [Fact]
    public void Event_type_selection_uses_remembered_visible_loaded_and_fallback_values()
    {
        var result = DesktopEventTypeSelections.Build(
            selectedIds: [1, 2, 3, 4],
            rememberedSelections: new Dictionary<int, string>
            {
                [1] = EventProjectPlanner.ChampionshipEventType
            },
            visibleDefaults: new Dictionary<int, string>
            {
                [2] = EventProjectPlanner.ChampionshipEventType,
                [3] = "invalid"
            },
            loadedSelections: new Dictionary<string, string>
            {
                ["4"] = EventProjectPlanner.ChampionshipEventType
            });

        Assert.Equal(EventProjectPlanner.ChampionshipEventType, result["1"]);
        Assert.Equal(EventProjectPlanner.ChampionshipEventType, result["2"]);
        Assert.Equal(EventProjectPlanner.ApprovedEventType, result["3"]);
        Assert.Equal(EventProjectPlanner.ChampionshipEventType, result["4"]);
        Assert.DoesNotContain("5", result.Keys);
    }

    private static CsvPreflightEventInput Event(
        int id,
        string name,
        IReadOnlyList<CsvPreflightExerciseInput> exercises) =>
        new(id, name, "2026-07-06", EventProjectPlanner.ChampionshipEventType, exercises);

    private static CsvPreflightExerciseInput Exercise(int id, string name, int starters) =>
        new(id, name, name[..Math.Min(3, name.Length)], HovedOvelseId: 1, starters);
}
