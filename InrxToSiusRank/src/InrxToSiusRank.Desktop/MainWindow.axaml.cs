using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace InrxToSiusRank.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        InitializeDefaults();
        WireEvents();
        Closing += (_, _) => SaveDesktopSettings(logWarning: false);
    }

    private TextBox DatabasePathInput => Get<TextBox>("DatabasePathBox");

    private TextBox StevneIdsInput => Get<TextBox>("StevneIdsBox");

    private ComboBox EncodingInput => Get<ComboBox>("EncodingBox");

    private TextBox OutputDirectoryInput => Get<TextBox>("OutputDirectoryBox");

    private TextBox ShooterGroupsTemplateInput => Get<TextBox>("ShooterGroupsTemplateBox");

    private TextBox OvelseFilterInput => Get<TextBox>("OvelseFilterBox");

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

    private TextBox LogInput => Get<TextBox>("LogBox");

    private TextBlock StatusLabel => Get<TextBlock>("StatusText");

    private T Get<T>(string name)
        where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"Missing control '{name}'.");

    private void InitializeDefaults()
    {
        StevneIdsInput.Text = "413-417";
        OutputDirectoryInput.Text = Path.Combine(Environment.CurrentDirectory, "siusrank-import");
        SscOrganizationNameInput.Text = "Legacy";
        SscOrganizationIdInput.Text = "f95a2bc3-79bd-4c24-98b6-4e17f99bbfaf";
        SscOutputDirectoryInput.Text = Path.Combine(Environment.CurrentDirectory, "ssc-setup");
        SscBibMapPathInput.Text = Path.Combine(Environment.CurrentDirectory, "siusrank-import", ChampionshipStartNumbers.BibMapFileName);
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
        SetTextIfPresent(DatabasePathInput, desktopSettings.DatabasePath);
        SetTextIfPresent(OutputDirectoryInput, desktopSettings.OutputDirectory);
        SetTextIfPresent(ShooterGroupsTemplateInput, desktopSettings.ShooterGroupsTemplatePath);
        SetTextIfPresent(ExportsDirectoryInput, desktopSettings.ExportsDirectory);
        SetTextIfPresent(BibMapPathInput, desktopSettings.BibMapPath);
        SetTextIfPresent(SscBibMapPathInput, desktopSettings.SscBibMapPath);
        SetTextIfPresent(SscOutputDirectoryInput, desktopSettings.SscOutputDirectory);
        SetTextIfPresent(SscUsersCsvPathInput, desktopSettings.SscUsersCsvPath);
        SetTextIfPresent(SscStartlagInput, desktopSettings.SscStartlag);
        SetTextIfPresent(SscOrganizationNameInput, desktopSettings.SscOrganizationName);
        SetTextIfPresent(SscOrganizationIdInput, desktopSettings.SscOrganizationId);
        SetSscLaneCount(desktopSettings.SscLaneCount);
        SetTextIfPresent(StevneIdsInput, desktopSettings.StevneIds);
        SetTextIfPresent(OvelseFilterInput, desktopSettings.OvelseFilter);
        SetTextIfPresent(EventFilterInput, desktopSettings.EventFilter);
        SetEncoding(desktopSettings.EncodingName);
    }

    private void WireEvents()
    {
        Get<Button>("BrowseDatabaseButton").Click += async (_, _) =>
            await BrowseFileAsync(DatabasePathInput, "Select storage.db3", "SQLite database", ["*.db3", "*.sqlite", "*.sqlite3"]);
        Get<Button>("BrowseShooterGroupsButton").Click += async (_, _) =>
            await BrowseFileAsync(ShooterGroupsTemplateInput, "Select ShooterGroupsTemplate.xml", "XML", ["*.xml"]);
        Get<Button>("BrowseBibMapButton").Click += async (_, _) =>
            await BrowseFileAsync(BibMapPathInput, "Select bib-map.csv", "CSV", ["*.csv"]);
        Get<Button>("BrowseOutputButton").Click += async (_, _) =>
            await BrowseFolderAsync(OutputDirectoryInput, "Select SIUS Rank import output directory");
        Get<Button>("BrowseExportsButton").Click += async (_, _) =>
            await BrowseFolderAsync(ExportsDirectoryInput, "Select SIUS Rank Exports directory");
        Get<Button>("BrowseSscBibMapButton").Click += async (_, _) =>
            await BrowseFileAsync(SscBibMapPathInput, "Select bib-map.csv", "CSV", ["*.csv"]);
        Get<Button>("BrowseSscOutputButton").Click += async (_, _) =>
            await BrowseFolderAsync(SscOutputDirectoryInput, "Select SSC setup output directory");
        Get<Button>("BrowseSscUsersCsvButton").Click += async (_, _) =>
            await BrowseFileAsync(SscUsersCsvPathInput, "Select SSC users CSV", "CSV", ["*.csv"]);

        Get<Button>("LoadDatabaseButton").Click += async (_, _) => await RunSafelyAsync("Inspecting database", InspectDatabaseAsync);
        Get<Button>("CopyTemplatesButton").Click += async (_, _) => await RunSafelyAsync("Copying templates", CopyTemplatesToSiusRankAsync);
        Get<Button>("CreateBibMapButton").Click += async (_, _) => await RunSafelyAsync("Creating bib-map.csv", RunCreateBibMapAsync);
        Get<Button>("RunExportButton").Click += async (_, _) => await RunSafelyAsync("Creating CSV files", RunExportAsync);
        Get<Button>("RunWritebackPreviewButton").Click += async (_, _) => await RunSafelyAsync("Running writeback dry-run", () => RunWritebackAsync(apply: false));
        Get<Button>("RunWritebackApplyButton").Click += async (_, _) => await RunSafelyAsync("Applying writeback", () => RunWritebackAsync(apply: true));
        Get<Button>("RunSscUsersButton").Click += async (_, _) => await RunSafelyAsync("Exporting SSC users", RunSscUsersAsync);
        Get<Button>("RunSscValidateButton").Click += async (_, _) => await RunSafelyAsync("Validating SSC setup", RunSscValidateAsync);
        Get<Button>("RunSscLanesButton").Click += async (_, _) => await RunSafelyAsync("Exporting SSC lanes", RunSscLanesAsync);
        Get<Button>("ShowStevnerButton").Click += async (_, _) => await RunSafelyAsync("Loading stevner", ShowRecentStevnerAsync);
        Get<Button>("ShowSelectedOvelserButton").Click += async (_, _) => await RunSafelyAsync("Loading øvelser", ShowSelectedOvelserAsync);
        Get<Button>("ClearLogButton").Click += (_, _) => LogInput.Text = string.Empty;
    }

    private async Task BrowseFileAsync(TextBox target, string title, string typeName, IReadOnlyList<string> patterns)
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
            target.Text = path;
            SaveDesktopSettings();
        }
    }

    private async Task BrowseFolderAsync(TextBox target, string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            target.Text = path;
            SaveDesktopSettings();
        }
    }

    private async Task RunSafelyAsync(string status, Func<Task> action)
    {
        StatusLabel.Text = status;
        SaveDesktopSettings();
        try
        {
            await action();
            StatusLabel.Text = "Klar";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException or System.Xml.XmlException)
        {
            AppendLog($"ERROR: {ex.Message}");
            StatusLabel.Text = "Feil";
        }
        catch (Exception ex)
        {
            AppendLog($"ERROR: {ex}");
            StatusLabel.Text = "Feil";
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

    private Task ShowRecentStevnerAsync() => InspectDatabaseAsync();

    private Task ShowSelectedOvelserAsync()
    {
        var databasePath = RequireExistingFile(DatabasePathInput.Text, "storage.db3");
        var ids = ParseIdList(StevneIdsInput.Text, "Stevne ids");
        return Task.Run(() =>
        {
            using var repository = new InrxRepository(databasePath);
            var builder = new StringBuilder();
            builder.AppendLine("Selected øvelser:");
            foreach (var id in ids)
            {
                var stevne = repository.GetStevneById(id);
                builder.AppendLine();
                builder.AppendLine($"{stevne.Id} {FormatDate(stevne.Date)} {stevne.Name}");
                foreach (var ovelse in repository.GetOvelserForStevne(id))
                {
                    builder.AppendLine($"  {ovelse.Id,3}  {ovelse.Name,-18} starters={ovelse.StarterCount}");
                }
            }

            AppendLog(builder.ToString());
        });
    }

    private Task RunExportAsync()
    {
        var options = BuildExportOptions();
        var command = BuildExportCommand(options);
        AppendLog(command);

        return Task.Run(() =>
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
        var options = BuildExportOptions(includeTemplate: false);
        var command = BuildBibMapCommand(options);
        AppendLog(command);

        var result = await Task.Run(() => BulkExportRunner.CreateBibMap(options));
        BibMapPathInput.Text = result.BibMapPath;
        SaveDesktopSettings();
        AppendLog(FormatBibMapResult(result));
    }

    private Task RunWritebackAsync(bool apply)
    {
        var options = BuildWritebackOptions(apply);
        var command = BuildWritebackCommand(options);
        AppendLog(command);

        return Task.Run(() =>
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
                    SscUsersCsvPathInput.Text = result.OutputPath;
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

    private AppOptions BuildExportOptions(bool includeTemplate = true)
    {
        var databasePath = RequireExistingFile(DatabasePathInput.Text, "storage.db3");
        var outputDirectory = RequireText(OutputDirectoryInput.Text, "Output directory");
        var templatePath = !includeTemplate || string.IsNullOrWhiteSpace(ShooterGroupsTemplateInput.Text)
            ? null
            : RequireExistingFile(ShooterGroupsTemplateInput.Text, "Shooter groups XML");
        var ids = ParseIdList(StevneIdsInput.Text, "Stevne ids");
        var ovelseFilter = OvelseFilterInput.Text?.Trim();
        int? ovelseId = null;
        string? ovelseName = null;
        if (!string.IsNullOrWhiteSpace(ovelseFilter))
        {
            if (int.TryParse(ovelseFilter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                ovelseId = parsed;
            }
            else
            {
                ovelseName = ovelseFilter;
            }
        }

        return new AppOptions(
            databasePath,
            StevneId: null,
            StevneIds: ids,
            EventDate: null,
            EventName: null,
            OvelseId: ovelseId,
            OvelseName: ovelseName,
            ShooterGroupsTemplatePath: templatePath,
            OutputDirectory: outputDirectory,
            EncodingName: SelectedEncoding(),
            Wizard: false);
    }

    private SiusRankWritebackOptions BuildWritebackOptions(bool apply)
    {
        var eventFilters = ParseEventFilters(EventFilterInput.Text);
        return new SiusRankWritebackOptions(
            RequireExistingFile(DatabasePathInput.Text, "storage.db3"),
            RequireExistingDirectory(ExportsDirectoryInput.Text, "Exports directory"),
            ParseIdList(StevneIdsInput.Text, "Stevne ids"),
            string.IsNullOrWhiteSpace(BibMapPathInput.Text)
                ? null
                : RequireExistingFile(BibMapPathInput.Text, "bib-map.csv"),
            eventFilters,
            apply);
    }

    private ExportSscUsersOptions BuildSscUsersOptions()
    {
        var outputDirectory = RequireText(SscOutputDirectoryInput.Text, "SSC output directory");
        var outputPath = Path.Combine(outputDirectory, "ssc-users.csv");
        return new ExportSscUsersOptions(
            RequireExistingFile(DatabasePathInput.Text, "storage.db3"),
            ParseIdList(StevneIdsInput.Text, "Stevne ids"),
            CleanSetting(SscBibMapPathInput.Text),
            outputPath,
            RequireText(SscOrganizationNameInput.Text, "SSC organization name"),
            RequireText(SscOrganizationIdInput.Text, "SSC organization id"),
            SelectedEncoding());
    }

    private ValidateSscOptions BuildSscValidateOptions()
    {
        var usersCsvPath = string.IsNullOrWhiteSpace(SscUsersCsvPathInput.Text)
            ? Path.Combine(RequireText(SscOutputDirectoryInput.Text, "SSC output directory"), "ssc-users.csv")
            : SscUsersCsvPathInput.Text;

        return new ValidateSscOptions(
            RequireExistingFile(DatabasePathInput.Text, "storage.db3"),
            ParseIdList(StevneIdsInput.Text, "Stevne ids"),
            CleanSetting(SscBibMapPathInput.Text),
            RequireExistingFile(usersCsvPath, "SSC users CSV"));
    }

    private ExportSscLanesOptions BuildSscLanesOptions()
    {
        var ids = ParseIdList(StevneIdsInput.Text, "Stevne ids");
        if (ids.Count != 1)
        {
            throw new ArgumentException("SSC lanes export requires exactly one Stevne.Id in the top Stevne ids field.");
        }

        return new ExportSscLanesOptions(
            RequireExistingFile(DatabasePathInput.Text, "storage.db3"),
            ids[0],
            RequireText(SscStartlagInput.Text, "SSC startlag"),
            CleanSetting(SscBibMapPathInput.Text),
            RequireText(SscOutputDirectoryInput.Text, "SSC output directory"),
            SelectedSscLaneCount());
    }

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
            BuildDesktopSettings().Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (logWarning)
            {
                AppendLog($"WARNING: Could not save desktop settings: {ex.Message}");
            }
        }
    }

    private DesktopSettings BuildDesktopSettings() => new(
        DatabasePath: CleanSetting(DatabasePathInput.Text),
        OutputDirectory: CleanSetting(OutputDirectoryInput.Text),
        ShooterGroupsTemplatePath: CleanSetting(ShooterGroupsTemplateInput.Text),
        ExportsDirectory: CleanSetting(ExportsDirectoryInput.Text),
        BibMapPath: CleanSetting(BibMapPathInput.Text),
        SscBibMapPath: CleanSetting(SscBibMapPathInput.Text),
        SscOutputDirectory: CleanSetting(SscOutputDirectoryInput.Text),
        SscUsersCsvPath: CleanSetting(SscUsersCsvPathInput.Text),
        SscStartlag: CleanSetting(SscStartlagInput.Text),
        SscLaneCount: SelectedSscLaneCount().ToString(CultureInfo.InvariantCulture),
        SscOrganizationName: CleanSetting(SscOrganizationNameInput.Text),
        SscOrganizationId: CleanSetting(SscOrganizationIdInput.Text),
        StevneIds: CleanSetting(StevneIdsInput.Text),
        EncodingName: SelectedEncoding(),
        OvelseFilter: CleanSetting(OvelseFilterInput.Text),
        EventFilter: CleanSetting(EventFilterInput.Text));

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

    private static IReadOnlyList<int> ParseIdList(string? value, string name)
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

    private static string RequireExistingFile(string? value, string label)
    {
        var path = RequireText(value, label);
        if (!File.Exists(path))
        {
            throw new ArgumentException($"{label} does not exist: {path}");
        }

        return Path.GetFullPath(path);
    }

    private static string RequireExistingDirectory(string? value, string label)
    {
        var path = RequireText(value, label);
        if (!Directory.Exists(path))
        {
            throw new ArgumentException($"{label} does not exist: {path}");
        }

        return Path.GetFullPath(path);
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

    private void AppendLog(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var text = LogInput.Text ?? string.Empty;
            if (text.Length > 0 && !text.EndsWith(Environment.NewLine, StringComparison.Ordinal))
            {
                text += Environment.NewLine;
            }

            LogInput.Text = text + $"[{DateTime.Now:HH:mm:ss}] " + message.TrimEnd() + Environment.NewLine;
            LogInput.CaretIndex = LogInput.Text.Length;
        });
    }
}
