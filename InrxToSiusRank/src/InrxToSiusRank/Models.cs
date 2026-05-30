using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;

namespace InrxToSiusRank;

public sealed record AppOptions(
    string DatabasePath,
    int? StevneId,
    IReadOnlyList<int> StevneIds,
    DateOnly? EventDate,
    string? EventName,
    int? OvelseId,
    string? OvelseName,
    string? ShooterGroupsTemplatePath,
    string? OutputDirectory,
    string EncodingName,
    bool Wizard,
    int SilhouetteShootersPerStand = 2)
{
    public static AppOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count > 0 && args[0].Equals("wizard", StringComparison.OrdinalIgnoreCase))
        {
            args = args.Skip(1).Prepend("--wizard").ToArray();
        }

        var cli = AppCommandLine.Create();
        var parseResult = cli.RootCommand.Parse(args.ToArray());
        if (parseResult.Errors.Count > 0)
        {
            throw new ArgumentException(
                string.Join(Environment.NewLine, parseResult.Errors.Select(error => error.Message)));
        }

        var settingsPath = parseResult.GetValue(cli.SettingsOption);
        var settings = AppSettings.Load(settingsPath);

        var configuredDatabasePath = parseResult.GetValue(cli.DatabaseOption);
        var databasePath = !string.IsNullOrWhiteSpace(configuredDatabasePath)
            ? configuredDatabasePath
            : settings.ResolveDatabasePath();
        if (!File.Exists(databasePath))
        {
            throw new ArgumentException(
                $"Database file does not exist: {databasePath}. " +
                "Use --db or configure Paths:Inrx/Paths:Database in appsettings.json.");
        }

        if (parseResult.GetValue(cli.WizardOption))
        {
            return new AppOptions(
                databasePath,
                StevneId: null,
                StevneIds: [],
                EventDate: null,
                EventName: null,
                OvelseId: null,
                OvelseName: null,
                ShooterGroupsTemplatePath: ResolveShooterGroupsTemplatePath(parseResult, cli, settings),
                OutputDirectory: null,
                EncodingName: CsvEncoding.Utf8Bom,
                Wizard: true);
        }

        var stevneId = ParseNullableInt(parseResult.GetValue(cli.StevneIdOption), "stevne-id");
        var stevneIdsValue = parseResult.GetValue(cli.StevneIdsOption);
        var stevneIds = !string.IsNullOrWhiteSpace(stevneIdsValue)
            ? ParseIdList(stevneIdsValue, "stevne-ids")
            : [];
        var eventDate = ParseNullableDate(parseResult.GetValue(cli.EventDateOption), "event-date");
        var eventName = parseResult.GetValue(cli.EventNameOption);

        if (stevneId is null && stevneIds.Count == 0 && eventDate is null)
        {
            throw new ArgumentException("Use --stevne-id, --stevne-ids, or --event-date to select an event.");
        }

        if (stevneId is not null && stevneIds.Count > 0)
        {
            throw new ArgumentException("Use either --stevne-id or --stevne-ids, not both.");
        }

        if (stevneIds.Count > 0 && eventDate is not null)
        {
            throw new ArgumentException("Use either --stevne-ids or --event-date, not both.");
        }

        var outputDirectory = parseResult.GetValue(cli.OutputDirectoryOption);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Use --output-dir to choose where CSV files should be written.");
        }

        var encoding = parseResult.GetValue(cli.EncodingOption);
        var encodingName = !string.IsNullOrWhiteSpace(encoding)
            ? CsvEncoding.NormalizeName(encoding)
            : CsvEncoding.Utf8Bom;
        var silhouetteShootersPerStand = ParseSilhouetteShootersPerStand(
            parseResult.GetValue(cli.SilhouetteShootersPerStandOption));

        var ovelseId = ParseNullableInt(parseResult.GetValue(cli.OvelseIdOption), "ovelse-id");
        var ovelseName = parseResult.GetValue(cli.OvelseNameOption);
        var shooterGroupsTemplatePath = ResolveShooterGroupsTemplatePath(parseResult, cli, settings);

        return new AppOptions(
            databasePath,
            stevneId,
            stevneIds,
            eventDate,
            string.IsNullOrWhiteSpace(eventName) ? null : eventName,
            ovelseId,
            string.IsNullOrWhiteSpace(ovelseName) ? null : ovelseName,
            string.IsNullOrWhiteSpace(shooterGroupsTemplatePath) ? null : shooterGroupsTemplatePath,
            outputDirectory.Trim(),
            encodingName,
            Wizard: false,
            silhouetteShootersPerStand);
    }

    private static string? ResolveShooterGroupsTemplatePath(
        ParseResult parseResult,
        AppCommandLine cli,
        AppSettings settings)
    {
        var shooterGroupsTemplatePath = parseResult.GetValue(cli.ShooterGroupsTemplateOption);

        if (string.IsNullOrWhiteSpace(shooterGroupsTemplatePath))
        {
            var configuredShooterGroupsTemplatePath = settings.ResolveShooterGroupsTemplatePath();
            return File.Exists(configuredShooterGroupsTemplatePath)
                ? configuredShooterGroupsTemplatePath
                : null;
        }

        if (!File.Exists(shooterGroupsTemplatePath))
        {
            throw new ArgumentException($"Shooter groups template file does not exist: {shooterGroupsTemplatePath}");
        }

        return shooterGroupsTemplatePath;
    }

    private static int? ParseNullableInt(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new ArgumentException($"Option '--{name}' must be an integer.");
        }

        return parsed;
    }

    private static DateOnly? ParseNullableDate(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            throw new ArgumentException($"Option '--{name}' must use yyyy-MM-dd.");
        }

        return parsed;
    }

    private static int ParseSilhouetteShootersPerStand(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 2;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ||
            parsed is not (1 or 2))
        {
            throw new ArgumentException("Option '--silhouette-shooters-per-stand' must be 1 or 2.");
        }

        return parsed;
    }

    private static IReadOnlyList<int> ParseIdList(string value, string name)
    {
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
                    throw new ArgumentException($"Option '--{name}' has invalid id range '{item}'.");
                }

                ids.AddRange(Enumerable.Range(from, to - from + 1));
                continue;
            }

            if (!int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                throw new ArgumentException($"Option '--{name}' must contain comma-separated integer ids or ranges.");
            }

            ids.Add(id);
        }

        return ids.Distinct().ToList();
    }

    private sealed record AppCommandLine(
        RootCommand RootCommand,
        Option<string?> SettingsOption,
        Option<string?> DatabaseOption,
        Option<bool> WizardOption,
        Option<string?> EventDateOption,
        Option<string?> EventNameOption,
        Option<string?> StevneIdOption,
        Option<string?> StevneIdsOption,
        Option<string?> OvelseIdOption,
        Option<string?> OvelseNameOption,
        Option<string?> OutputDirectoryOption,
        Option<string?> ShooterGroupsTemplateOption,
        Option<string?> EncodingOption,
        Option<string?> SilhouetteShootersPerStandOption)
    {
        public static AppCommandLine Create()
        {
            var settingsOption = new Option<string?>("--settings")
            {
                Description = "Path to appsettings.json."
            };
            var databaseOption = new Option<string?>("--db")
            {
                Description = "Path to storage.db3. Overrides appsettings."
            };
            var wizardOption = new Option<bool>("--wizard")
            {
                Description = "Start interactive Spectre.Console wizard."
            };
            var eventDateOption = new Option<string?>("--event-date")
            {
                Description = "Select event by date."
            };
            var eventNameOption = new Option<string?>("--event-name")
            {
                Description = "Select event by name text together with --event-date."
            };
            var stevneIdOption = new Option<string?>("--stevne-id")
            {
                Description = "inrX Stevne.Id."
            };
            var stevneIdsOption = new Option<string?>("--stevne-ids")
            {
                Description = "Select stevner, for example 405,406,407 or 405-411."
            };
            var ovelseIdOption = new Option<string?>("--ovelse-id")
            {
                Description = "Select by OvelseDef.Id."
            };
            var ovelseNameOption = new Option<string?>("--ovelse")
            {
                Description = "Exercise name, for example Fripistol."
            };
            var outputDirectoryOption = new Option<string?>("--output-dir")
            {
                Description = "Output directory for generated CSV files."
            };
            var shooterGroupsTemplateOption = new Option<string?>("--shooter-groups-template", "--shooter-groups")
            {
                Description = "Validate Groups against SIUS Rank ShooterGroupsTemplate.xml."
            };
            var encodingOption = new Option<string?>("--encoding")
            {
                Description = "Output encoding. Default: utf8-bom."
            };
            var silhouetteShootersPerStandOption = new Option<string?>("--silhouette-shooters-per-stand")
            {
                Description = "Silhuett target layout: 1 uses middle targets, 2 uses side targets. Default: 2."
            };

            var rootCommand = new RootCommand("Export SIUS Rank starter import CSV files from an inrX SQLite database.")
            {
                settingsOption,
                databaseOption,
                wizardOption,
                eventDateOption,
                eventNameOption,
                stevneIdOption,
                stevneIdsOption,
                ovelseIdOption,
                ovelseNameOption,
                outputDirectoryOption,
                shooterGroupsTemplateOption,
                encodingOption,
                silhouetteShootersPerStandOption
            };

            return new AppCommandLine(
                rootCommand,
                settingsOption,
                databaseOption,
                wizardOption,
                eventDateOption,
                eventNameOption,
                stevneIdOption,
                stevneIdsOption,
                ovelseIdOption,
                ovelseNameOption,
                outputDirectoryOption,
                shooterGroupsTemplateOption,
                encodingOption,
                silhouetteShootersPerStandOption);
        }
    }
}

