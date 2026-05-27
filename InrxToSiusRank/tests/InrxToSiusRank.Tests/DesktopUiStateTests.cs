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
    public void Desktop_event_paths_force_project_output_directory_to_event_local_path()
    {
        var eventPath = Path.Combine("/Users/me/Stevner/Pinse2026", EventProjectFile.FileName);
        var localOutput = Path.Combine("/Users/me/Stevner/Pinse2026", "siusrank-import");
        const string externalOutput = "/Users/me/siusrank-import";

        Assert.Equal("./siusrank-import", DesktopEventPaths.ToEventLocalDisplayPath(eventPath, localOutput, "./siusrank-import"));
        Assert.Equal("./siusrank-import", DesktopEventPaths.ToEventLocalDisplayPath(eventPath, externalOutput, "./siusrank-import"));
        Assert.Equal("./siusrank-import", DesktopEventPaths.ToEventLocalDisplayPath(eventPath, null, "./siusrank-import"));
    }

    [Fact]
    public void Ssc_status_rows_provide_actionable_next_steps()
    {
        var rows = SscActionStatusBuilder.Build(new SscActionStatusInput(
            DatabaseExists: true,
            StevneIdsValid: true,
            StevneIds: [413, 414, 415, 416, 417],
            SelectedStevneIdsText: "413-417",
            LaneStevneId: null,
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
        Assert.Equal("Mangler stevnevalg", lanes.Status);
        Assert.Contains("Prosjektet har nå: 413-417 (5).", lanes.NextStep);
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

    [Fact]
    public void Writeback_instances_are_built_from_event_class_config()
    {
        var eventPath = Path.Combine("/Users/me/Stevner/Pinse2026", EventProjectFile.FileName);
        var config = new EventProjectConfig
        {
            Exercise = new EventExerciseConfig { Id = 9, Name = "Finpistol", ShortName = "Fin", HovedOvelseId = 1 },
            Inrx = new EventInrxConfig { Db = "./storage.db3", Stevner = "413-417" },
            Classes =
            [
                new EventClassConfig
                {
                    Class = "B",
                    Folder = "./SiusRank_Finpistol_B",
                    Exports = "./SiusRank_Finpistol_B/Exports"
                }
            ]
        };

        var rows = DesktopWritebackInstances.Build(eventPath, config);

        var row = Assert.Single(rows);
        Assert.Equal("B", row.Class);
        Assert.Equal("./SiusRank_Finpistol_B", row.Folder);
        Assert.Equal("./SiusRank_Finpistol_B/Exports", row.Exports);
        Assert.Equal("6FB", row.EventFilter);
    }

    [Fact]
    public void Writeback_instance_odf_count_is_per_exports_folder()
    {
        using var directory = TempDirectory.Create();
        var exports = Path.Combine(directory.Path, "Exports");
        Directory.CreateDirectory(exports);
        File.WriteAllText(Path.Combine(exports, "result.odf.xml"), "<root />");
        File.WriteAllText(Path.Combine(exports, "ignored.xml"), "<root />");

        Assert.Equal(1, DesktopWritebackInstances.CountOdfFiles(exports));
        Assert.Equal(0, DesktopWritebackInstances.CountOdfFiles(Path.Combine(directory.Path, "Missing")));
    }

    [Fact]
    public void Writeback_scanner_finds_exports_and_ignores_build_folders()
    {
        using var directory = TempDirectory.Create();
        var eventPath = Path.Combine(directory.Path, EventProjectFile.FileName);
        File.WriteAllText(eventPath, "{}");
        var rankB = Path.Combine(directory.Path, "Rank_B", "Exports");
        var ignored = Path.Combine(directory.Path, "bin", "Rank_C", "Exports");
        Directory.CreateDirectory(rankB);
        Directory.CreateDirectory(ignored);
        File.WriteAllText(Path.Combine(rankB, "result.odf.xml"), "<root />");
        File.WriteAllText(Path.Combine(ignored, "ignored.odf.xml"), "<root />");

        var result = DesktopSiusRankExportsScanner.Scan(eventPath, ConfigWithClasses(("B", "./Rank_B/Exports"), ("C", "./Rank_C/Exports")));

        Assert.Single(result.FoundExports);
        Assert.Equal("./Rank_B/Exports", result.FoundExports[0].DisplayPath);
        Assert.Contains(result.Rows, row => row.Class == "B" && row.Status == DesktopWritebackDiscoveryStatus.Ready);
        Assert.Contains(result.Rows, row => row.Class == "C" && row.Status == DesktopWritebackDiscoveryStatus.NoMatch);
    }

    [Fact]
    public void Writeback_scanner_prefers_exact_configured_exports_path()
    {
        using var directory = TempDirectory.Create();
        var eventPath = Path.Combine(directory.Path, EventProjectFile.FileName);
        File.WriteAllText(eventPath, "{}");
        var exact = Path.Combine(directory.Path, "Configured", "Exports");
        var suggested = Path.Combine(directory.Path, "Rank_B", "Exports");
        Directory.CreateDirectory(exact);
        Directory.CreateDirectory(suggested);
        File.WriteAllText(Path.Combine(exact, "exact.odf.xml"), "<root />");
        File.WriteAllText(Path.Combine(suggested, "suggested.odf.xml"), "<root />");

        var result = DesktopSiusRankExportsScanner.Scan(eventPath, ConfigWithClasses(("B", "./Configured/Exports")));

        var row = Assert.Single(result.Rows);
        Assert.Equal("./Configured/Exports", row.ExportsDisplayPath);
        Assert.Equal(DesktopWritebackDiscoveryStatus.Ready, row.Status);
    }

    [Fact]
    public void Writeback_scanner_reports_no_files_and_ambiguous_matches()
    {
        using var directory = TempDirectory.Create();
        var eventPath = Path.Combine(directory.Path, EventProjectFile.FileName);
        File.WriteAllText(eventPath, "{}");
        Directory.CreateDirectory(Path.Combine(directory.Path, "Rank_B", "Exports"));
        Directory.CreateDirectory(Path.Combine(directory.Path, "SiusRank_B", "Exports"));

        var result = DesktopSiusRankExportsScanner.Scan(eventPath, ConfigWithClasses(("B", "./Expected_B/Exports")));

        var row = Assert.Single(result.Rows);
        Assert.Equal(DesktopWritebackDiscoveryStatus.Ambiguous, row.Status);
        Assert.Equal(2, row.Candidates.Count);
        Assert.All(row.Candidates, candidate => Assert.Equal(0, candidate.FileCount));
    }

    [Fact]
    public void Writeback_scanner_assumes_single_unmatched_folder_for_single_unmatched_class()
    {
        using var directory = TempDirectory.Create();
        var eventPath = Path.Combine(directory.Path, EventProjectFile.FileName);
        File.WriteAllText(eventPath, "{}");
        var exports = Path.Combine(directory.Path, "Ukjent", "Exports");
        Directory.CreateDirectory(exports);
        File.WriteAllText(Path.Combine(exports, "result.odf.xml"), "<root />");

        var result = DesktopSiusRankExportsScanner.Scan(eventPath, ConfigWithClasses(("B", "./Expected_B/Exports")));

        var row = Assert.Single(result.Rows);
        Assert.Equal(DesktopWritebackDiscoveryStatus.Assumed, row.Status);
        Assert.Equal("./Ukjent/Exports", row.ExportsDisplayPath);
        Assert.True(row.CanRun);
    }

    private static CsvPreflightEventInput Event(
        int id,
        string name,
        IReadOnlyList<CsvPreflightExerciseInput> exercises) =>
        new(id, name, "2026-07-06", EventProjectPlanner.ChampionshipEventType, exercises);

    private static CsvPreflightExerciseInput Exercise(int id, string name, int starters) =>
        new(id, name, name[..Math.Min(3, name.Length)], HovedOvelseId: 1, starters);

    private static EventProjectConfig ConfigWithClasses(params (string Class, string Exports)[] classes) =>
        new()
        {
            Exercise = new EventExerciseConfig { Id = 9, Name = "Finpistol", ShortName = "Fin", HovedOvelseId = 1 },
            Inrx = new EventInrxConfig { Db = "./storage.db3", Stevner = "413" },
            Csv = new EventCsvConfig { Output = "./siusrank-import" },
            Classes = classes
                .Select(item => new EventClassConfig
                {
                    Class = item.Class,
                    Folder = item.Exports.Replace("/Exports", string.Empty, StringComparison.Ordinal),
                    Exports = item.Exports
                })
                .ToList()
        };

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
