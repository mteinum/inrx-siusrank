using Spectre.Console;

namespace InrxToSiusRank;

public static class WizardRunner
{
    public static int Run(string databasePath, string? defaultShooterGroupsTemplatePath = null)
    {
        using var repository = new InrxRepository(databasePath);

        AnsiConsole.Write(new Rule("[bold]inrX til SIUS Rank[/]").RuleStyle("green"));
        AnsiConsole.MarkupLine($"Database: [grey]{Markup.Escape(Path.GetFullPath(databasePath))}[/]");
        AnsiConsole.WriteLine();

        var filter = AnsiConsole.Prompt(
            new TextPrompt<string>("Filter for stevner [grey](navn eller dato, tom = siste 100)[/]:")
                .AllowEmpty());

        var stevner = repository.SearchStevner(filter, limit: 100);
        if (stevner.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Ingen stevner funnet.[/]");
            return 1;
        }

        var stevne = AnsiConsole.Prompt(
            new SelectionPrompt<StevneInfo>()
                .Title("Velg [green]stevne[/]")
                .PageSize(15)
                .EnableSearch()
                .MoreChoicesText("[grey](Bruk piltaster, skriv for å søke)[/]")
                .UseConverter(FormatStevne)
                .AddChoices(stevner));

        var ovelser = repository.GetOvelserForStevne(stevne.Id);
        if (ovelser.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Ingen øvelser funnet for valgt stevne.[/]");
            return 1;
        }

        var ovelse = AnsiConsole.Prompt(
            new SelectionPrompt<OvelseSummary>()
                .Title("Velg [green]øvelse[/]")
                .PageSize(12)
                .EnableSearch()
                .UseConverter(FormatOvelse)
                .AddChoices(ovelser));

        var classes = repository.GetKmNmClasses(stevne.Id, ovelse.Id);
        if (classes.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Ingen KM/NM-klasser funnet for valgt øvelse.[/]");
            return 1;
        }

        WriteClassTable(classes);

        var selectedClass = AnsiConsole.Prompt(
            new SelectionPrompt<KmNmClassSummary>()
                .Title("Velg [green]KM/NM-klasse[/]")
                .PageSize(12)
                .EnableSearch()
                .UseConverter(FormatClass)
                .AddChoices(classes));

        var destination = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Velg [green]importmåte[/]")
                .AddChoices("File", "Clipboard", "Both"));

        var outputPath = NeedsFile(destination)
            ? AnsiConsole.Prompt(
            new TextPrompt<string>("Output-fil:")
                    .DefaultValue(OutputFileName.ForImport(stevne, ovelse, selectedClass)))
            : null;

        var includeTeam = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Fyll Team/TeamDisplay med klubb?")
                .AddChoices("No", "Yes")) == "Yes";

        var shooterGroupsPrompt = string.IsNullOrWhiteSpace(defaultShooterGroupsTemplatePath)
            ? "ShooterGroupsTemplate.xml for validering [grey](tom = ingen)[/]:"
            : "ShooterGroupsTemplate.xml for validering [grey](tom = standard fra appsettings, '-' = ingen)[/]:";
        var shooterGroupsTemplateInput = AnsiConsole.Prompt(
            new TextPrompt<string>(shooterGroupsPrompt)
                .AllowEmpty());
        var shooterGroupsTemplatePath = shooterGroupsTemplateInput.Trim() == "-"
            ? null
            : string.IsNullOrWhiteSpace(shooterGroupsTemplateInput)
                ? defaultShooterGroupsTemplatePath
                : shooterGroupsTemplateInput;

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(
                $"Stevne: {Markup.Escape(stevne.Name)}\n" +
                $"Øvelse: {Markup.Escape(ovelse.Name)}\n" +
                $"KM/NM: {Markup.Escape(selectedClass.Name)} ({selectedClass.StarterCount} startere)\n" +
                $"Import: {Markup.Escape(destination)}\n" +
                $"Shooter groups: {Markup.Escape(string.IsNullOrWhiteSpace(shooterGroupsTemplatePath) ? "ikke validert" : shooterGroupsTemplatePath)}")
            .Header("Oppsummering")
            .BorderColor(Color.Green));

        if (!AnsiConsole.Confirm("Lage SIUS Rank importdata?", defaultValue: true))
        {
            AnsiConsole.MarkupLine("[yellow]Avbrutt.[/]");
            return 1;
        }

        var options = new AppOptions(
            databasePath,
            StevneId: stevne.Id,
            StevneIds: [],
            EventDate: null,
            EventName: null,
            OvelseId: ovelse.Id,
            OvelseName: null,
            KmNmClass: selectedClass.Name,
            SiusGroupOverride: null,
            ShooterGroupsTemplatePath: shooterGroupsTemplatePath,
            OutputDirectory: null,
            OutputPath: outputPath,
            CopyToClipboard: NeedsClipboard(destination),
            EncodingName: CsvEncoding.Utf8Bom,
            IncludeClubTeam: includeTeam,
            AllClasses: false,
            Wizard: false);

        var result = ExportRunner.Run(options);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]SIUS Rank importdata opprettet.[/]");
        Program.PrintResult(result);
        return 0;
    }

    private static void WriteClassTable(IReadOnlyList<KmNmClassSummary> classes)
    {
        var table = new Table()
            .Title("KM/NM-klasser")
            .AddColumn("Klasse")
            .AddColumn(new TableColumn("Antall").RightAligned())
            .AddColumn("Relays");

        foreach (var item in classes)
        {
            table.AddRow(
                Markup.Escape(item.Name),
                item.StarterCount.ToString(),
                Markup.Escape(string.IsNullOrWhiteSpace(item.Relays) ? "-" : item.Relays));
        }

        AnsiConsole.Write(table);
    }

    private static string FormatStevne(StevneInfo stevne)
    {
        var date = stevne.Date.Length >= 10 ? stevne.Date[..10] : stevne.Date;
        return $"{Markup.Escape(date)}  {Markup.Escape(stevne.Name)} [grey](Id {stevne.Id})[/]";
    }

    private static string FormatOvelse(OvelseSummary ovelse) =>
        $"{Markup.Escape(ovelse.Name)} [grey](Id {ovelse.Id}, {ovelse.StarterCount} startere)[/]";

    private static string FormatClass(KmNmClassSummary item) =>
        $"{Markup.Escape(item.Name)} [grey]({item.StarterCount} startere, relays {Markup.Escape(item.Relays)})[/]";

    private static bool NeedsFile(string destination) => destination is "File" or "Both";

    private static bool NeedsClipboard(string destination) => destination is "Clipboard" or "Both";
}
