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

            if (SeedStartLagCommand.IsCommand(args))
            {
                var seedOptions = SeedStartLagCommand.Parse(args.Skip(1).ToArray());
                var result = SeedStartLagRunner.RunAsync(seedOptions).GetAwaiter().GetResult();
                SeedStartLagReporter.Print(result);
                return 0;
            }

            if (TimetableCommand.IsCommand(args))
            {
                var timetableOptions = TimetableCommand.Parse(args.Skip(1).ToArray());
                TimetableReporter.Print(TimetableRunner.Run(timetableOptions));
                return 0;
            }

            if (SiusRankWritebackCommand.IsCommand(args))
            {
                var writebackOptions = SiusRankWritebackCommand.Parse(args.Skip(1).ToArray());
                SiusRankWritebackReporter.Print(SiusRankWritebackRunner.Run(writebackOptions));
                return 0;
            }

            var options = AppOptions.Parse(args);
            if (options.Wizard)
            {
                return WizardRunner.Run(options.DatabasePath, options.ShooterGroupsTemplatePath);
            }

            PrintBulkResult(BulkExportRunner.Run(options));

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
        catch (System.Xml.XmlException ex)
        {
            Console.Error.WriteLine($"XML error: {ex.Message}");
            return 1;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"HTTP error: {ex.Message}");
            return 1;
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
        InrxToSiusRank - export SIUS Rank starter import CSV files from an inrX SQLite database.

        Interactive:
          InrxToSiusRank --wizard
          InrxToSiusRank wizard

        Required for direct export:
          --stevne-id <id>                    inrX Stevne.Id. Use this, --stevne-ids, or --event-date/--event-name.
          --output-dir <path>                 Directory for generated CSV files.

        Common examples:
          InrxToSiusRank --db storage.db3 --stevne-id 405 --output-dir siusrank-import
          InrxToSiusRank --db storage.db3 --stevne-id 405 --ovelse Fripistol --output-dir siusrank-import
          InrxToSiusRank --db storage.db3 --stevne-ids 405-411 --output-dir siusrank-import
          InrxToSiusRank seed-startlag --db storage.db3 --stevne-ids 405-411
          InrxToSiusRank seed-startlag --db storage.db3 --stevne-ids 405-411 --apply
          InrxToSiusRank show-timetable --db storage.db3
          InrxToSiusRank writeback-siusrank --db storage.db3 --stevne-ids 413-417 --exports Rank_A\Exports
          InrxToSiusRank writeback-siusrank --db storage.db3 --stevne-ids 413-417 --exports Rank_A\Exports --bib-map siusrank-import\bib-map.csv --apply

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
          --stevne-id <id>                    Select one Stevne.Id.
          --stevne-ids <ids>                  Select several stevner and export all exercises, for example 405,406,407 or 405-411.
          --ovelse-id <id>                    Select by OvelseDef.Id.
          --ovelse <name>                     Exercise name, for example Fripistol.
          --output-dir <path>                 Output directory for generated CSV files.
          --shooter-groups-template <path>    Validate Groups against SIUS Rank ShooterGroupsTemplate.xml.
          --encoding <utf8-bom|windows-1252>  Output encoding. Default: utf8-bom.
          --help                              Show help.

        seed-startlag options:
          seed-startlag                       Preview or apply NM startlag seeding from NSF ranking.
          --db <path>                         Path to storage.db3. Overrides appsettings.
          --settings <path>                   Path to appsettings.json.
          --stevne-id <id>                    Select one Stevne.Id.
          --stevne-ids <ids>                  Select several stevner, for example 405,406,407 or 405-411.
          --ranking-period-start <iso>        Ranking period start. Default: 2025-12-31T23:00:00.000Z.
          --ranking-period-end <iso>          Ranking period end. Default: 2026-12-31T22:59:59.999Z.
          --apply                             Write updates after creating storage.db3.bak-seed-YYYYMMDD-HHMMSS.

        show-timetable options:
          show-timetable                      Show NM startlag timetable. Alias: timetable.
          --db <path>                         Path to storage.db3. Overrides appsettings.
          --settings <path>                   Path to appsettings.json.
          --stevne-id <id>                    Select one Stevne.Id.
          --stevne-ids <ids>                  Select several stevner. Default: 405-411.

        writeback-siusrank options:
          writeback-siusrank                  Preview or apply SIUS Rank Rank List Main ODF XML results back to inrX.
          --db <path>                         Path to storage.db3. Overrides appsettings.
          --settings <path>                   Path to appsettings.json.
          --exports <path>                    SIUS Rank Exports directory.
          --stevne-id <id>                    Select one inrX Stevne.Id.
          --stevne-ids <ids>                  Select several inrX stevner, for example 413-417.
          --bib-map <path>                    Optional bib-map.csv. If omitted, siusrank-import\bib-map.csv is auto-detected when possible.
          --event <name>                      Optional comma-separated SIUS event filter, for example HurtigFin_M,HurtigGrov_Apen.
          --apply                             Write updates after creating storage.db3.bak-siusrank-writeback-YYYYMMDD-HHMMSS.
        """;
}