public sealed record StevneInfo(int Id, string Name, string Date, int ArrangementId);

public sealed record OvelseInfo(int Id, string Name, string ShortName, int HovedOvelseId);

public sealed record OvelseSummary(int Id, string Name, string ShortName, int HovedOvelseId, int StarterCount);

public sealed record KmNmClassSummary(string Name, int StarterCount, string Relays);

public sealed record BulkExportFileResult(
    StevneInfo Stevne,
    OvelseInfo Ovelse,
    string KmNmClass,
    int StarterCount,
    string OutputPath,
    IReadOnlyList<string> Warnings);

public sealed record BulkExportResult(
    string OutputDirectory,
    string? ShooterGroupsTemplatePath,
    IReadOnlyList<BulkExportFileResult> Files);

public sealed record BibMapCreateResult(
    string OutputDirectory,
    string BibMapPath,
    int EventCount,
    int StarterCount,
    int ShooterCount);

public sealed record InrxStarter(
    int ResultatId,
    int DeltakerId,
    int Standplass,
    string SkivenrFra,
    string SkivenrTil,
    int? Relay,
    string RelayDate,
    string NsfId,
    string AccreditationNumber,
    string FirstName,
    string LastName,
    string BirthDay,
    string Gender,
    string Land,
    string ClubName,
    string ClubShortName,
    string InrxClass,
    string KmNmClass,
    string DmClass,
    string OvelseName,
    string StevneName);

public sealed record SiusRankStarter(
    string StartNumber,
    string AccreditationNumber,
    string IssfId,
    string DisplayNameLong,
    string DisplayName,
    string FirstName,
    string Name,
    string BirthDay,
    string Gender,
    string Nation,
    string BibNumber,
    string TargetNumber,
    string Relay,
    string TeamIndex,
    string DuellIndex,
    string Groups,
    string Comment,
    string StarterId,
    string TeamPosition,
    string Team,
    string TeamDisplay,
    string TeamDuellIndex,
    string TeamComment)
{
    public string[] ToFields() =>
    [
        StartNumber,
        AccreditationNumber,
        IssfId,
        DisplayNameLong,
        DisplayName,
        FirstName,
        Name,
        BirthDay,
        Gender,
        Nation,
        BibNumber,
        TargetNumber,
        Relay,
        TeamIndex,
        DuellIndex,
        Groups,
        Comment,
        StarterId,
        TeamPosition,
        Team,
        TeamDisplay,
        TeamDuellIndex,
        TeamComment
    ];
}
