using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace InrxToSiusRank.Desktop;

public partial class MainWindow : Window
{
    private readonly Dictionary<int, CheckBox> _stevneChecks = new();
    private readonly Dictionary<int, StevneChoice> _stevneChoices = new();
    private readonly Dictionary<int, ComboBox> _eventTypeInputs = new();
    private readonly Dictionary<int, string> _eventTypeSelections = new();
    private readonly Dictionary<string, TextBlock> _writebackStatusLabels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SiusRankClassWritebackStatus> _writebackStatuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DesktopWritebackDiscoveryRow> _writebackRows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Button> _writebackValidateButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Button> _writebackApplyButtons = new(StringComparer.OrdinalIgnoreCase);
    private string? _currentEventFilePath;
    private EventProjectConfig? _currentEventConfig;
    private CsvPreflightResult? _csvPreflight;
    private int _csvPreflightRefreshVersion;
    private bool _isRunning;
    private bool _updatingStevneChecks;
    private bool _updatingOvelseSelection;
    private readonly StringBuilder _logText = new();
    private LogWindow? _logWindow;
    private int _logErrorCount;
    private string _latestLogMessage = "Ingen loggmeldinger.";
    private DesktopSettings _desktopSettings = DesktopSettings.Empty;

    private static readonly IBrush ReadyStatusBrush = new SolidColorBrush(Color.Parse("#dafbe1"));
    private static readonly IBrush RunningStatusBrush = new SolidColorBrush(Color.Parse("#fff8c5"));
    private static readonly IBrush ErrorStatusBrush = new SolidColorBrush(Color.Parse("#ffebe9"));

    public MainWindow()
    {
        InitializeComponent();
        InitializeDefaults();
        WireEvents();
        UpdateActionStates();
        SetStatus(RunStatus.Ready);
        UpdateLogIndicators();
        QueueCsvPreflightRefresh();
        Closing += (_, _) => SaveDesktopSettings(logWarning: false);
    }

    private TextBox EventFilePathInput => Get<TextBox>("EventFilePathBox");

    private TextBox DatabasePathInput => Get<TextBox>("DatabasePathBox");

    private TextBox StevneSearchInput => Get<TextBox>("StevneSearchBox");

    private StackPanel StevneListContainer => Get<StackPanel>("StevneListPanel");

    private TextBox StevneIdsInput => Get<TextBox>("StevneIdsBox");

    private ComboBox EncodingInput => Get<ComboBox>("EncodingBox");

    private TextBox SiusRankFolderInput => Get<TextBox>("SiusRankFolderBox");

    private TextBox OutputDirectoryInput => Get<TextBox>("OutputDirectoryBox");

    private TextBox ShooterGroupsTemplateInput => Get<TextBox>("ShooterGroupsTemplateBox");

    private ComboBox OvelseSelectInput => Get<ComboBox>("OvelseSelectBox");

    private TextBox OvelseFilterInput => Get<TextBox>("OvelseFilterBox");

    private ComboBox SilhouetteShootersPerStandInput => Get<ComboBox>("SilhouetteShootersPerStandBox");

    private TextBox ExportsDirectoryInput => Get<TextBox>("ExportsDirectoryBox");

    private TextBox BibMapPathInput => Get<TextBox>("BibMapPathBox");

    private TextBox EventFilterInput => Get<TextBox>("EventFilterBox");

    private TextBox SscOrganizationNameInput => Get<TextBox>("SscOrganizationNameBox");

    private TextBox SscOrganizationIdInput => Get<TextBox>("SscOrganizationIdBox");

    private TextBox SscBibMapPathInput => Get<TextBox>("SscBibMapPathBox");

    private TextBox SscOutputDirectoryInput => Get<TextBox>("SscOutputDirectoryBox");

    private TextBox SscUsersCsvPathInput => Get<TextBox>("SscUsersCsvPathBox");

    private TextBox SscStartlagInput => Get<TextBox>("SscStartlagBox");

    private ComboBox SscLaneCountInput => Get<ComboBox>("SscLaneCountBox");

    private ComboBox SscStevneInput => Get<ComboBox>("SscStevneBox");

    private StackPanel WritebackClassRowsContainer => Get<StackPanel>("WritebackClassRowsPanel");

    private Button OpenLogButtonControl => Get<Button>("OpenLogButton");

    private TextBlock LatestLogLabelControl => Get<TextBlock>("LatestLogText");

    private TextBlock StatusLabel => Get<TextBlock>("StatusText");

    private Border StatusBadgeControl => Get<Border>("StatusBadge");

    private TextBlock EventActionHelpLabel => Get<TextBlock>("EventActionHelpText");

    private TextBlock CsvActionHelpLabel => Get<TextBlock>("CsvActionHelpText");

    private TextBlock WritebackActionHelpLabel => Get<TextBlock>("WritebackActionHelpText");

    private TextBlock WritebackScanSummaryLabel => Get<TextBlock>("WritebackScanSummaryText");

    private TextBlock StevneResultCountLabel => Get<TextBlock>("StevneResultCountText");

    private StackPanel CsvPreflightRowsContainer => Get<StackPanel>("CsvPreflightRowsPanel");

    private TextBlock CsvPreflightSummaryLabel => Get<TextBlock>("CsvPreflightSummaryText");

    private StackPanel SscStatusRowsContainer => Get<StackPanel>("SscStatusRowsPanel");

    private StackPanel RecentStevnerRowsContainer => Get<StackPanel>("RecentStevnerRowsPanel");

    private StackPanel SelectedOvelserRowsContainer => Get<StackPanel>("SelectedOvelserRowsPanel");

