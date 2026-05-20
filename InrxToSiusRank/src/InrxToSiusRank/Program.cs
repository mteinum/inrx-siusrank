using System.Text;
using Microsoft.Data.Sqlite;

namespace InrxToSiusRank;

public static class Program
{
    public static int Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        try
        {
            if (args.Length == 0 || args.Any(arg => arg is "-h" or "--help"))
            {
                Console.WriteLine(Usage.Text);
                return args.Length == 0 ? 1 : 0;
            }

            var options = AppOptions.Parse(args);
            if (options.Wizard)
            {
                return WizardRunner.Run(options.DatabasePath, options.ShooterGroupsTemplatePath);
            }

            if (options.AllClasses)
            {
                PrintBulkResult(BulkExportRunner.Run(options));
            }
            else
            {
                PrintResult(ExportRunner.Run(options));
            }

            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine(Usage.Text);
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (SqliteException ex)
        {
            Console.Error.WriteLine($"SQLite error: {ex.Message}");
            return 1;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"File error: {ex.Message}");
            return 1;
        }
    }

    public static void PrintResult(ExportResult result)
    {
        Console.WriteLine("SIUS Rank import data created.");
        Console.WriteLine($"Event: {result.Stevne.Name} ({result.Stevne.Date})");
        Console.WriteLine($"Exercise: {result.Ovelse.Name} (OvelseDef.Id={result.Ovelse.Id})");
        Console.WriteLine($"KM/NM class: {result.KmNmClass}");
        Console.WriteLine(result.SiusGroupOverride is null
            ? "SIUS group: from KM/NM class"
            : $"SIUS group override: {result.SiusGroupOverride}");
        if (result.ShooterGroupsTemplatePath is not null)
        {
            Console.WriteLine($"Shooter groups template: {Path.GetFullPath(result.ShooterGroupsTemplatePath)}");
        }

        Console.WriteLine($"Starters exported: {result.StarterCount}");
        if (result.OutputPath is not null)
        {
            Console.WriteLine($"File: {Path.GetFullPath(result.OutputPath)}");
        }

        if (result.CopiedToClipboard)
        {
            Console.WriteLine("Clipboard: copied");
        }

        foreach (var warning in result.Warnings)
        {
            Console.WriteLine($"WARNING: {warning}");
        }
    }

    public static void PrintBulkResult(BulkExportResult result)
    {
        Console.WriteLine("SIUS Rank import files created.");
        Console.WriteLine($"Output directory: {result.OutputDirectory}");
        if (result.ShooterGroupsTemplatePath is not null)
        {
            Console.WriteLine($"Shooter groups template: {Path.GetFullPath(result.ShooterGroupsTemplatePath)}");
        }

        Console.WriteLine($"Files created: {result.Files.Count}");
        foreach (var file in result.Files)
        {
            Console.WriteLine(
                $"- {Path.GetFileName(file.OutputPath)}: Stevne.Id={file.Stevne.Id}, " +
                $"{file.Ovelse.Name}, KM/NM={file.KmNmClass}, starters={file.StarterCount}");
            foreach (var warning in file.Warnings)
            {
                Console.WriteLine($"  WARNING: {warning}");
            }
        }
    }
}

internal static class Usage
{
    public const string Text =
        """
        InrxToSiusRank - export SIUS Rank starter import CSV from an inrX SQLite database.

        Interactive:
          InrxToSiusRank --wizard
          InrxToSiusRank wizard

        Required for direct export:
          --stevne-id <id>                    inrX Stevne.Id. Use this or --event-date/--event-name.
          --ovelse <name>                     Exercise name, for example Fripistol. Use this or --ovelse-id.
          --output <path>                     Output CSV path. Optional when --clipboard is used.

        Common examples:
          InrxToSiusRank --db storage.db3 --stevne-id 405 --ovelse Fripistol --klasse Å --output NM50FRI_APEN_import.csv
          InrxToSiusRank --db storage.db3 --stevne-id 405 --ovelse Fripistol --klasse Å --clipboard
          InrxToSiusRank --db storage.db3 --stevne-ids 405-411 --all-classes --output-dir siusrank-import

        appsettings.json:
          Loaded from the current directory or executable directory.
          Default Paths:Inrx is C:\Program Files (x86)\inrX.
          Default Paths:SiusRankTemplates is C:\SIUS\SiusRank\Resources\Templates.

        Options:
          --settings <path>                   Path to appsettings.json.
          --db <path>                         Path to storage.db3. Overrides appsettings.
          --wizard                            Start interactive Spectre.Console wizard.
          --event-date <yyyy-MM-dd>           Select event by date.
          --event-name <text>                 Select event by name text together with --event-date.
          --stevne-ids <ids>                  Bulk select stevner, for example 405,406,407 or 405-411.
          --ovelse-id <id>                    Select by OvelseDef.Id.
          --klasse <value>                    Filter by inrX KM/NM class, for example Å, V55, V65.
          --km-nm-klasse <value>              Same as --klasse.
          --all-classes                       Export one file per KM/NM class.
          --output-dir <path>                 Output directory for --all-classes.
          --sius-group <value>                Override SIUS Rank Groups value. Default: derive from KM/NM class.
          --shooter-groups-template <path>    Validate Groups against SIUS Rank ShooterGroupsTemplate.xml.
          --clipboard                         Copy import data to clipboard for "Update starters from clipboard".
          --copy-to-clipboard                 Same as --clipboard.
          --encoding <utf8-bom|windows-1252>  Output encoding. Default: utf8-bom.
          --include-club-team                 Fill Team and TeamDisplay from club name. Default.
          --no-include-club-team              Leave Team and TeamDisplay empty.
          --help                              Show help.
        """;
}
