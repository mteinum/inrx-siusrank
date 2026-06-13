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
    public void Csv_preflight_missing_selected_exercise_rows_show_available_exercises()
    {
        var result = DesktopCsvPreflight.Build(
            [
                Event(413, "NM Hurtig", [Exercise(7, "Hurtigpistol", 12)]),
                Event(414, "NM Standard", [Exercise(10, "Standard", 14)])
            ],
            new CsvExerciseSelection(IsAll: false, OvelseId: 10, Name: "Standard"));

        var missing = result.Rows.Single(row => row.StevneId == 413);
        Assert.Equal("Hurtigpistol", missing.OvelseName);
        Assert.Null(missing.OvelseId);
        Assert.Equal("Øvelsen finnes ikke i dette stevnet", missing.Status);

        var included = result.Rows.Single(row => row.StevneId == 414);
        Assert.Equal("Standard", included.OvelseName);
        Assert.Equal(10, included.OvelseId);
        Assert.True(included.Include);
    }

    [Fact]
    public void Csv_preflight_rows_show_distinct_classes_for_exercise()
    {
        var result = DesktopCsvPreflight.Build(
            [
                Event(
                    415,
                    "NM Silhuett",
                    [Exercise(11, "Silhuett", 3, ["V55", "Apen", "Apen", "Jrm"])])
            ],
            new CsvExerciseSelection(IsAll: false, OvelseId: 11, Name: "Silhuett"));

        var row = Assert.Single(result.Rows);
        Assert.Equal("Jrm, Apen, V55", row.Classes);
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
            Selection: CsvExerciseSelection.All,
            SilhouetteShootersPerStand: 1,
            FinalClasses: ["Apen", "Jm"]));

        Assert.Null(options.OvelseId);
        Assert.Null(options.OvelseName);
        Assert.Equal([413, 414], options.StevneIds);
        Assert.Equal(1, options.SilhouetteShootersPerStand);
        Assert.Equal(["Apen", "Jm"], options.FinalClasses);
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

        var validate = rows.Single(row => row.Action == "Kontroller oppsett");
        Assert.False(validate.CanRun);
        Assert.Equal("Mangler Users CSV", validate.Status);
        Assert.Equal("Klikk Lag SSC-brukere først.", validate.NextStep);

        var lanes = rows.Single(row => row.Action == "Lag banefiler");
        Assert.False(lanes.CanRun);
        Assert.Equal("Velg stevne", lanes.Status);
        Assert.Contains("Nå valgt i prosjektet: 413-417 (5).", lanes.NextStep);
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
    public void Event_type_effective_selection_prefers_ui_then_loaded_then_repository_fallback()
    {
        var remembered = new Dictionary<int, string>
        {
            [1] = EventProjectPlanner.ChampionshipEventType
        };
        var visible = new Dictionary<int, string?>
        {
            [2] = EventProjectPlanner.ChampionshipEventType,
            [4] = "invalid"
        };
        var loaded = new Dictionary<string, string>
        {
            ["3"] = EventProjectPlanner.ChampionshipEventType
        };

        Assert.Equal(
            EventProjectPlanner.ChampionshipEventType,
            DesktopEventTypeSelections.ResolveEffective(1, remembered, visible, loaded, EventProjectPlanner.ApprovedEventType));
        Assert.Equal(
            EventProjectPlanner.ChampionshipEventType,
            DesktopEventTypeSelections.ResolveEffective(2, remembered, visible, loaded, EventProjectPlanner.ApprovedEventType));
        Assert.Equal(
            EventProjectPlanner.ChampionshipEventType,
            DesktopEventTypeSelections.ResolveEffective(3, remembered, visible, loaded, EventProjectPlanner.ApprovedEventType));
        Assert.Equal(
            EventProjectPlanner.ChampionshipEventType,
            DesktopEventTypeSelections.ResolveEffective(5, remembered, visible, loaded, EventProjectPlanner.ChampionshipEventType));
        Assert.Equal(
            EventProjectPlanner.ApprovedEventType,
            DesktopEventTypeSelections.ResolveEffective(4, remembered, visible, loaded, EventProjectPlanner.ChampionshipEventType));
    }

    [Fact]
    public void Csv_preflight_rows_keep_effective_event_type()
    {
        var effectiveEventType = DesktopEventTypeSelections.ResolveEffective(
            413,
            new Dictionary<int, string> { [413] = EventProjectPlanner.ChampionshipEventType },
            new Dictionary<int, string?>(),
            null,
            EventProjectPlanner.ApprovedEventType);

        var result = DesktopCsvPreflight.Build(
            [Event(413, "NM Fin", [Exercise(9, "Finpistol", 21)], effectiveEventType)],
            new CsvExerciseSelection(IsAll: false, OvelseId: 9, Name: "Finpistol"));

        var row = Assert.Single(result.Rows);
        Assert.Equal(EventProjectPlanner.ChampionshipEventType, row.EventType);
    }

    [Fact]
    public void Writeback_instance_odf_count_is_per_exports_folder()
    {
        using var directory = TempDirectory.Create();
        var exports = Path.Combine(directory.Path, "Exports");
        Directory.CreateDirectory(exports);
        File.WriteAllText(Path.Combine(exports, "result.odf.xml"), "<root />");
        File.WriteAllText(Path.Combine(exports, "ignored.xml"), "<root />");

        Assert.Equal(1, DesktopSiusRankExportsScanner.CountOdfFiles(exports));
        Assert.Equal(0, DesktopSiusRankExportsScanner.CountOdfFiles(Path.Combine(directory.Path, "Missing")));
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
        File.WriteAllText(Path.Combine(rankB, "result.odf.xml"), OdfXml("Fin_M", "SPM_M"));
        File.WriteAllText(Path.Combine(ignored, "ignored.odf.xml"), OdfXml("Fin_K", "SPW_K"));

        var result = DesktopSiusRankExportsScanner.Scan(eventPath, ConfigWithExercise(9, "Finpistol"));

        Assert.Single(result.FoundExports);
        Assert.Equal("./Rank_B/Exports", result.FoundExports[0].DisplayPath);
        var row = Assert.Single(result.Rows);
        Assert.Equal("M", row.Class);
        Assert.Equal("FINM", row.EventFilter);
        Assert.Equal(DesktopWritebackDiscoveryStatus.Ready, row.Status);
    }

    [Fact]
    public void Writeback_scanner_derives_result_class_from_odf_not_folder_name()
    {
        using var directory = TempDirectory.Create();
        var eventPath = Path.Combine(directory.Path, EventProjectFile.FileName);
        File.WriteAllText(eventPath, "{}");
        var suggested = Path.Combine(directory.Path, "Rank_B", "Exports");
        Directory.CreateDirectory(Path.Combine(directory.Path, "Configured", "Exports"));
        Directory.CreateDirectory(suggested);
        File.WriteAllText(Path.Combine(suggested, "suggested.odf.xml"), OdfXml("Fin_M", "SPM_M"));

        var result = DesktopSiusRankExportsScanner.Scan(eventPath, ConfigWithExercise(9, "Finpistol"));

        var row = Assert.Single(result.Rows);
        Assert.Equal("M", row.Class);
        Assert.Equal("./Rank_B/Exports", row.ExportsDisplayPath);
        Assert.Equal(DesktopWritebackDiscoveryStatus.Ready, row.Status);
    }

    [Fact]
    public void Writeback_scanner_ignores_results_for_other_exercises()
    {
        using var directory = TempDirectory.Create();
        var eventPath = Path.Combine(directory.Path, EventProjectFile.FileName);
        File.WriteAllText(eventPath, "{}");
        var exports = Path.Combine(directory.Path, "Rank_M", "Exports");
        Directory.CreateDirectory(exports);
        File.WriteAllText(Path.Combine(exports, "standard.odf.xml"), OdfXml("Standard_M", "STP_M"));

        var result = DesktopSiusRankExportsScanner.Scan(eventPath, ConfigWithExercise(9, "Finpistol"));

        Assert.Single(result.FoundExports);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public void Writeback_scanner_marks_matching_results_without_shots_as_no_files()
    {
        using var directory = TempDirectory.Create();
        var eventPath = Path.Combine(directory.Path, EventProjectFile.FileName);
        File.WriteAllText(eventPath, "{}");
        var exports = Path.Combine(directory.Path, "Rank_M", "Exports");
        Directory.CreateDirectory(exports);
        File.WriteAllText(Path.Combine(exports, "startlist.odf.xml"), OdfXml("Fin_M", "SPM_M", includeShots: false));

        var result = DesktopSiusRankExportsScanner.Scan(eventPath, ConfigWithExercise(9, "Finpistol"));

        var row = Assert.Single(result.Rows);
        Assert.Equal(DesktopWritebackDiscoveryStatus.NoFiles, row.Status);
        Assert.False(row.CanRun);
    }

    private static CsvPreflightEventInput Event(
        int id,
        string name,
        IReadOnlyList<CsvPreflightExerciseInput> exercises,
        string eventType = EventProjectPlanner.ChampionshipEventType) =>
        new(id, name, "2026-07-06", eventType, exercises);

    private static CsvPreflightExerciseInput Exercise(
        int id,
        string name,
        int starters,
        IReadOnlyList<string>? classes = null) =>
        new(
            id,
            name,
            name[..Math.Min(3, name.Length)],
            HovedOvelseId: 1,
            starters,
            classes ?? []);

    private static EventProjectConfig ConfigWithExercise(int ovelseId, string ovelseName) =>
        new()
        {
            Exercise = new EventExerciseConfig { Id = ovelseId, Name = ovelseName, ShortName = "Fin", HovedOvelseId = 1 },
            Inrx = new EventInrxConfig { Db = "./storage.db3", Stevner = "413" },
            Csv = new EventCsvConfig { Output = "./siusrank-import" }
        };

    private static string OdfXml(string shortName, string eventCode, bool includeShots = true)
    {
        var shotXml = includeShots
            ? """<ExtendedResult Type="CER_SH" Pos="1" Code="SH_SHOT" Value="10"><Extensions><Extension Type="SH_SHOT" Code="SH_TIMESTAMP" Value="2026-05-23T11:04:24.5300000" /></Extensions></ExtendedResult>"""
            : string.Empty;
        return $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <OdfBody ResultStatus="INTERIM">
          <Competition Code="">
            <ExtendedHeader EventCode="{{eventCode}}" ShortName="{{shortName}}" EventUnitName="{{shortName}}" ProductType="IndividualResults" />
            <CumulativeResult Rank="1" ResultType="POINTS" Result="10" SortOrder="1">
              <Competitor AccreditationNumber="1273763" Bib="26008" Organisation="NOR" NameDisplay="Test Shooter">
                <Composition>
                  <Athlete Bib="26008" AccreditationNumber="1273763" FamilyName="Shooter" GivenName="Test">
                    <ExtendedResults>
                      <ExtendedResult Type="CER_SH" Code="SH_INNER_TENS" Value="1" />
                      {{shotXml}}
                    </ExtendedResults>
                  </Athlete>
                </Composition>
              </Competitor>
            </CumulativeResult>
          </Competition>
        </OdfBody>
        """;
    }

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