    private T Get<T>(string name)
        where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"Missing control '{name}'.");

    private void InitializeDefaults()
    {
        StevneIdsInput.Text = string.Empty;
        SiusRankFolderInput.Text = @"C:\SIUS\SiusRank";
        OutputDirectoryInput.Text = "./siusrank-import";
        SscOrganizationNameInput.Text = "Legacy";
        SscOrganizationIdInput.Text = "f95a2bc3-79bd-4c24-98b6-4e17f99bbfaf";
        SscOutputDirectoryInput.Text = "./ssc-setup";
        SscBibMapPathInput.Text = "./siusrank-import/" + ChampionshipStartNumbers.BibMapFileName;
        SscUsersCsvPathInput.Text = Path.Combine(SscOutputDirectoryInput.Text, "ssc-users.csv");
        SscStartlagInput.Text = "2026-07-06T09:00:00";
        EncodingInput.SelectedIndex = 0;

        try
        {
            var settings = AppSettings.Load();
            DatabasePathInput.Text = settings.ResolveDatabasePath();
            var templatePath = settings.ResolveShooterGroupsTemplatePath();
            if (File.Exists(templatePath))
            {
                ShooterGroupsTemplateInput.Text = templatePath;
            }
        }
        catch
        {
            // The UI stays usable even when appsettings.json is missing or invalid.
        }

        var desktopSettings = DesktopSettings.Load();
        _desktopSettings = desktopSettings;
        SetTextIfPresent(DatabasePathInput, desktopSettings.Global.DefaultDatabasePath);
        SetTextIfPresent(SiusRankFolderInput, desktopSettings.Global.SiusRankFolder);
        SetEncoding(desktopSettings.Global.EncodingName);
        SetTextIfPresent(EventFilePathInput, desktopSettings.Recent.LastEventFilePath);
        if (!string.IsNullOrWhiteSpace(desktopSettings.Recent.LastEventFilePath))
        {
            _currentEventFilePath = desktopSettings.Recent.LastEventFilePath;
        }
        OvelseSelectInput.ItemsSource = new[] { OvelseChoice.All };
        OvelseSelectInput.SelectedItem = OvelseChoice.All;
    }

    private void WireEvents()
    {
        Get<MenuItem>("CreateEventMenuItem").Click += async (_, _) => await RunSafelyAsync("Oppretter event.json", CreateEventFileAsync);
        Get<MenuItem>("OpenEventMenuItem").Click += async (_, _) => await RunSafelyAsync("Åpner event.json", OpenEventFileAsync);
        Get<MenuItem>("SaveEventMenuItem").Click += async (_, _) => await RunSafelyAsync("Lagrer event.json", SaveEventFileAsync);
        Get<MenuItem>("SettingsMenuItem").Click += async (_, _) => await RunSafelyAsync("Åpner innstillinger", ShowSettingsWindowAsync);
        Get<Button>("CreateEventButton").Click += async (_, _) => await RunSafelyAsync("Oppretter event.json", CreateEventFileAsync);
        Get<Button>("OpenEventButton").Click += async (_, _) => await RunSafelyAsync("Åpner event.json", OpenEventFileAsync);
        Get<Button>("SaveEventButton").Click += async (_, _) => await RunSafelyAsync("Lagrer event.json", SaveEventFileAsync);

        Get<Button>("BrowseDatabaseButton").Click += async (_, _) =>
            await BrowseFileAsync(DatabasePathInput, "Select storage.db3", "SQLite database", ["*.db3", "*.sqlite", "*.sqlite3"]);
        Get<Button>("BrowseShooterGroupsButton").Click += async (_, _) =>
            await BrowseFileAsync(ShooterGroupsTemplateInput, "Select ShooterGroupsTemplate.xml", "XML", ["*.xml"]);
        Get<Button>("BrowseOutputButton").Click += async (_, _) =>
        {
            await BrowseFolderAsync(OutputDirectoryInput, "Select SIUS Rank import output directory");
            NormalizeOutputDirectoryInput();
        };
        Get<Button>("BrowseSiusRankFolderButton").Click += async (_, _) =>
            await BrowseFolderAsync(SiusRankFolderInput, "Select SIUS Rank directory", useEventRelativePath: false);
        Get<Button>("BrowseExportsButton").Click += async (_, _) =>
            await BrowseFolderAsync(ExportsDirectoryInput, "Select SIUS Rank Exports directory");
        Get<Button>("BrowseSscBibMapButton").Click += async (_, _) =>
            await BrowseFileAsync(SscBibMapPathInput, "Select bib-map.csv", "CSV", ["*.csv"]);
        Get<Button>("BrowseSscOutputButton").Click += async (_, _) =>
            await BrowseFolderAsync(SscOutputDirectoryInput, "Select SSC setup output directory");
        Get<Button>("BrowseSscUsersCsvButton").Click += async (_, _) =>
            await BrowseFileAsync(SscUsersCsvPathInput, "Select SSC users CSV", "CSV", ["*.csv"]);

        Get<Button>("LoadDatabaseButton").Click += async (_, _) => await RunSafelyAsync("Kontrollerer database", InspectDatabaseAsync);
        Get<Button>("SearchStevnerButton").Click += async (_, _) => await RunSafelyAsync("Søker etter stevner", SearchStevnerAsync);
        Get<Button>("RefreshOvelserButton").Click += async (_, _) => await RunSafelyAsync("Laster øvelser", RefreshOvelseChoicesAsync);
        Get<Button>("RefreshEventClassesButton").Click += async (_, _) => await RunSafelyAsync("Oppdaterer klasser", RefreshEventClassesAsync);
        Get<Button>("SelectAllStevnerButton").Click += (_, _) => SetAllSearchResultSelections(isSelected: true);
        Get<Button>("ClearStevnerButton").Click += (_, _) => SetAllSearchResultSelections(isSelected: false);
        Get<Button>("CopyTemplatesButton").Click += async (_, _) => await RunSafelyAsync("Kopierer templates", CopyTemplatesToSiusRankAsync);
        Get<Button>("CreateBibMapButton").Click += async (_, _) => await RunSafelyAsync("Oppretter bib-map.csv", RunCreateBibMapAsync);
        Get<Button>("RunExportButton").Click += async (_, _) => await RunSafelyAsync("Lager CSV-filer", RunExportAsync);
        Get<Button>("RunWritebackPreviewButton").Click += async (_, _) => await RunSafelyAsync("Tørrkjører writeback", () => RunWritebackAsync(apply: false));
        Get<Button>("RunWritebackApplyButton").Click += async (_, _) => await RunSafelyAsync("Skriver til inrX", () => RunWritebackAsync(apply: true));
        Get<Button>("ScanWritebackResultsButton").Click += async (_, _) => await RunSafelyAsync("Finner resultater", ScanWritebackResultsAsync);
        Get<Button>("OpenEventFromWritebackButton").Click += async (_, _) => await RunSafelyAsync("Åpner event.json", OpenEventFileAsync);
        Get<Button>("ValidateAllWritebackButton").Click += async (_, _) => await RunSafelyAsync("Validerer alle", ValidateAllWritebackAsync);
        Get<Button>("DryRunReadyWritebackButton").Click += async (_, _) => await RunSafelyAsync("Tørrkjører klare", DryRunReadyWritebackAsync);
        Get<Button>("ApplyValidatedWritebackButton").Click += async (_, _) => await RunSafelyAsync("Skriver validerte til inrX", ApplyValidatedWritebackAsync);
        Get<Button>("RunSscUsersButton").Click += async (_, _) => await RunSafelyAsync("Eksporterer SSC-brukere", RunSscUsersAsync);
        Get<Button>("RunSscValidateButton").Click += async (_, _) => await RunSafelyAsync("Validerer SSC", RunSscValidateAsync);
        Get<Button>("RunSscLanesButton").Click += async (_, _) => await RunSafelyAsync("Eksporterer SSC baner/reset", RunSscLanesAsync);
        Get<Button>("ShowStevnerButton").Click += async (_, _) => await RunSafelyAsync("Laster stevner", ShowRecentStevnerAsync);
        Get<Button>("ShowSelectedOvelserButton").Click += async (_, _) => await RunSafelyAsync("Laster øvelser", ShowSelectedOvelserAsync);
        OpenLogButtonControl.Click += (_, _) => ShowLogWindow();
        OvelseSelectInput.SelectionChanged += (_, _) =>
        {
            if (_updatingOvelseSelection)
            {
                return;
            }

            if (OvelseSelectInput.SelectedItem is OvelseChoice { IsAll: false } choice)
            {
                OvelseFilterInput.Text = choice.Id.ToString(CultureInfo.InvariantCulture);
            }
            else if (OvelseSelectInput.SelectedItem is OvelseChoice { IsAll: true })
            {
                OvelseFilterInput.Text = string.Empty;
            }

            QueueCsvPreflightRefresh();
            UpdateActionStates();
        };

        EncodingInput.SelectionChanged += (_, _) => UpdateActionStates();
        SilhouetteShootersPerStandInput.SelectionChanged += (_, _) => UpdateActionStates();
        SscLaneCountInput.SelectionChanged += (_, _) => UpdateActionStates();
        SscStevneInput.SelectionChanged += (_, _) => UpdateActionStates();
        WireActionStateTextChanges();
    }

    private void WireActionStateTextChanges()
    {
        foreach (var input in new[]
        {
            DatabasePathInput,
            StevneIdsInput,
            SiusRankFolderInput,
            OutputDirectoryInput,
            ShooterGroupsTemplateInput,
            OvelseFilterInput,
            ExportsDirectoryInput,
            BibMapPathInput,
            EventFilterInput,
            SscBibMapPathInput,
            SscOutputDirectoryInput,
            SscUsersCsvPathInput,
            SscStartlagInput,
            SscOrganizationNameInput,
            SscOrganizationIdInput
        })
        {
            input.TextChanged += (_, _) =>
            {
                if (ReferenceEquals(input, DatabasePathInput) ||
                    ReferenceEquals(input, StevneIdsInput) ||
                    ReferenceEquals(input, OvelseFilterInput))
                {
                    QueueCsvPreflightRefresh();
                }

                if (ReferenceEquals(input, StevneIdsInput))
                {
                    RefreshSscStevneChoices();
                }

                UpdateActionStates();
            };
        }
    }

    private async Task CreateEventFileAsync()
    {
        var databasePath = RequireExistingFile(DatabasePathInput.Text, "storage.db3");
        var ovelse = RequireSelectedOvelse();
        var ids = ParseIdList(StevneIdsInput.Text, "Stevne ids");
        var parentFolders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select parent directory for the exercise folder",
            AllowMultiple = false
        });
        if (parentFolders.Count == 0 || parentFolders[0].TryGetLocalPath() is not { } parentDirectory)
        {
            AppendLog("Create event.json cancelled.");
            return;
        }

        var eventDirectory = Path.Combine(parentDirectory, EventProjectPlanner.SanitizePathPart(ovelse.Name));
        Directory.CreateDirectory(eventDirectory);
        var eventPath = Path.Combine(eventDirectory, EventProjectFile.FileName);

        EventProjectConfig config;
        using (var repository = new InrxRepository(databasePath))
        {
            config = EventProjectPlanner.Build(
                repository,
                databasePath,
                ids,
                ovelse,
                SelectedSilhouetteShootersPerStand(),
                RequireText(SiusRankFolderInput.Text, "SIUS Rank folder"));
        }

        config = config with
        {
            Inrx = config.Inrx with { Db = EventProjectFile.ToStoredPath(eventPath, databasePath) },
            Csv = new EventCsvConfig { Output = "./siusrank-import" }
        };

        CreateEventDirectories(eventPath, config);
        EventProjectFile.Save(eventPath, config);
        await ApplyEventConfigAsync(eventPath, config);
        AppendLog($"event.json created: {eventPath}");
    }

    private async Task OpenEventFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open event.json",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("event.json") { Patterns = ["event.json", "*.json"] },
                FilePickerFileTypes.All
            ]
        });
        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path)
        {
            AppendLog("Open event.json cancelled.");
            return;
        }

        var config = EventProjectFile.Load(path);
        await ApplyEventConfigAsync(path, config);
        AppendLog($"event.json opened: {path}");
    }

    private async Task SaveEventFileAsync()
    {
        var path = _currentEventFilePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save event.json",
                SuggestedFileName = EventProjectFile.FileName,
                FileTypeChoices =
                [
                    new FilePickerFileType("JSON") { Patterns = ["*.json"] }
                ]
            });
            path = file?.TryGetLocalPath();
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            AppendLog("Save event.json cancelled.");
            return;
        }

        var config = BuildEventConfigFromUi(path, refreshClasses: _currentEventConfig is null || _currentEventConfig.Classes.Count == 0);
        EventProjectFile.Save(path, config);
        await ApplyEventConfigAsync(path, config);
        AppendLog($"event.json saved: {path}");
    }

    private async Task ApplyEventConfigAsync(string eventPath, EventProjectConfig config)
    {
        _currentEventFilePath = Path.GetFullPath(eventPath);
        _currentEventConfig = config;
        _writebackRows.Clear();
        _writebackStatuses.Clear();
        WritebackScanSummaryLabel.Text = "Trykk Finn resultater i stevnemappen.";
        EventFilePathInput.Text = _currentEventFilePath;
        EventFilterInput.Text = string.Empty;
        DatabasePathInput.Text = ToEventDisplayPath(EventProjectFile.ResolvePath(_currentEventFilePath, config.Inrx.Db));
        StevneIdsInput.Text = config.Inrx.Stevner;
        SiusRankFolderInput.Text = ToEventDisplayPath(EventProjectFile.ResolvePath(_currentEventFilePath, config.SiusRankFolder));
        OutputDirectoryInput.Text = ToProjectOutputDisplayPath(config.Csv.Output);
        ExportsDirectoryInput.Text = string.Empty;
        RefreshSscStevneChoices();

        var bibMapPath = Path.Combine(ResolveEventPath(OutputDirectoryInput.Text), ChampionshipStartNumbers.BibMapFileName);
        BibMapPathInput.Text = ToEventDisplayPath(bibMapPath);
        SscBibMapPathInput.Text = ToEventDisplayPath(bibMapPath);
        SscOutputDirectoryInput.Text = "./ssc-setup";
        SscUsersCsvPathInput.Text = "./ssc-setup/ssc-users.csv";
        if (!string.IsNullOrWhiteSpace(ShooterGroupsTemplateInput.Text))
        {
            ShooterGroupsTemplateInput.Text = ToEventDisplayPath(ShooterGroupsTemplateInput.Text);
        }
        SetSilhouetteShootersPerStand(config.Silhouette.ShootersPerStand);
        LoadEventTypeSelections(config.EventTypes);
        await RefreshOvelseChoicesAsync(config.Exercise.Id);
        await SearchSelectedStevnerAsync();
        RenderWritebackRows(config);
        SaveDesktopSettings();
        UpdateActionStates();
    }

    private Task SearchStevnerAsync()
    {
        var databasePath = RequireExistingFile(DatabasePathInput.Text, "storage.db3");
        var filter = CleanSetting(StevneSearchInput.Text);
        return Task.Run(() =>
        {
            using var repository = new InrxRepository(databasePath);
            var rows = repository.SearchStevner(filter, limit: 100)
                .Select(stevne =>
                {
                    var ovelser = repository.GetOvelserForStevne(stevne.Id);
                    return new StevneChoice(
                        stevne.Id,
                        stevne.Name,
                        stevne.Date,
                        repository.GetStevneEventType(stevne.Id),
                        string.Join(", ", ovelser.Select(ovelse => ovelse.Name)));
                })
                .ToList();

            Dispatcher.UIThread.Post(() => RenderStevneChoices(rows));
        });
    }

    private Task SearchSelectedStevnerAsync()
    {
        if (!HasExistingFile(DatabasePathInput.Text) ||
            !TryParseOptionalIds(StevneIdsInput.Text, out var ids) ||
            ids.Count == 0)
        {
            return Task.CompletedTask;
        }

        var databasePath = RequireExistingFile(DatabasePathInput.Text, "storage.db3");
        return Task.Run(() =>
        {
            using var repository = new InrxRepository(databasePath);
            var rows = ids
                .Distinct()
                .Select(id =>
                {
                    var stevne = repository.GetStevneById(id);
                    var ovelser = repository.GetOvelserForStevne(stevne.Id);
                    return new StevneChoice(
                        stevne.Id,
                        stevne.Name,
                        stevne.Date,
                        repository.GetStevneEventType(stevne.Id),
                        string.Join(", ", ovelser.Select(ovelse => ovelse.Name)));
                })
                .ToList();

            Dispatcher.UIThread.Post(() =>
            {
                StevneSearchInput.Text = string.Empty;
                RenderStevneChoices(rows);
            });
            AppendLog($"Stevnesøk: lastet {rows.Count} valgte stevner fra event.json.");
        });
    }

    private Task RefreshOvelseChoicesAsync() => RefreshOvelseChoicesAsync(selectedOvelseId: null);

    private Task RefreshOvelseChoicesAsync(int? selectedOvelseId)
    {
        var databasePath = RequireExistingFile(DatabasePathInput.Text, "storage.db3");
        var ids = ParseOptionalIdList(StevneIdsInput.Text);
        if (ids.Count == 0)
        {
            Dispatcher.UIThread.Post(() =>
            {
                OvelseSelectInput.ItemsSource = new[] { OvelseChoice.All };
                OvelseSelectInput.SelectedItem = null;
                OvelseFilterInput.Text = string.Empty;
                ClearCsvPreflight("Velg minst ett Stevne.Id for CSV preflight.");
                UpdateActionStates();
            });
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            using var repository = new InrxRepository(databasePath);
            var choices = ids
                .SelectMany(repository.GetOvelserForStevne)
                .GroupBy(ovelse => ovelse.Id)
                .Select(group =>
                {
                    var first = group.First();
                    return new OvelseChoice(
                        first.Id,
                        first.Name,
                        first.ShortName,
                        first.HovedOvelseId,
                        group.Sum(ovelse => ovelse.StarterCount));
                })
                .OrderBy(choice => choice.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Dispatcher.UIThread.Post(() =>
            {
                var allChoices = new[] { OvelseChoice.All }.Concat(choices).ToList();
                OvelseSelectInput.ItemsSource = allChoices;
                var selected = ResolveSelectedOvelseChoice(allChoices, selectedOvelseId);
                _updatingOvelseSelection = true;
                try
                {
                    OvelseSelectInput.SelectedItem = selected;
                    OvelseFilterInput.Text = selected is { IsAll: false }
                        ? selected.Id.ToString(CultureInfo.InvariantCulture)
                        : string.Empty;
                }
                finally
                {
                    _updatingOvelseSelection = false;
                }

                QueueCsvPreflightRefresh();
                UpdateActionStates();
            });
        });
    }

    private OvelseChoice ResolveSelectedOvelseChoice(IReadOnlyList<OvelseChoice> choices, int? selectedOvelseId)
    {
        if (selectedOvelseId is not null)
        {
            return choices.FirstOrDefault(choice => !choice.IsAll && choice.Id == selectedOvelseId.Value) ?? OvelseChoice.All;
        }

        if (int.TryParse(OvelseFilterInput.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var persistedId))
        {
            return choices.FirstOrDefault(choice => !choice.IsAll && choice.Id == persistedId) ?? OvelseChoice.All;
        }

        return OvelseChoice.All;
    }

    private Task RefreshEventClassesAsync()
    {
        var path = _currentEventFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), EventProjectFile.FileName);
        var config = BuildEventConfigFromUi(path, refreshClasses: true);
        _currentEventConfig = config;
        RenderWritebackRows(config);
        AppendLog("Event-oppsett oppdatert.");
        return Task.CompletedTask;
    }

    private async Task BrowseFileAsync(
        TextBox target,
        string title,
        string typeName,
        IReadOnlyList<string> patterns,
        bool useEventRelativePath = true)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(typeName) { Patterns = patterns },
                FilePickerFileTypes.All
            ]
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            target.Text = useEventRelativePath ? ToEventDisplayPath(path) : path;
            AppendOutsideEventPathWarning(path);
            SaveDesktopSettings();
            UpdateActionStates();
        }
    }

    private async Task BrowseFolderAsync(TextBox target, string title, bool useEventRelativePath = true)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            target.Text = useEventRelativePath ? ToEventDisplayPath(path) : path;
            if (useEventRelativePath)
            {
                AppendOutsideEventPathWarning(path);
            }

            SaveDesktopSettings();
            UpdateActionStates();
        }
    }

    private async Task ShowSettingsWindowAsync()
    {
        var values = new GlobalSettingsValues(
            _desktopSettings.Global.EncodingName ?? SelectedEncoding(),
            _desktopSettings.Global.SiusRankFolder ?? SiusRankFolderInput.Text?.Trim() ?? string.Empty,
            _desktopSettings.Global.DefaultDatabasePath ?? (string.IsNullOrWhiteSpace(_currentEventFilePath) ? DatabasePathInput.Text?.Trim() : null) ?? string.Empty);
        var dialog = new SettingsWindow(values);
        var result = await dialog.ShowDialog<GlobalSettingsValues?>(this);
        if (result is null)
        {
            return;
        }

        _desktopSettings = BuildDesktopSettings() with
        {
            Global = new GlobalDesktopSettings
            {
                EncodingName = result.EncodingName,
                SiusRankFolder = CleanSetting(result.SiusRankFolder),
                DefaultDatabasePath = CleanSetting(result.DefaultDatabasePath)
            }
        };
        _desktopSettings.Save();

        SetEncoding(result.EncodingName);
        SiusRankFolderInput.Text = result.SiusRankFolder;
        if (string.IsNullOrWhiteSpace(_currentEventFilePath))
        {
            DatabasePathInput.Text = result.DefaultDatabasePath;
        }

        AppendLog("Innstillinger lagret.");
        UpdateActionStates();
    }

    private void AppendOutsideEventPathWarning(string path)
    {
        if (!string.IsNullOrWhiteSpace(_currentEventFilePath) &&
            !DesktopEventPaths.IsInsideEventDirectory(_currentEventFilePath, path))
        {
            AppendLog("Filen ligger utenfor stevnemappen og lagres som absolutt sti.");
        }
    }

    private async Task RunSafelyAsync(string status, Func<Task> action)
    {
        _isRunning = true;
        SetStatus(RunStatus.Running, status);
        UpdateActionStates();
        SaveDesktopSettings();
        try
        {
            await action();
            SetStatus(RunStatus.Ready);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException or System.Xml.XmlException)
        {
            AppendLog($"ERROR: {FormatUiExceptionMessage(ex)}");
            SetStatus(RunStatus.Error, ex.Message);
        }
        catch (Exception ex)
        {
            AppendLog($"ERROR: {ex}");
            SetStatus(RunStatus.Error, ex.Message);
        }
        finally
        {
            _isRunning = false;
            UpdateActionStates();
        }
    }

    private string FormatUiExceptionMessage(Exception ex)
    {
        var message = ex.Message;
        var match = Regex.Match(message, @"No starters found for Stevne\.Id=(\d+) and OvelseDef\.Id=(\d+)\.");
        if (!match.Success)
        {
            return message;
        }

        var stevneId = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var ovelseId = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var ovelseName = TryLookupOvelseName(ovelseId) ?? $"OvelseDef.Id {ovelseId}";
        return $"Ingen startere funnet for Stevne.Id {stevneId} / {ovelseName}. " +
               "Velg 'Alle øvelser i valgte stevner' eller fjern stevner uten denne øvelsen. " +
               $"Detalj: {message}";
    }

    private string? TryLookupOvelseName(int ovelseId)
    {
        if (!HasExistingFile(DatabasePathInput.Text))
        {
            return null;
        }

        try
        {
            using var repository = new InrxRepository(RequireExistingFile(DatabasePathInput.Text, "storage.db3"));
            return repository.GetOvelseById(ovelseId).Name;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or Microsoft.Data.Sqlite.SqliteException)
        {
            return null;
        }
    }

    private Task InspectDatabaseAsync()
    {
        var databasePath = RequireExistingFile(DatabasePathInput.Text, "storage.db3");
        return Task.Run(() =>
        {
            using var repository = new InrxRepository(databasePath);
            var stevner = repository.SearchStevner(null, limit: 25);

            var builder = new StringBuilder();
            builder.AppendLine("Recent stevner:");
            foreach (var stevne in stevner)
            {
                builder.AppendLine($"  {stevne.Id,4}  {FormatDate(stevne.Date),10}  {stevne.Name}");
            }

            AppendLog(builder.ToString());
        });
    }

    private Task ShowRecentStevnerAsync()
    {
        var databasePath = RequireExistingFile(DatabasePathInput.Text, "storage.db3");
        return Task.Run(() =>
        {
            using var repository = new InrxRepository(databasePath);
            var rows = repository.SearchStevner(null, limit: 25)
                .Select(stevne =>
                {
                    var ovelser = repository.GetOvelserForStevne(stevne.Id);
                    return new DiagnosticStevneRow(
                        FormatDate(stevne.Date),
                        stevne.Id,
                        stevne.Name,
                        repository.GetStevneEventType(stevne.Id),
                        ovelser.Count,
                        ovelser.Sum(ovelse => ovelse.StarterCount));
                })
                .ToList();

            Dispatcher.UIThread.Post(() => RenderDiagnosticStevner(rows));
            AppendLog($"Diagnostikk: lastet {rows.Count} stevner.");
        });
    }

    private Task ShowSelectedOvelserAsync()
    {
        var databasePath = RequireExistingFile(DatabasePathInput.Text, "storage.db3");
        var ids = ParseIdList(StevneIdsInput.Text, "Stevne ids");
        return Task.Run(() =>
        {
            using var repository = new InrxRepository(databasePath);
            var rows = new List<DiagnosticOvelseRow>();
            foreach (var id in ids)
            {
                var stevne = repository.GetStevneById(id);
                foreach (var ovelse in repository.GetOvelserForStevne(id))
                {
                    rows.Add(new DiagnosticOvelseRow(
                        FormatDate(stevne.Date),
                        stevne.Id,
                        stevne.Name,
                        ovelse.Id,
                        ovelse.Name,
                        ovelse.ShortName,
                        ovelse.StarterCount));
                }
            }

            Dispatcher.UIThread.Post(() => RenderDiagnosticOvelser(rows));
            AppendLog($"Diagnostikk: lastet {rows.Count} øvelsesrader.");
        });
    }

    private void RenderStevneChoices(IReadOnlyList<StevneChoice> rows)
    {
        var selectedIds = ParseOptionalIdList(StevneIdsInput.Text).ToHashSet();
        _stevneChecks.Clear();
        _stevneChoices.Clear();
        _eventTypeInputs.Clear();
        StevneListContainer.Children.Clear();
        StevneResultCountLabel.Text = rows.Count == 1
            ? "1 treff."
            : $"{rows.Count} treff.";

        foreach (var row in rows)
        {
            _stevneChoices[row.Id] = row;
            var checkBox = new CheckBox
            {
                IsChecked = selectedIds.Contains(row.Id),
                VerticalAlignment = VerticalAlignment.Center
            };
            checkBox.IsCheckedChanged += (_, _) =>
            {
                if (_updatingStevneChecks)
                {
                    return;
                }

                UpdateSelectedStevneIdsFromChecks();
                var ids = ParseOptionalIdList(StevneIdsInput.Text);
                if (ids.Count > 0)
                {
                    _ = RefreshOvelseChoicesAsync();
                }

                UpdateActionStates();
            };
            _stevneChecks[row.Id] = checkBox;
            StevneListContainer.Children.Add(CreateStevneRow(row, checkBox));
        }

        RefreshSscStevneChoices();
        UpdateActionStates();
    }

    private Border CreateStevneRow(StevneChoice row, CheckBox checkBox)
    {
        var typeInput = CreateEventTypeComboBox(row.Id, row.EventType);
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("32,92,60,2*,130,2*"),
            ColumnSpacing = 8
        };
        grid.Children.Add(checkBox);
        AddRowText(grid, 1, FormatDate(row.Date));
        AddRowText(grid, 2, row.Id.ToString(CultureInfo.InvariantCulture));
        AddRowText(grid, 3, row.Name);
        Grid.SetColumn(typeInput, 4);
        grid.Children.Add(typeInput);
        AddRowText(grid, 5, row.Ovelser);

        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#d8dee4")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 5),
            Child = grid
        };
    }

    private ComboBox CreateEventTypeComboBox(int stevneId, string repositoryEventType)
    {
        var selectedType = ResolveEventTypeSelection(stevneId, repositoryEventType);
        _eventTypeSelections[stevneId] = selectedType;

        var comboBox = new ComboBox
        {
            Width = 124,
            MinHeight = 28,
            ItemsSource = new[] { EventProjectPlanner.ApprovedEventType, EventProjectPlanner.ChampionshipEventType },
            SelectedItem = selectedType
        };
        comboBox.SelectionChanged += (_, _) =>
        {
            _eventTypeSelections[stevneId] = DesktopEventTypeSelections.Normalize(comboBox.SelectedItem?.ToString());
            QueueCsvPreflightRefresh();
            UpdateActionStates();
        };
        _eventTypeInputs[stevneId] = comboBox;
        return comboBox;
    }

    private static Border CreateTableRow(Grid grid) =>
        new()
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#d8dee4")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 5),
            Child = grid
        };

    private static void AddRowText(Grid grid, int column, string text, bool wrap = false)
    {
        var block = new TextBlock
        {
            Text = text,
            TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }

    private void SetAllSearchResultSelections(bool isSelected)
    {
        _updatingStevneChecks = true;
        try
        {
            foreach (var checkBox in _stevneChecks.Values)
            {
                checkBox.IsChecked = isSelected;
            }
        }
        finally
        {
            _updatingStevneChecks = false;
        }

        UpdateSelectedStevneIdsFromChecks();
        var ids = ParseOptionalIdList(StevneIdsInput.Text);
        if (ids.Count > 0)
        {
            _ = RefreshOvelseChoicesAsync();
        }
        else
        {
            OvelseSelectInput.SelectedItem = null;
            OvelseFilterInput.Text = string.Empty;
            ClearCsvPreflight("Velg minst ett Stevne.Id for CSV preflight.");
        }

        QueueCsvPreflightRefresh();
        UpdateActionStates();
    }

    private void QueueCsvPreflightRefresh()
    {
        var version = ++_csvPreflightRefreshVersion;
        _ = RefreshCsvPreflightLaterAsync(version);
    }

    private async Task RefreshCsvPreflightLaterAsync(int version)
    {
        await Task.Delay(250);
        if (version != _csvPreflightRefreshVersion)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (version == _csvPreflightRefreshVersion)
            {
                await RefreshCsvPreflightAsync();
            }
        });
    }

    private async Task RefreshCsvPreflightAsync()
    {
        if (!HasExistingFile(DatabasePathInput.Text))
        {
            ClearCsvPreflight("Velg storage.db3 for CSV preflight.");
            return;
        }

        if (!TryParseOptionalIds(StevneIdsInput.Text, out var ids) || ids.Count == 0)
        {
            ClearCsvPreflight("Velg Stevne.Id for CSV preflight.");
            return;
        }

        var databasePath = RequireExistingFile(DatabasePathInput.Text, "storage.db3");

        var selection = SelectedCsvExerciseSelection();
        var rememberedEventTypes = new Dictionary<int, string>(_eventTypeSelections);
        var visibleEventTypes = _eventTypeInputs.ToDictionary(
            item => item.Key,
            item => item.Value.SelectedItem?.ToString());
        var loadedEventTypes = _currentEventConfig?.EventTypes;
        try
        {
            var events = await Task.Run(() =>
            {
                using var repository = new InrxRepository(databasePath);
                return ids
                    .Select(id =>
                    {
                        var stevne = repository.GetStevneById(id);
                        var exercises = repository.GetOvelserForStevne(id)
                            .Select(ovelse => new CsvPreflightExerciseInput(
                                ovelse.Id,
                                ovelse.Name,
                                ovelse.ShortName,
                                ovelse.HovedOvelseId,
                                ovelse.StarterCount))
                            .ToList();
                        return new CsvPreflightEventInput(
                            stevne.Id,
                            stevne.Name,
                            FormatDate(stevne.Date),
                            DesktopEventTypeSelections.ResolveEffective(
                                stevne.Id,
                                rememberedEventTypes,
                                visibleEventTypes,
                                loadedEventTypes,
                                repository.GetStevneEventType(stevne.Id)),
                            exercises);
                    })
                    .ToList();
            });

            _csvPreflight = DesktopCsvPreflight.Build(events, selection);
            RenderCsvPreflight(_csvPreflight);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or Microsoft.Data.Sqlite.SqliteException)
        {
            ClearCsvPreflight($"CSV preflight feilet: {ex.Message}");
        }

        UpdateActionStates();
    }

    private void ClearCsvPreflight(string message)
    {
        _csvPreflight = null;
        CsvPreflightRowsContainer.Children.Clear();
        CsvPreflightSummaryLabel.Text = message;
    }

    private void RenderCsvPreflight(CsvPreflightResult result)
    {
        CsvPreflightRowsContainer.Children.Clear();
        foreach (var row in result.Rows)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("70,92,72,1.5*,100,1.2*,90,70,1.6*"),
                ColumnSpacing = 8
            };
            grid.Children.Add(new CheckBox
            {
                IsChecked = row.Include,
                IsEnabled = false,
                VerticalAlignment = VerticalAlignment.Center
            });
            AddRowText(grid, 1, row.Date);
            AddRowText(grid, 2, row.StevneId.ToString(CultureInfo.InvariantCulture));
            AddRowText(grid, 3, row.StevneName);
            AddRowText(grid, 4, row.EventType);
            AddRowText(grid, 5, row.OvelseName);
            AddRowText(grid, 6, row.OvelseId?.ToString(CultureInfo.InvariantCulture) ?? "-");
            AddRowText(grid, 7, row.StarterCount.ToString(CultureInfo.InvariantCulture));
            AddRowText(grid, 8, row.Status);
            CsvPreflightRowsContainer.Children.Add(CreateTableRow(grid));
        }

        CsvPreflightSummaryLabel.Text = result.CanExport
            ? $"{result.Rows.Count(row => row.Include)} rader klare, {result.ExcludedStevneIds.Count} Stevne.Id hoppes over."
            : result.EmptyMessage;
    }

    private void AppendCsvSkippedMessage()
    {
        if (!string.IsNullOrWhiteSpace(_csvPreflight?.SkippedMessage))
        {
            AppendLog(_csvPreflight.SkippedMessage);
        }
    }

    private void RenderSscStatusRows(IReadOnlyList<SscActionStatusRow> rows)
    {
        SscStatusRowsContainer.Children.Clear();
        foreach (var row in rows)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("220,180,*"),
                ColumnSpacing = 8
            };
            AddRowText(grid, 0, row.Action);
            AddRowText(grid, 1, row.Status);
            AddRowText(grid, 2, row.NextStep, wrap: true);
            SscStatusRowsContainer.Children.Add(CreateTableRow(grid));
        }
    }

    private void RenderDiagnosticStevner(IReadOnlyList<DiagnosticStevneRow> rows)
    {
        RecentStevnerRowsContainer.Children.Clear();
        foreach (var row in rows)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("92,72,2*,100,100,100"),
                ColumnSpacing = 8
            };
            AddRowText(grid, 0, row.Date);
            AddRowText(grid, 1, row.StevneId.ToString(CultureInfo.InvariantCulture));
            AddRowText(grid, 2, row.Name);
            AddRowText(grid, 3, row.EventType);
            AddRowText(grid, 4, row.ExerciseCount.ToString(CultureInfo.InvariantCulture));
            AddRowText(grid, 5, row.StarterCount.ToString(CultureInfo.InvariantCulture));
            RecentStevnerRowsContainer.Children.Add(CreateTableRow(grid));
        }
    }

    private void RenderDiagnosticOvelser(IReadOnlyList<DiagnosticOvelseRow> rows)
    {
        SelectedOvelserRowsContainer.Children.Clear();
        foreach (var row in rows)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("92,72,1.8*,90,1.4*,90,80"),
                ColumnSpacing = 8
            };
            AddRowText(grid, 0, row.Date);
            AddRowText(grid, 1, row.StevneId.ToString(CultureInfo.InvariantCulture));
            AddRowText(grid, 2, row.StevneName);
            AddRowText(grid, 3, row.OvelseId.ToString(CultureInfo.InvariantCulture));
            AddRowText(grid, 4, row.OvelseName);
            AddRowText(grid, 5, row.ShortName);
            AddRowText(grid, 6, row.StarterCount.ToString(CultureInfo.InvariantCulture));
            SelectedOvelserRowsContainer.Children.Add(CreateTableRow(grid));
        }
    }

    private void UpdateSelectedStevneIdsFromChecks()
    {
        var ids = _stevneChecks
            .Where(item => item.Value.IsChecked == true)
            .Select(item => item.Key)
            .OrderBy(id => id)
            .ToList();
        StevneIdsInput.Text = ids.Count == 0 ? string.Empty : EventProjectPlanner.FormatIds(ids);
        SaveDesktopSettings();
    }

    private void RefreshSscStevneChoices()
    {
        var previousId = SelectedSscStevneId();
        var ids = TryParseOptionalIds(StevneIdsInput.Text, out var parsedIds)
            ? parsedIds
            : [];
        var options = ids
            .Select(id =>
            {
                var label = _stevneChoices.TryGetValue(id, out var choice)
                    ? $"{id} - {choice.Date} {choice.Name}"
                    : id.ToString(CultureInfo.InvariantCulture);
                return new SscStevneChoice(id, label);
            })
            .ToList();

        SscStevneInput.ItemsSource = options;
        SscStevneInput.SelectedItem =
            options.FirstOrDefault(option => option.Id == previousId) ??
            options.FirstOrDefault();
    }

    private void LoadEventTypeSelections(IReadOnlyDictionary<string, string> eventTypes)
    {
        foreach (var item in eventTypes)
        {
            if (int.TryParse(item.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                _eventTypeSelections[id] = DesktopEventTypeSelections.Normalize(item.Value);
            }
        }
    }

    private EventProjectConfig BuildEventConfigFromUi(string eventPath, bool refreshClasses)
    {
        var databasePath = RequireExistingFile(DatabasePathInput.Text, "storage.db3");
        var ids = ParseIdList(StevneIdsInput.Text, "Stevne ids");
        var ovelse = RequireSelectedOvelse();
        var siusRankFolder = RequireText(SiusRankFolderInput.Text, "SIUS Rank folder");
        var outputDirectory = NormalizeOutputDirectoryInput(eventPath);

        return new EventProjectConfig
        {
            Version = 1,
            Exercise = new EventExerciseConfig
            {
                Id = ovelse.Id,
                Name = ovelse.Name,
                ShortName = ovelse.ShortName,
                HovedOvelseId = ovelse.HovedOvelseId
            },
            Inrx = new EventInrxConfig
            {
                Db = EventProjectFile.ToStoredPath(eventPath, databasePath),
                Stevner = EventProjectPlanner.FormatIds(ids)
            },
            EventTypes = BuildEventTypeMap(ids),
            Silhouette = new EventSilhouetteConfig
            {
                ShootersPerStand = SelectedSilhouetteShootersPerStand()
            },
            SiusRankFolder = EventProjectFile.ToStoredPath(eventPath, siusRankFolder),
            Csv = new EventCsvConfig
            {
                Output = EventProjectFile.ToStoredPath(eventPath, ResolveEventPath(outputDirectory))
            },
            Classes = []
        };
    }

    private Dictionary<string, string> BuildEventTypeMap(IReadOnlyList<int> ids)
        => new(DesktopEventTypeSelections.Build(
            ids,
            _eventTypeSelections,
            _stevneChoices.ToDictionary(item => item.Key, item => item.Value.EventType),
            _currentEventConfig?.EventTypes), StringComparer.Ordinal);

    private string ResolveEventTypeSelection(int id, string? fallback)
        => GetEffectiveEventTypeForStevne(id, fallback);

    private string GetEffectiveEventTypeForStevne(int stevneId, string? repositoryFallback)
    {
        var visibleSelections = _eventTypeInputs.ToDictionary(
            item => item.Key,
            item => item.Value.SelectedItem?.ToString());
        return DesktopEventTypeSelections.ResolveEffective(
            stevneId,
            _eventTypeSelections,
            visibleSelections,
            _currentEventConfig?.EventTypes,
            repositoryFallback);
    }

    private OvelseInfo RequireSelectedOvelse()
    {
        if (OvelseSelectInput.SelectedItem is OvelseChoice { IsAll: false } choice)
        {
            if (choice.HovedOvelseId == 0 && string.IsNullOrWhiteSpace(choice.ShortName))
            {
                using var repository = new InrxRepository(RequireExistingFile(DatabasePathInput.Text, "storage.db3"));
                return repository.GetOvelseById(choice.Id);
            }

            return new OvelseInfo(choice.Id, choice.Name, choice.ShortName, choice.HovedOvelseId);
        }

        var ovelseFilter = OvelseFilterInput.Text?.Trim();
        if (int.TryParse(ovelseFilter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ovelseId))
        {
            using var repository = new InrxRepository(RequireExistingFile(DatabasePathInput.Text, "storage.db3"));
            return repository.GetOvelseById(ovelseId);
        }

        throw new ArgumentException("Choose an exercise before creating event.json.");
    }

    private int SelectedSilhouetteShootersPerStand() =>
        SilhouetteShootersPerStandInput.SelectedIndex == 0 ? 1 : 2;

    private void SetSilhouetteShootersPerStand(int shootersPerStand)
    {
        SilhouetteShootersPerStandInput.SelectedIndex = shootersPerStand == 1 ? 0 : 1;
    }

    private void CreateEventDirectories(string eventPath, EventProjectConfig config)
    {
        Directory.CreateDirectory(EventProjectFile.ResolvePath(eventPath, config.Csv.Output));
        var eventDirectory = Path.GetDirectoryName(Path.GetFullPath(eventPath)) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(Path.Combine(eventDirectory, "SiusData_files"));
    }

    private void RenderWritebackRows(EventProjectConfig config)
    {
        var rows = _writebackRows.Values
            .OrderBy(row => row.Class, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.ExportsDisplayPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        RenderWritebackRows(rows);
    }

    private void RenderWritebackRows(IReadOnlyList<DesktopWritebackDiscoveryRow> rows)
    {
        _writebackStatusLabels.Clear();
        _writebackValidateButtons.Clear();
        _writebackApplyButtons.Clear();
        WritebackClassRowsContainer.Children.Clear();
        if (_currentEventConfig is null)
        {
            var empty = new Grid { ColumnDefinitions = new ColumnDefinitions("*") };
            AddRowText(empty, 0, "Åpne event.json først.");
            WritebackClassRowsContainer.Children.Add(CreateTableRow(empty));
            return;
        }

        if (rows.Count == 0)
        {
            var empty = new Grid { ColumnDefinitions = new ColumnDefinitions("*") };
            AddRowText(empty, 0, "Trykk Finn resultater i stevnemappen.");
            WritebackClassRowsContainer.Children.Add(CreateTableRow(empty));
            return;
        }

        foreach (var discoveryRow in rows)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("70,150,1.6*,80,220"),
                ColumnSpacing = 8
            };
            AddRowText(row, 0, discoveryRow.Class);
            var status = new TextBlock
            {
                Text = WritebackDiscoveryStatusText(discoveryRow),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(status, 1);
            _writebackStatusLabels[discoveryRow.Class] = status;
            row.Children.Add(status);
            AddExportsCell(row, 2, discoveryRow);
            AddRowText(row, 3, discoveryRow.ExportsFullPath is null ? string.Empty : $"{discoveryRow.FileCount} filer");

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6
            };
            Grid.SetColumn(actions, 4);
            if (!string.IsNullOrWhiteSpace(discoveryRow.ExportsFullPath))
            {
                var openButton = new Button { Content = "Åpne" };
                openButton.Click += async (_, _) => await RunSafelyAsync(
                    $"Åpner {discoveryRow.Class}",
                    () => OpenFolderAsync(discoveryRow.ExportsFullPath));
                actions.Children.Add(openButton);
            }

            var validateButton = new Button { Content = "Tørrkjør" };
            validateButton.Click += async (_, _) => await RunSafelyAsync(
                $"Tørrkjører {discoveryRow.Class}",
                () => RunClassWritebackAsync(discoveryRow.Class, apply: false));
            _writebackValidateButtons[discoveryRow.Class] = validateButton;
            actions.Children.Add(validateButton);
            var writeButton = new Button { Content = "Skriv til inR" };
            writeButton.Classes.Add("danger");
            writeButton.Click += async (_, _) => await RunSafelyAsync(
                $"Skriver {discoveryRow.Class}",
                () => RunClassWritebackAsync(discoveryRow.Class, apply: true));
            _writebackApplyButtons[discoveryRow.Class] = writeButton;
            actions.Children.Add(writeButton);
            row.Children.Add(actions);
            WritebackClassRowsContainer.Children.Add(CreateTableRow(row));
        }

        UpdateActionStates();
    }

    private void AddExportsCell(Grid grid, int column, DesktopWritebackDiscoveryRow row)
    {
        if (row.Status == DesktopWritebackDiscoveryStatus.Ambiguous && row.Candidates.Count > 0)
        {
            var selector = new ComboBox
            {
                ItemsSource = row.Candidates,
                MinWidth = 220,
                SelectedIndex = -1
            };
            selector.SelectionChanged += (_, _) =>
            {
                if (selector.SelectedItem is DesktopDiscoveredExports selected)
                {
                    var updated = row with
                    {
                        ExportsDisplayPath = selected.DisplayPath,
                        ExportsFullPath = selected.FullPath,
                        FileCount = selected.FileCount,
                        Status = selected.FileCount == 0 ? DesktopWritebackDiscoveryStatus.NoFiles : DesktopWritebackDiscoveryStatus.Assumed,
                        Candidates = []
                    };
                    _writebackRows[row.Class] = updated;
                    _writebackStatuses.Remove(row.Class);
                    RenderWritebackRows(_currentEventConfig ?? throw new InvalidOperationException("event.json er ikke lastet."));
                }
            };
            Grid.SetColumn(selector, column);
            grid.Children.Add(selector);
            return;
        }

        var block = new TextBlock
        {
            Text = row.ExportsDisplayPath ?? "Trykk Finn resultater",
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (!string.IsNullOrWhiteSpace(row.ExportsFullPath))
        {
            ToolTip.SetTip(block, row.ExportsFullPath);
        }
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }

    private static string WritebackDiscoveryStatusText(DesktopWritebackDiscoveryRow row) =>
        row.Status switch
        {
            DesktopWritebackDiscoveryStatus.Ready => "Klar",
            DesktopWritebackDiscoveryStatus.Assumed => row.FileCount == 0 ? "Ingen ODF-filer" : "Antatt match",
            DesktopWritebackDiscoveryStatus.NoFiles => "Ingen ODF-filer",
            DesktopWritebackDiscoveryStatus.Ambiguous => "Flere mulige treff",
            _ => "Finner ikke resultater"
        };

    private static int CountOdfFiles(string exportsFolder) =>
        DesktopSiusRankExportsScanner.CountOdfFiles(exportsFolder);

    private static string BuildInitialWritebackStatus(string instanceFolder, string exportsFolder, int odfCount)
    {
        if (!Directory.Exists(instanceFolder))
        {
            return "Mangler SIUS Rank-kopi";
        }

        if (!Directory.Exists(exportsFolder))
        {
            return "Mangler Exports-mappe";
        }

        return odfCount == 0 ? "Ingen ODF-filer" : "Ikke validert";
    }

    private Task PrepareSiusRankCopiesAsync()
    {
        var config = _currentEventConfig ?? throw new InvalidOperationException("event.json er ikke lastet.");
        var eventPath = _currentEventFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), EventProjectFile.FileName);
        foreach (var classConfig in config.Classes)
        {
            var folder = EventProjectFile.ResolvePath(eventPath, $"./Rank_{classConfig.Class}");
            var exports = EventProjectFile.ResolvePath(eventPath, $"./Rank_{classConfig.Class}/Exports");
            if (Directory.Exists(folder))
            {
                AppendLog($"{classConfig.Class}: SIUS Rank-kopi finnes allerede: {folder}");
            }
            else
            {
                Directory.CreateDirectory(folder);
                AppendLog($"{classConfig.Class}: opprettet SIUS Rank-kopi-mappe: {folder}");
            }

            if (Directory.Exists(exports))
            {
                AppendLog($"{classConfig.Class}: Exports-mappe finnes allerede: {exports}");
            }
            else
            {
                Directory.CreateDirectory(exports);
                AppendLog($"{classConfig.Class}: opprettet Exports-mappe: {exports}");
            }
        }

        RenderWritebackRows(config);
        return Task.CompletedTask;
    }

    private Task ScanWritebackResultsAsync()
    {
        var config = _currentEventConfig ?? throw new InvalidOperationException("Åpne event.json først.");
        if (config.Classes.Count == 0)
        {
            WritebackScanSummaryLabel.Text = "Det finnes ingen klasseoppsett. Gå til Event-fanen og oppdater klasser.";
            RenderWritebackRows(config);
            return Task.CompletedTask;
        }

        var eventPath = _currentEventFilePath ?? throw new InvalidOperationException("Åpne event.json først.");
        var result = DesktopSiusRankExportsScanner.Scan(eventPath, config);
        _writebackRows.Clear();
        _writebackStatuses.Clear();
        foreach (var row in result.Rows)
        {
            _writebackRows[row.Class] = row;
        }

        RenderWritebackRows(result.Rows);
        WritebackScanSummaryLabel.Text = result.Summary;
        AppendLog($"SIUS Rank scan: found {result.FoundExports.Count} Exports folders, matched {result.ReadyCount} classes.");
        if (result.FoundExports.Count == 0)
        {
            AppendLog("Ingen resultater funnet. Trykk Rank List Main i SIUS Rank og prøv igjen.");
        }

        return Task.CompletedTask;
    }

    private async Task ValidateAllWritebackAsync()
    {
        foreach (var row in ReadyWritebackRows())
        {
            await RunClassWritebackAsync(row.Class, apply: false);
        }
    }

    private async Task DryRunReadyWritebackAsync()
    {
        foreach (var row in ReadyWritebackRows())
        {
            await RunClassWritebackAsync(row.Class, apply: false);
        }
    }

    private async Task ApplyValidatedWritebackAsync()
    {
        var ready = ReadyWritebackRows()
            .Where(row => _writebackStatuses.TryGetValue(row.Class, out var status) && status.CanApply)
            .ToList();
        if (ready.Count == 0)
        {
            AppendLog("Ingen validerte SIUS Rank-resultater er klare for writeback.");
            return;
        }

        if (!await ConfirmInrxWritebackAsync($"Skriv {ready.Count} validerte klasse(r) til inR?"))
        {
            AppendLog("Writeback cancelled.");
            return;
        }

        foreach (var row in ready)
        {
            var options = BuildClassWritebackOptions(row.Class, apply: true);
            var result = await Task.Run(() => SiusRankWritebackRunner.Run(options));
            AppendLog(FormatWritebackResult(result));
            var after = await Task.Run(() => SiusRankClassWritebackStatusResolver.Validate(options with { Apply = false }));
            UpdateWritebackStatus(row.Class, after);
            AppendLog(FormatClassWritebackStatus(row.Class, after));
        }
    }

    private IReadOnlyList<DesktopWritebackDiscoveryRow> ReadyWritebackRows() =>
        _writebackRows.Values
            .Where(row => row.CanRun)
            .OrderBy(row => row.Class, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static Task OpenFolderAsync(string folder)
    {
        if (!Directory.Exists(folder))
        {
            throw new ArgumentException($"Mappe finnes ikke: {folder}");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
        return Task.CompletedTask;
    }

    private async Task RunClassWritebackAsync(string className, bool apply)
    {
        var options = BuildClassWritebackOptions(className, apply: false);
        var status = await Task.Run(() => SiusRankClassWritebackStatusResolver.Validate(options));
        UpdateWritebackStatus(className, status);
        AppendLog(FormatClassWritebackStatus(className, status));

        if (!apply)
        {
            return;
        }

        if (!status.CanApply)
        {
            AppendLog($"Writeback not applied for {className}: {status.Text}");
            return;
        }

        if (!await ConfirmInrxWritebackAsync($"Skriv {className} til inR?"))
        {
            AppendLog($"Writeback cancelled for {className}.");
            return;
        }

        var result = await Task.Run(() => SiusRankWritebackRunner.Run(options with { Apply = true }));
        AppendLog(FormatWritebackResult(result));
        var after = await Task.Run(() => SiusRankClassWritebackStatusResolver.Validate(options));
        UpdateWritebackStatus(className, after);
        AppendLog(FormatClassWritebackStatus(className, after));
    }

    private SiusRankWritebackOptions BuildClassWritebackOptions(string className, bool apply)
    {
        var eventPath = _currentEventFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), EventProjectFile.FileName);
        var config = _currentEventConfig ?? BuildEventConfigFromUi(eventPath, refreshClasses: false);
        var row = _writebackRows.TryGetValue(className, out var discovered)
            ? discovered
            : throw new InvalidOperationException("Trykk Finn resultater i stevnemappen først.");
        if (!row.CanRun || string.IsNullOrWhiteSpace(row.ExportsFullPath))
        {
            throw new InvalidOperationException($"{className}: finner ikke resultater. Trykk Rank List Main i SIUS Rank og prøv igjen.");
        }

        var csvOutput = EventProjectFile.ResolvePath(eventPath, config.Csv.Output);
        var bibMapPath = Path.Combine(csvOutput, ChampionshipStartNumbers.BibMapFileName);
        return new SiusRankWritebackOptions(
            EventProjectFile.ResolvePath(eventPath, config.Inrx.Db),
            row.ExportsFullPath,
            ParseIdList(config.Inrx.Stevner, "Stevne ids"),
            File.Exists(bibMapPath) ? bibMapPath : null,
            new HashSet<string>([row.EventFilter], StringComparer.OrdinalIgnoreCase),
            apply);
    }

    private void UpdateWritebackStatus(string className, SiusRankClassWritebackStatus status)
    {
        _writebackStatuses[className] = status;
        Dispatcher.UIThread.Post(() =>
        {
            if (_writebackStatusLabels.TryGetValue(className, out var label))
            {
                label.Text = FormatWritebackStatusLabel(status);
            }
            UpdateActionStates();
        });
    }

    private static string FormatWritebackStatusLabel(SiusRankClassWritebackStatus status) =>
        status.Kind switch
        {
            SiusRankClassWritebackStatusKind.MissingExport => "Finner ikke resultater",
            SiusRankClassWritebackStatusKind.NoCompleteResults => "Ingen ODF-filer",
            SiusRankClassWritebackStatusKind.ReadyForWriteback => "Tørrkjørt OK",
            SiusRankClassWritebackStatusKind.WrittenBack => "Skrevet til inR",
            SiusRankClassWritebackStatusKind.Error => "Validering feilet",
            _ => status.Text
        };

    private static string FormatClassWritebackStatus(
        string className,
        SiusRankClassWritebackStatus status)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{className}: {status.Text}");
        if (status.Kind == SiusRankClassWritebackStatusKind.MissingExport)
        {
            builder.AppendLine($"{className}: finner ikke resultater.");
            builder.AppendLine("Trykk Rank List Main i SIUS Rank og prøv igjen.");
        }
        if (status.Result is not null)
        {
            builder.AppendLine($"Updates={status.Result.UpdateCount}, unchanged={status.Result.UnchangedCount}, skipped={status.Result.SkippedCount}");
        }

        foreach (var message in status.Messages.Take(20))
        {
            builder.AppendLine($"  {message}");
        }

        if (status.Messages.Count > 20)
        {
            builder.AppendLine($"  ... {status.Messages.Count - 20} more message(s)");
        }

        return builder.ToString();
    }

    private async Task RunExportAsync()
    {
        await RefreshCsvPreflightAsync();
        AppendCsvSkippedMessage();
        var options = BuildExportOptions();
        var command = BuildExportCommand(options);
        AppendLog(command);

        await Task.Run(() =>
        {
            var result = BulkExportRunner.Run(options);
            AppendLog(FormatBulkExportResult(result));
        });
    }

    private async Task CopyTemplatesToSiusRankAsync()
    {
        var sourceDirectory = ResolveBundledTemplatesDirectory();
        var targetDirectory = ResolveSiusRankTemplatesDirectory();
        AppendLog($"Copying SIUS Rank templates from {sourceDirectory} to {targetDirectory}");

        var result = await Task.Run(() => SiusRankTemplateCopier.Copy(sourceDirectory, targetDirectory));
        var shooterGroupsTemplatePath = result.Files
            .Select(file => file.TargetPath)
            .FirstOrDefault(path => Path.GetFileName(path).Equals("ShooterGroupsTemplate.xml", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(shooterGroupsTemplatePath))
        {
            ShooterGroupsTemplateInput.Text = shooterGroupsTemplatePath;
            SaveDesktopSettings();
        }

        AppendLog(FormatTemplateCopyResult(result));
    }

    private async Task RunCreateBibMapAsync()
    {
        await RefreshCsvPreflightAsync();
        AppendCsvSkippedMessage();
        var options = BuildExportOptions(includeTemplate: false);
        var command = BuildBibMapCommand(options);
        AppendLog(command);

        var result = await Task.Run(() => BulkExportRunner.CreateBibMap(options));
        BibMapPathInput.Text = ToEventDisplayPath(result.BibMapPath);
        SaveDesktopSettings();
        AppendLog(FormatBibMapResult(result));
    }

    private Task RunWritebackAsync(bool apply)
    {
        var options = BuildWritebackOptions(apply);
        var command = BuildWritebackCommand(options);
        AppendLog(command);

        return RunConfirmedWritebackAsync(options);
    }

    private async Task RunConfirmedWritebackAsync(SiusRankWritebackOptions options)
    {
        if (options.Apply && !await ConfirmInrxWritebackAsync("Skriv SIUS Rank-resultater til inrX?"))
        {
            AppendLog("Writeback cancelled.");
            return;
        }

        await Task.Run(() =>
        {
            var result = SiusRankWritebackRunner.Run(options);
            AppendLog(FormatWritebackResult(result));
        });
    }

    private Task RunSscUsersAsync()
    {
        var options = BuildSscUsersOptions();
        AppendLog(BuildSscUsersCommand(options));

        return Task.Run(() =>
        {
            var result = SscUsersRunner.Run(options);
            if (result.Written && !string.IsNullOrWhiteSpace(result.OutputPath))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    SscUsersCsvPathInput.Text = ToEventDisplayPath(result.OutputPath);
                    SaveDesktopSettings();
                });
            }

            AppendLog(FormatSscUsersResult(result));
        });
    }

    private Task RunSscValidateAsync()
    {
        var options = BuildSscValidateOptions();
        AppendLog(BuildSscValidateCommand(options));

        return Task.Run(() =>
        {
            var result = SscValidationRunner.Run(options);
            AppendLog(FormatSscValidationResult(result));
        });
    }

    private Task RunSscLanesAsync()
    {
        var options = BuildSscLanesOptions();
        AppendLog(BuildSscLanesCommand(options));

        return Task.Run(() =>
        {
            var result = SscLanesRunner.Run(options);
            AppendLog(FormatSscLanesResult(result));
        });
    }

    private void ShowLogWindow()
    {
        if (_logWindow is null)
        {
            _logWindow = new LogWindow(
                () => RunSafelyAsync("Kopierer logg", CopyLogAsync),
                () => RunSafelyAsync("Lagrer logg", SaveLogAsync),
                ClearLog);
            _logWindow.Closed += (_, _) => _logWindow = null;
            _logWindow.Show(this);
        }
        else
        {
            _logWindow.Activate();
        }

        _logWindow.SetLogText(_logText.ToString());
        _logErrorCount = 0;
        UpdateLogIndicators();
    }

    private void ClearLog()
    {
        _logText.Clear();
        _logErrorCount = 0;
        _latestLogMessage = "Loggen er tømt.";
        _logWindow?.SetLogText(string.Empty);
        UpdateLogIndicators();
    }

    private void UpdateLogIndicators()
    {
        OpenLogButtonControl.Content = _logErrorCount > 0 ? $"Logg ({_logErrorCount})" : "Logg";
        LatestLogLabelControl.Text = _latestLogMessage;
    }

    private async Task CopyLogAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            throw new InvalidOperationException("Clipboard is not available.");
        }

        await clipboard.SetTextAsync(_logText.ToString());
        AppendLog("Logg kopiert.");
    }

    private async Task SaveLogAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Lagre logg",
            SuggestedFileName = $"inrxtosiusrank-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            FileTypeChoices =
            [
                new FilePickerFileType("Text") { Patterns = ["*.txt"] },
                FilePickerFileTypes.All
            ]
        });

        if (file is null)
        {
            AppendLog("Lagre logg avbrutt.");
            return;
        }

        var text = _logText.ToString();
        if (file.TryGetLocalPath() is { } path)
        {
            await File.WriteAllTextAsync(path, text, Encoding.UTF8);
            AppendLog($"Logg lagret: {path}");
            return;
        }

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await writer.WriteAsync(text);
        AppendLog("Logg lagret.");
    }

    private async Task<bool> ConfirmInrxWritebackAsync(string title)
    {
        var dialog = new ConfirmWritebackDialog(title);
        return await dialog.ShowDialog<bool>(this);
    }

    private void UpdateActionStates()
    {
        var createEvent = BuildCreateEventActionState();
        var saveEvent = BuildSaveEventActionState();
        var csvBase = BuildCsvActionState(includeTemplate: false);
        var csvExport = BuildCsvActionState(includeTemplate: true);
        var writeback = BuildWritebackActionState();
        var sscRows = BuildSscStatusRows();
        var classWriteback = BuildEventClassWritebackActionState();
        var databaseExists = HasExistingFile(DatabasePathInput.Text);
        var idsAreValid = TryParseOptionalIds(StevneIdsInput.Text, out var ids);
        var hasIds = idsAreValid && ids.Count > 0;
        var csvPreflightCanExport = _csvPreflight?.CanExport == true;
        var hasReadyWritebackRows = _writebackRows.Values.Any(row => row.CanRun);
        var hasValidatedReadyClass = _writebackStatuses.Values.Any(status => status.CanApply);

        SetControlEnabled("CreateEventMenuItem", createEvent.CanRun && !_isRunning);
        SetControlEnabled("CreateEventButton", createEvent.CanRun && !_isRunning);
        SetControlEnabled("OpenEventMenuItem", !_isRunning);
        SetControlEnabled("OpenEventButton", !_isRunning);
        SetControlEnabled("SaveEventMenuItem", saveEvent.CanRun && !_isRunning);
        SetControlEnabled("SaveEventButton", saveEvent.CanRun && !_isRunning);
        SetControlEnabled("LoadDatabaseButton", databaseExists && !_isRunning);
        SetControlEnabled("SearchStevnerButton", databaseExists && !_isRunning);
        SetControlEnabled("RefreshOvelserButton", databaseExists && hasIds && !_isRunning);
        SetControlEnabled("RefreshEventClassesButton", saveEvent.CanRun && !_isRunning);
        SetControlEnabled("SelectAllStevnerButton", _stevneChecks.Count > 0 && !_isRunning);
        SetControlEnabled("ClearStevnerButton", _stevneChecks.Count > 0 && !_isRunning);
        SetControlEnabled("CopyTemplatesButton", !_isRunning);
        SetControlEnabled("CreateBibMapButton", csvBase.CanRun && csvPreflightCanExport && !_isRunning);
        SetControlEnabled("RunExportButton", csvExport.CanRun && csvPreflightCanExport && !_isRunning);
        SetControlEnabled("RunWritebackPreviewButton", writeback.CanRun && !_isRunning);
        SetControlEnabled("RunWritebackApplyButton", writeback.CanRun && !_isRunning);
        SetControlEnabled("ScanWritebackResultsButton", _currentEventConfig is not null && !_isRunning);
        SetControlEnabled("OpenEventFromWritebackButton", _currentEventConfig is null && !_isRunning);
        SetControlEnabled("ValidateAllWritebackButton", classWriteback.CanRun && hasReadyWritebackRows && !_isRunning);
        SetControlEnabled("DryRunReadyWritebackButton", classWriteback.CanRun && hasReadyWritebackRows && !_isRunning);
        SetControlEnabled("ApplyValidatedWritebackButton", classWriteback.CanRun && hasValidatedReadyClass && !_isRunning);
        SetControlEnabled("RunSscUsersButton", sscRows.First(row => row.Action == "Eksporter SSC-brukere").CanRun && !_isRunning);
        SetControlEnabled("RunSscValidateButton", sscRows.First(row => row.Action == "Valider SSC").CanRun && !_isRunning);
        SetControlEnabled("RunSscLanesButton", sscRows.First(row => row.Action == "Eksporter SSC baner/reset").CanRun && !_isRunning);
        SetControlEnabled("ShowStevnerButton", databaseExists && !_isRunning);
        SetControlEnabled("ShowSelectedOvelserButton", databaseExists && hasIds && !_isRunning);

        foreach (var item in _writebackValidateButtons)
        {
            item.Value.IsEnabled = classWriteback.CanRun &&
                _writebackRows.TryGetValue(item.Key, out var row) &&
                row.CanRun &&
                !_isRunning;
        }

        foreach (var item in _writebackApplyButtons)
        {
            item.Value.IsEnabled = classWriteback.CanRun &&
                _writebackRows.TryGetValue(item.Key, out _) &&
                _writebackStatuses.TryGetValue(item.Key, out var status) &&
                status.CanApply &&
                !_isRunning;
        }

        EventActionHelpLabel.Text = FormatMissing(createEvent);
        CsvActionHelpLabel.Text = FormatCsvMissing(csvBase, csvExport, _csvPreflight);
        WritebackActionHelpLabel.Text = FormatWritebackSummary(classWriteback);
        RenderSscStatusRows(sscRows);
        UpdatePathTooltips();
    }

    private string FormatWritebackSummary(ActionState state)
    {
        if (!state.CanRun)
        {
            return FormatMissing(state);
        }

        if (_writebackRows.Count == 0)
        {
            return "Trykk Finn resultater i stevnemappen.";
        }

        var ready = _writebackRows.Values.Count(row => row.CanRun);
        return ready == _writebackRows.Count
            ? $"Alle {ready} resultatsett er klare."
            : $"{ready} av {_writebackRows.Count} resultatsett er klare.";
    }

    private void UpdatePathTooltips()
    {
        SetPathTooltip(DatabasePathInput);
        SetPathTooltip(OutputDirectoryInput);
        SetPathTooltip(ShooterGroupsTemplateInput);
        SetPathTooltip(ExportsDirectoryInput);
        SetPathTooltip(BibMapPathInput);
        SetPathTooltip(SscBibMapPathInput);
        SetPathTooltip(SscOutputDirectoryInput);
        SetPathTooltip(SscUsersCsvPathInput);
    }

    private void SetPathTooltip(TextBox input)
    {
        var text = input.Text;
        ToolTip.SetTip(input, string.IsNullOrWhiteSpace(text) ? null : ResolveEventPath(text));
    }

    private void SetStatus(RunStatus status, string? detail = null)
    {
        switch (status)
        {
            case RunStatus.Ready:
                StatusLabel.Text = "Klar";
                StatusBadgeControl.Background = ReadyStatusBrush;
                ToolTip.SetTip(StatusBadgeControl, "Klar");
                break;
            case RunStatus.Running:
                StatusLabel.Text = "Kjører";
                StatusBadgeControl.Background = RunningStatusBrush;
                ToolTip.SetTip(StatusBadgeControl, detail ?? "Kjører");
                break;
            case RunStatus.Error:
                StatusLabel.Text = "Feil";
                StatusBadgeControl.Background = ErrorStatusBrush;
                ToolTip.SetTip(StatusBadgeControl, detail ?? "Feil");
                break;
        }
    }

    private void SetControlEnabled(string name, bool isEnabled)
    {
        if (this.FindControl<Control>(name) is { } control)
        {
            control.IsEnabled = isEnabled;
        }
    }

    private ActionState BuildCreateEventActionState()
    {
        var missing = new List<string>();
        AddDatabaseRequirement(missing);
        AddStevneIdsRequirement(missing);
        AddOvelseRequirement(missing);
        AddTextRequirement(missing, SiusRankFolderInput.Text, "SIUS Rank-mappe");
        return new ActionState(missing);
    }

    private ActionState BuildSaveEventActionState()
    {
        var missing = BuildCreateEventActionState().Missing.ToList();
        AddTextRequirement(missing, OutputDirectoryInput.Text, "Output-mappe");
        return new ActionState(missing);
    }

    private ActionState BuildCsvActionState(bool includeTemplate)
    {
        var missing = new List<string>();
        AddDatabaseRequirement(missing);
        AddStevneIdsRequirement(missing);
        AddTextRequirement(missing, OutputDirectoryInput.Text, "Output-mappe");
        if (includeTemplate && !string.IsNullOrWhiteSpace(ShooterGroupsTemplateInput.Text) &&
            !HasExistingFile(ShooterGroupsTemplateInput.Text))
        {
            missing.Add("ShooterGroupsTemplate.xml finnes ikke");
        }

        return new ActionState(missing);
    }

    private ActionState BuildWritebackActionState()
    {
        var missing = new List<string>();
        AddDatabaseRequirement(missing);
        AddStevneIdsRequirement(missing);
        if (!HasExistingDirectory(ExportsDirectoryInput.Text))
        {
            missing.Add("eksportmappe finnes ikke");
        }

        return new ActionState(missing);
    }

    private ActionState BuildEventClassWritebackActionState()
    {
        var missing = new List<string>();
        if (_currentEventConfig is null)
        {
            missing.Add("event.json er ikke lastet");
            return new ActionState(missing);
        }
        var eventPath = _currentEventFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), EventProjectFile.FileName);
        if (!HasExistingFile(EventProjectFile.ResolvePath(eventPath, _currentEventConfig.Inrx.Db)))
        {
            missing.Add("storage.db3 finnes ikke");
        }

        return new ActionState(missing);
    }

    private IReadOnlyList<SscActionStatusRow> BuildSscStatusRows()
    {
        var idsValid = TryParseOptionalIds(StevneIdsInput.Text, out var ids);
        var usersCsvPath = ResolveSscUsersCsvPathForValidation();
        return SscActionStatusBuilder.Build(new SscActionStatusInput(
            DatabaseExists: HasExistingFile(DatabasePathInput.Text),
            StevneIdsValid: idsValid,
            StevneIds: ids,
            SelectedStevneIdsText: StevneIdsInput.Text?.Trim() ?? string.Empty,
            LaneStevneId: SelectedSscStevneId(),
            OutputDirectoryPresent: !string.IsNullOrWhiteSpace(SscOutputDirectoryInput.Text),
            UsersCsvExists: HasExistingFile(usersCsvPath),
            BibMapExists: HasExistingFile(SscBibMapPathInput.Text),
            OrganizationNamePresent: !string.IsNullOrWhiteSpace(SscOrganizationNameInput.Text),
            OrganizationIdPresent: !string.IsNullOrWhiteSpace(SscOrganizationIdInput.Text),
            StartlagPresent: !string.IsNullOrWhiteSpace(SscStartlagInput.Text),
            StartlagValid: DateTime.TryParse(SscStartlagInput.Text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)));
    }

    private void AddDatabaseRequirement(List<string> missing)
    {
        if (!HasExistingFile(DatabasePathInput.Text))
        {
            missing.Add("storage.db3 finnes ikke");
        }
    }

    private void AddStevneIdsRequirement(List<string> missing)
    {
        if (!TryParseOptionalIds(StevneIdsInput.Text, out var ids))
        {
            missing.Add("Stevne.Id er ugyldig");
        }
        else if (ids.Count == 0)
        {
            missing.Add("Stevne.Id mangler");
        }
    }

    private void AddOvelseRequirement(List<string> missing)
    {
        if (!HasSelectedOvelse())
        {
            missing.Add("Øvelse mangler");
        }
    }

    private static void AddTextRequirement(List<string> missing, string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add($"{label} mangler");
        }
    }

    private bool HasSelectedOvelse() =>
        OvelseSelectInput.SelectedItem is OvelseChoice { IsAll: false } ||
        int.TryParse(OvelseFilterInput.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

    private CsvExerciseSelection SelectedCsvExerciseSelection()
    {
        if (OvelseSelectInput.SelectedItem is OvelseChoice { IsAll: false } choice)
        {
            var name = choice.HovedOvelseId == 0 && string.IsNullOrWhiteSpace(choice.ShortName)
                ? TryLookupOvelseName(choice.Id) ?? choice.Name
                : choice.Name;
            return new CsvExerciseSelection(IsAll: false, choice.Id, name);
        }

        if (int.TryParse(OvelseFilterInput.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ovelseId))
        {
            var name = OvelseSelectInput.ItemsSource is IEnumerable<OvelseChoice> choices
                ? choices.FirstOrDefault(choice => !choice.IsAll && choice.Id == ovelseId)?.Name
                : null;
            return new CsvExerciseSelection(IsAll: false, ovelseId, name ?? $"OvelseDef.Id {ovelseId}");
        }

        return CsvExerciseSelection.All;
    }

    private string? ResolveSscUsersCsvPathForValidation()
    {
        if (!string.IsNullOrWhiteSpace(SscUsersCsvPathInput.Text))
        {
            return ResolveEventPath(SscUsersCsvPathInput.Text);
        }

        return string.IsNullOrWhiteSpace(SscOutputDirectoryInput.Text)
            ? null
            : Path.Combine(ResolveEventPath(SscOutputDirectoryInput.Text), "ssc-users.csv");
    }

    private string CurrentEventPath =>
        _currentEventFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), EventProjectFile.FileName);

    private string ToEventDisplayPath(string? path) =>
        DesktopEventPaths.ToEventDisplayPath(_currentEventFilePath, path);

    private string ToProjectOutputDisplayPath(string? path, string? eventPath = null) =>
        DesktopEventPaths.ToEventLocalDisplayPath(eventPath ?? _currentEventFilePath, path, "./siusrank-import");

    private string NormalizeOutputDirectoryInput(string? eventPath = null)
    {
        var outputDirectory = ToProjectOutputDisplayPath(OutputDirectoryInput.Text, eventPath);
        OutputDirectoryInput.Text = outputDirectory;
        return outputDirectory;
    }

    private string ResolveEventPath(string? path) =>
        DesktopEventPaths.ResolveEventPath(_currentEventFilePath, path);

    private bool HasExistingFile(string? value) =>
        !string.IsNullOrWhiteSpace(value) && File.Exists(ResolveEventPath(value));

    private bool HasExistingDirectory(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Directory.Exists(ResolveEventPath(value));

    private static bool TryParseOptionalIds(string? value, out IReadOnlyList<int> ids)
        => DesktopUiParsing.TryParseOptionalIds(value, out ids);

    private static string FormatMissing(ActionState state) =>
        state.CanRun ? string.Empty : "Mangler: " + string.Join(", ", state.Missing) + ".";

    private static string FormatCsvMissing(
        ActionState bibMapState,
        ActionState exportState,
        CsvPreflightResult? preflight)
    {
        var lines = new List<string>();
        if (!bibMapState.CanRun)
        {
            lines.Add("bib-map.csv: " + string.Join(", ", bibMapState.Missing) + ".");
        }

        if (!exportState.CanRun)
        {
            lines.Add("CSV-filer: " + string.Join(", ", exportState.Missing) + ".");
        }

        if (preflight is not null && !preflight.CanExport)
        {
            lines.Add(preflight.EmptyMessage);
        }

        if (preflight is null)
        {
            lines.Add("CSV preflight er ikke kjørt ennå.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private enum RunStatus
    {
        Ready,
        Running,
        Error
    }

    private sealed record ActionState(IReadOnlyList<string> Missing)
    {
        public bool CanRun => Missing.Count == 0;
    }

    private AppOptions BuildExportOptions(bool includeTemplate = true)
    {
        var databasePath = RequireExistingFile(DatabasePathInput.Text, "storage.db3");
        var outputDirectory = ResolveEventPath(NormalizeOutputDirectoryInput());
        var templatePath = !includeTemplate || string.IsNullOrWhiteSpace(ShooterGroupsTemplateInput.Text)
            ? null
            : RequireExistingFile(ShooterGroupsTemplateInput.Text, "Shooter groups XML");
        var ids = _csvPreflight?.IncludedStevneIds.Count > 0
            ? _csvPreflight.IncludedStevneIds
            : ParseIdList(StevneIdsInput.Text, "Stevne ids");

        return DesktopExportOptionsBuilder.Build(new DesktopExportOptionsInput(
            databasePath,
            ids,
            outputDirectory,
            SelectedEncoding(),
            templatePath,
            SelectedCsvExerciseSelection()));
    }

    private SiusRankWritebackOptions BuildWritebackOptions(bool apply)
    {
        var exportsDirectory = RequireExistingDirectory(ExportsDirectoryInput.Text, "Exports directory");
        var requestedBibMapPath = CleanSetting(BibMapPathInput.Text) is { } bibMapPath
            ? ResolveEventPath(bibMapPath)
            : null;
        var resolvedBibMapPath = SiusRankWritebackCommand.ResolveBibMapPath(
            requestedBibMapPath,
            exportsDirectory,
            requireExplicitPath: false);

        if (!string.IsNullOrWhiteSpace(requestedBibMapPath) &&
            !File.Exists(requestedBibMapPath))
        {
            AppendLog(resolvedBibMapPath is null
                ? $"WARNING: bib-map.csv does not exist, continuing without it: {requestedBibMapPath}"
                : $"WARNING: bib-map.csv does not exist at {requestedBibMapPath}; using auto-detected file: {resolvedBibMapPath}");
        }

        if (!string.IsNullOrWhiteSpace(resolvedBibMapPath))
        {
            BibMapPathInput.Text = ToEventDisplayPath(resolvedBibMapPath);
        }

        var eventFilters = ParseEventFilters(EventFilterInput.Text);
        return new SiusRankWritebackOptions(
            RequireExistingFile(DatabasePathInput.Text, "storage.db3"),
            exportsDirectory,
            ParseIdList(StevneIdsInput.Text, "Stevne ids"),
            resolvedBibMapPath,
            eventFilters,
            apply);
    }

    private ExportSscUsersOptions BuildSscUsersOptions()
    {
        var outputDirectory = ResolveEventPath(RequireText(SscOutputDirectoryInput.Text, "SSC output directory"));
        var outputPath = Path.Combine(outputDirectory, "ssc-users.csv");
        return new ExportSscUsersOptions(
            RequireExistingFile(DatabasePathInput.Text, "storage.db3"),
            ParseIdList(StevneIdsInput.Text, "Stevne ids"),
            CleanSetting(SscBibMapPathInput.Text) is { } bibMapPath ? ResolveEventPath(bibMapPath) : null,
            outputPath,
            RequireText(SscOrganizationNameInput.Text, "SSC organization name"),
            RequireText(SscOrganizationIdInput.Text, "SSC organization id"),
            SelectedEncoding());
    }

    private ValidateSscOptions BuildSscValidateOptions()
    {
        var usersCsvPath = string.IsNullOrWhiteSpace(SscUsersCsvPathInput.Text)
            ? Path.Combine(ResolveEventPath(RequireText(SscOutputDirectoryInput.Text, "SSC output directory")), "ssc-users.csv")
            : ResolveEventPath(SscUsersCsvPathInput.Text);

        return new ValidateSscOptions(
            RequireExistingFile(DatabasePathInput.Text, "storage.db3"),
            ParseIdList(StevneIdsInput.Text, "Stevne ids"),
            CleanSetting(SscBibMapPathInput.Text) is { } bibMapPath ? ResolveEventPath(bibMapPath) : null,
            RequireExistingFile(usersCsvPath, "SSC users CSV"));
    }

    private ExportSscLanesOptions BuildSscLanesOptions()
    {
        var selectedId = SelectedSscStevneId();
        if (selectedId is null)
        {
            throw new ArgumentException("Velg stevne for SSC baner/reset.");
        }

        return new ExportSscLanesOptions(
            RequireExistingFile(DatabasePathInput.Text, "storage.db3"),
            selectedId.Value,
            RequireText(SscStartlagInput.Text, "SSC startlag"),
            CleanSetting(SscBibMapPathInput.Text) is { } bibMapPath ? ResolveEventPath(bibMapPath) : null,
            ResolveEventPath(RequireText(SscOutputDirectoryInput.Text, "SSC output directory")),
            SelectedSscLaneCount());
    }

    private int? SelectedSscStevneId() =>
        SscStevneInput.SelectedItem is SscStevneChoice choice
            ? choice.Id
            : null;

    private string SelectedEncoding()
    {
        if (EncodingInput.SelectedItem is ComboBoxItem item && item.Content is not null)
        {
            return item.Content.ToString() ?? CsvEncoding.Utf8Bom;
        }

        return CsvEncoding.Utf8Bom;
    }

    private void SetEncoding(string? encodingName)
    {
        if (string.IsNullOrWhiteSpace(encodingName))
        {
            return;
        }

        EncodingInput.SelectedIndex = encodingName.Equals(CsvEncoding.Windows1252, StringComparison.OrdinalIgnoreCase)
            ? 1
            : 0;
    }

    private int SelectedSscLaneCount()
    {
        if (SscLaneCountInput.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Content?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var laneCount))
        {
            return laneCount;
        }

        return 40;
    }

    private void SetSscLaneCount(string? laneCount)
    {
        SscLaneCountInput.SelectedIndex = laneCount switch
        {
            "10" => 0,
            "25" => 1,
            _ => 2
        };
    }

    private void SaveDesktopSettings(bool logWarning = true)
    {
        try
        {
            _desktopSettings = BuildDesktopSettings();
            _desktopSettings.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (logWarning)
            {
                AppendLog($"WARNING: Could not save desktop settings: {ex.Message}");
            }
        }
    }

    private DesktopSettings BuildDesktopSettings() => new()
    {
        Version = 2,
        Global = _desktopSettings.Global,
        Recent = new RecentDesktopSettings
        {
            LastEventFilePath = CleanSetting(EventFilePathInput.Text)
        }
    };

    private static void SetTextIfPresent(TextBox input, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            input.Text = value;
        }
    }

    private static string? CleanSetting(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlySet<string> ParseEventFilters(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return value
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(SiusRankEventDiscipline.NormalizeFilter)
            .Where(item => item.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<int> ParseIdList(string? value, string name) =>
        DesktopUiParsing.ParseIdList(value, name);

    private static IReadOnlyList<int> ParseOptionalIdList(string? value) =>
        DesktopUiParsing.ParseOptionalIdList(value);

    private string RequireExistingFile(string? value, string label)
    {
        var path = ResolveEventPath(RequireText(value, label));
        if (!File.Exists(path))
        {
            throw new ArgumentException($"{label} does not exist: {path}");
        }

        return EventProjectFile.IsWindowsRootedPath(path) ? path : Path.GetFullPath(path);
    }

    private string RequireExistingDirectory(string? value, string label)
    {
        var path = ResolveEventPath(RequireText(value, label));
        if (!Directory.Exists(path))
        {
            throw new ArgumentException($"{label} does not exist: {path}");
        }

        return EventProjectFile.IsWindowsRootedPath(path) ? path : Path.GetFullPath(path);
    }

    private static string RequireText(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{label} is required.");
        }

        return value.Trim();
    }

    private static string ResolveBundledTemplatesDirectory()
    {
        var fallback = Path.Combine(AppContext.BaseDirectory, "Templates");
        var candidates = new string?[]
        {
            fallback,
            Path.Combine(Directory.GetCurrentDirectory(), "Templates"),
            FindRepositoryTemplatesDirectory()
        };

        return candidates
            .OfType<string>()
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(Directory.Exists)
            ?? Path.GetFullPath(fallback);
    }

    private static string? FindRepositoryTemplatesDirectory()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Templates");
            if (File.Exists(Path.Combine(candidate, "ShooterGroupsTemplate.xml")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string ResolveSiusRankTemplatesDirectory()
    {
        var settings = AppSettings.Load();
        var path = string.IsNullOrWhiteSpace(settings.SiusRankTemplatesPath)
            ? AppSettings.DefaultSiusRankTemplatesPath
            : settings.SiusRankTemplatesPath;

        if (!OperatingSystem.IsWindows() && !Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException(
                $"SIUS Rank template path is not a local absolute path on this machine: {path}. " +
                "Set Paths:SiusRankTemplates in appsettings.json to test this outside Windows.");
        }

        return path;
    }

    private static string BuildExportCommand(AppOptions options)
    {
        var parts = new List<string>
        {
            "InrxToSiusRank",
            "--db", Quote(options.DatabasePath),
            "--stevne-ids", Quote(FormatIds(options.StevneIds)),
            "--output-dir", Quote(options.OutputDirectory ?? string.Empty),
            "--encoding", options.EncodingName
        };

        if (options.OvelseId is not null)
        {
            parts.AddRange(["--ovelse-id", options.OvelseId.Value.ToString(CultureInfo.InvariantCulture)]);
        }
        else if (!string.IsNullOrWhiteSpace(options.OvelseName))
        {
            parts.AddRange(["--ovelse", Quote(options.OvelseName)]);
        }

        if (!string.IsNullOrWhiteSpace(options.ShooterGroupsTemplatePath))
        {
            parts.AddRange(["--shooter-groups-template", Quote(options.ShooterGroupsTemplatePath)]);
        }

        return "$ " + string.Join(' ', parts);
    }

    private static string BuildBibMapCommand(AppOptions options)
    {
        var parts = new List<string>
        {
            "InrxToSiusRank.Desktop",
            "create-bib-map",
            "--db", Quote(options.DatabasePath),
            "--stevne-ids", Quote(FormatIds(options.StevneIds)),
            "--output-dir", Quote(options.OutputDirectory ?? string.Empty)
        };

        if (options.OvelseId is not null)
        {
            parts.AddRange(["--ovelse-id", options.OvelseId.Value.ToString(CultureInfo.InvariantCulture)]);
        }
        else if (!string.IsNullOrWhiteSpace(options.OvelseName))
        {
            parts.AddRange(["--ovelse", Quote(options.OvelseName)]);
        }

        return "$ " + string.Join(' ', parts);
    }

    private static string BuildWritebackCommand(SiusRankWritebackOptions options)
    {
        var parts = new List<string>
        {
            "InrxToSiusRank",
            "writeback-siusrank",
            "--db", Quote(options.DatabasePath),
            "--stevne-ids", Quote(FormatIds(options.StevneIds)),
            "--exports", Quote(options.ExportsDirectory)
        };

        if (!string.IsNullOrWhiteSpace(options.BibMapPath))
        {
            parts.AddRange(["--bib-map", Quote(options.BibMapPath)]);
        }

        if (options.EventFilters.Count > 0)
        {
            parts.AddRange(["--event", Quote(string.Join(",", options.EventFilters))]);
        }

        if (options.Apply)
        {
            parts.Add("--apply");
        }

        return "$ " + string.Join(' ', parts);
    }

    private static string BuildSscUsersCommand(ExportSscUsersOptions options)
    {
        var parts = new List<string>
        {
            "InrxToSiusRank",
            "export-ssc-users",
            "--db", Quote(options.DatabasePath),
            "--stevne-ids", Quote(FormatIds(options.StevneIds)),
            "--output", Quote(options.OutputPath ?? string.Empty),
            "--organization-name", Quote(options.OrganizationName),
            "--organization-id", Quote(options.OrganizationId),
            "--encoding", options.EncodingName
        };

        if (!string.IsNullOrWhiteSpace(options.BibMapPath))
        {
            parts.AddRange(["--bib-map", Quote(options.BibMapPath)]);
        }

        return "$ " + string.Join(' ', parts);
    }

    private static string BuildSscValidateCommand(ValidateSscOptions options)
    {
        var parts = new List<string>
        {
            "InrxToSiusRank",
            "validate-ssc",
            "--db", Quote(options.DatabasePath),
            "--stevne-ids", Quote(FormatIds(options.StevneIds)),
            "--users-csv", Quote(options.UsersCsvPath)
        };

        if (!string.IsNullOrWhiteSpace(options.BibMapPath))
        {
            parts.AddRange(["--bib-map", Quote(options.BibMapPath)]);
        }

        return "$ " + string.Join(' ', parts);
    }

    private static string BuildSscLanesCommand(ExportSscLanesOptions options)
    {
        var parts = new List<string>
        {
            "InrxToSiusRank",
            "export-ssc-lanes",
            "--db", Quote(options.DatabasePath),
            "--stevne-id", options.StevneId.ToString(CultureInfo.InvariantCulture),
            "--startlag", Quote(options.Startlag),
            "--output-dir", Quote(options.OutputDirectory),
            "--lane-count", options.LaneCount.ToString(CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(options.BibMapPath))
        {
            parts.AddRange(["--bib-map", Quote(options.BibMapPath)]);
        }

        return "$ " + string.Join(' ', parts);
    }

    private static string FormatBulkExportResult(BulkExportResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SIUS Rank import files created.");
        builder.AppendLine($"Output directory: {result.OutputDirectory}");
        if (!string.IsNullOrWhiteSpace(result.ShooterGroupsTemplatePath))
        {
            builder.AppendLine($"Shooter groups template: {Path.GetFullPath(result.ShooterGroupsTemplatePath)}");
        }

        builder.AppendLine($"Files created: {result.Files.Count}");
        foreach (var file in result.Files)
        {
            builder.AppendLine(
                $"- {Path.GetFileName(file.OutputPath)}: Stevne.Id={file.Stevne.Id}, " +
                $"{file.Ovelse.Name}, KM/NM={file.KmNmClass}, starters={file.StarterCount}");
            foreach (var warning in file.Warnings)
            {
                builder.AppendLine($"  WARNING: {warning}");
            }
        }

        return builder.ToString();
    }

    private static string FormatBibMapResult(BibMapCreateResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("bib-map.csv created/updated.");
        builder.AppendLine($"Output directory: {result.OutputDirectory}");
        builder.AppendLine($"bib-map.csv: {result.BibMapPath}");
        builder.AppendLine($"Events: {result.EventCount}");
        builder.AppendLine($"Starters: {result.StarterCount}");
        builder.AppendLine($"Unique shooters: {result.ShooterCount}");
        return builder.ToString();
    }

    private static string FormatTemplateCopyResult(SiusRankTemplateCopyResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SIUS Rank template files copied.");
        builder.AppendLine($"Source: {result.SourceDirectory}");
        builder.AppendLine($"Target: {result.TargetDirectory}");
        builder.AppendLine($"Files copied: {result.Files.Count}");
        foreach (var file in result.Files)
        {
            var action = file.Overwritten ? "overwritten" : "created";
            builder.AppendLine($"- {Path.GetFileName(file.TargetPath)} ({action})");
        }

        return builder.ToString();
    }

    private static string FormatWritebackResult(SiusRankWritebackResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine(result.Applied
            ? "SIUS Rank results written back to inrX."
            : "Dry run only. No database changes written.");
        if (!string.IsNullOrWhiteSpace(result.BackupPath))
        {
            builder.AppendLine($"Backup: {Path.GetFullPath(result.BackupPath)}");
        }

        builder.AppendLine($"Planned updates: {result.UpdateCount}");
        builder.AppendLine($"Unchanged rows: {result.UnchangedCount}");
        builder.AppendLine($"Skipped rows: {result.SkippedCount}");
        foreach (var warning in result.Warnings)
        {
            builder.AppendLine($"WARNING: {warning}");
        }

        foreach (var eventPlan in result.Events)
        {
            builder.AppendLine();
            builder.AppendLine(SiusRankWritebackReporter.FormatEventSummary(eventPlan));

            foreach (var update in eventPlan.Updates)
            {
                builder.AppendLine(
                    $"  UPDATE Resultat.Id={update.ResultatId}, Stevne.Id={update.StevneId}: " +
                    $"{update.DisplayName} bib={update.BibNumber}, " +
                    $"{update.ExistingTotal}-{update.ExistingInnerTens}x/{update.ExistingShotCount} shots -> " +
                    $"{update.NewTotal}-{update.NewInnerTens}x/{update.NewShotCount} shots");
            }

            foreach (var skipped in eventPlan.Skipped)
            {
                builder.AppendLine(
                    $"  SKIP {skipped.DisplayName} bib={skipped.BibNumber} nsf={skipped.AccreditationNumber}: " +
                    skipped.Reason);
            }

            foreach (var warning in eventPlan.Warnings)
            {
                builder.AppendLine($"  WARNING: {warning}");
            }
        }

        return builder.ToString();
    }

    private static string FormatSscUsersResult(SscUsersExportResult result)
    {
        var builder = new StringBuilder();
        var status = result.Written
            ? "SSC users CSV created."
            : string.IsNullOrWhiteSpace(result.OutputPath)
                ? "SSC users CSV dry-run."
                : "SSC users CSV not written because validation errors were found.";
        builder.AppendLine(status);
        if (!string.IsNullOrWhiteSpace(result.OutputPath))
        {
            builder.AppendLine($"Output: {result.OutputPath}");
        }

        if (!string.IsNullOrWhiteSpace(result.BibMapPath))
        {
            builder.AppendLine($"bib-map.csv: {result.BibMapPath}");
        }

        builder.AppendLine($"Users: {result.UserCount}");
        AppendSscMessages(builder, result.Messages);
        return builder.ToString();
    }

    private static string FormatSscValidationResult(SscValidationResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SSC validation complete.");
        builder.AppendLine($"Starters: {result.StarterCount}");
        builder.AppendLine($"Users: {result.UserCount}");
        AppendSscMessages(builder, result.Messages);
        return builder.ToString();
    }

    private static string FormatSscLanesResult(SscLanesExportResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SSC lane payload files created.");
        builder.AppendLine($"Output directory: {result.OutputDirectory}");
        builder.AppendLine($"Reset: {result.ResetPath}");
        builder.AppendLine($"Active lanes: {result.ActiveLanesPath}");
        builder.AppendLine($"Lane count: {result.LaneCount}");
        builder.AppendLine($"Active lanes: {result.ActiveLaneCount}");
        AppendSscMessages(builder, result.Messages);
        return builder.ToString();
    }

    private static void AppendSscMessages(StringBuilder builder, IReadOnlyList<SscValidationMessage> messages)
    {
        foreach (var message in messages)
        {
            builder.AppendLine($"{SscValidationMessageFormatter.Prefix(message)}: {message.Message}");
        }
    }

    private static string FormatDate(string value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : value;

    private static string FormatIds(IReadOnlyList<int> ids) => string.Join(",", ids);

    private static string Quote(string value) =>
        value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;

    private sealed record StevneChoice(
        int Id,
        string Name,
        string Date,
        string EventType,
        string Ovelser);

    private sealed record OvelseChoice(
        int Id,
        string Name,
        string ShortName,
        int HovedOvelseId,
        int StarterCount,
        bool IsAll = false)
    {
        public static OvelseChoice All { get; } = new(
            Id: 0,
            Name: "Alle øvelser i valgte stevner",
            ShortName: string.Empty,
            HovedOvelseId: 0,
            StarterCount: 0,
            IsAll: true);

        public override string ToString() =>
            IsAll
                ? Name
                : $"{Name} (Id {Id}, startere={StarterCount})";
    }

    private sealed record DiagnosticStevneRow(
        string Date,
        int StevneId,
        string Name,
        string EventType,
        int ExerciseCount,
        int StarterCount);

    private sealed record DiagnosticOvelseRow(
        string Date,
        int StevneId,
        string StevneName,
        int OvelseId,
        string OvelseName,
        string ShortName,
        int StarterCount);

    private sealed record SscStevneChoice(int Id, string Label)
    {
        public override string ToString() => Label;
    }

    private void AppendLog(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var trimmed = message.TrimEnd();
            _logText.Append('[')
                .Append(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture))
                .Append("] ")
                .Append(trimmed)
                .AppendLine();

            _latestLogMessage = BuildLatestLogMessage(trimmed);
            if (trimmed.Contains("ERROR:", StringComparison.OrdinalIgnoreCase))
            {
                _logErrorCount++;
            }

            _logWindow?.SetLogText(_logText.ToString());
            UpdateLogIndicators();
        });
    }

    private static string BuildLatestLogMessage(string message)
    {
        var firstLine = message
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .FirstOrDefault() ?? string.Empty;

        if (firstLine.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
        {
            return "Siste feil: " + firstLine["ERROR:".Length..].Trim();
        }

        return "Siste: " + firstLine;
    }
}
