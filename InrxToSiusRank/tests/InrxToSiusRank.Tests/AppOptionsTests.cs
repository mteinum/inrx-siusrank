namespace InrxToSiusRank.Tests;

public sealed class AppOptionsTests
{
    [Fact]
    public void Output_is_not_required_when_clipboard_is_used()
    {
        using var db = TempDatabaseFile.Create();
        var options = AppOptions.Parse(
        [
            "--db", db.Path,
            "--stevne-id", "405",
            "--ovelse", "Fripistol",
            "--klasse", "Å",
            "--clipboard"
        ]);

        Assert.Null(options.OutputPath);
        Assert.True(options.CopyToClipboard);
    }

    [Fact]
    public void Output_and_clipboard_can_be_used_together()
    {
        using var db = TempDatabaseFile.Create();
        var options = AppOptions.Parse(
        [
            "--db", db.Path,
            "--stevne-id", "405",
            "--ovelse", "Fripistol",
            "--klasse", "Å",
            "--output", "out.csv",
            "--clipboard"
        ]);

        Assert.Equal("out.csv", options.OutputPath);
        Assert.True(options.CopyToClipboard);
    }

    [Fact]
    public void Either_output_or_clipboard_is_required()
    {
        using var db = TempDatabaseFile.Create();
        var ex = Assert.Throws<ArgumentException>(() => AppOptions.Parse(
        [
            "--db", db.Path,
            "--stevne-id", "405",
            "--ovelse", "Fripistol",
            "--klasse", "Å"
        ]));

        Assert.Contains("--output", ex.Message);
        Assert.Contains("--clipboard", ex.Message);
    }

    [Fact]
    public void Wizard_flag_only_requires_database()
    {
        using var db = TempDatabaseFile.Create();

        var options = AppOptions.Parse(["--wizard", "--db", db.Path]);

        Assert.True(options.Wizard);
        Assert.Equal(db.Path, options.DatabasePath);
        Assert.Null(options.StevneId);
    }

    [Fact]
    public void Wizard_positional_command_is_supported()
    {
        using var db = TempDatabaseFile.Create();

        var options = AppOptions.Parse(["wizard", "--db", db.Path]);

        Assert.True(options.Wizard);
        Assert.Equal(db.Path, options.DatabasePath);
    }

    [Fact]
    public void Shooter_groups_template_path_can_be_supplied()
    {
        using var db = TempDatabaseFile.Create();
        using var template = TempDatabaseFile.Create();

        var options = AppOptions.Parse(
        [
            "--db", db.Path,
            "--stevne-id", "405",
            "--ovelse", "Fripistol",
            "--klasse", "Å",
            "--output", "out.csv",
            "--shooter-groups-template", template.Path
        ]);

        Assert.Equal(template.Path, options.ShooterGroupsTemplatePath);
    }

    [Fact]
    public void Database_path_can_be_resolved_from_appsettings_inrx_path()
    {
        using var directory = TempDirectory.Create();
        var inrxDirectory = System.IO.Path.Combine(directory.Path, "inrx");
        Directory.CreateDirectory(inrxDirectory);
        var dbPath = System.IO.Path.Combine(inrxDirectory, "storage.db3");
        File.WriteAllText(dbPath, string.Empty);

        var settingsPath = WriteSettings(directory.Path, """
            {
              "Paths": {
                "Inrx": "inrx"
              }
            }
            """);

        var options = AppOptions.Parse(
        [
            "--settings", settingsPath,
            "--stevne-id", "405",
            "--ovelse", "Fripistol",
            "--klasse", "Å",
            "--output", "out.csv"
        ]);

        Assert.Equal(dbPath, options.DatabasePath);
    }

    [Fact]
    public void Command_line_database_path_overrides_appsettings()
    {
        using var directory = TempDirectory.Create();
        var inrxDirectory = System.IO.Path.Combine(directory.Path, "inrx");
        Directory.CreateDirectory(inrxDirectory);
        File.WriteAllText(System.IO.Path.Combine(inrxDirectory, "storage.db3"), string.Empty);
        using var explicitDb = TempDatabaseFile.Create();

        var settingsPath = WriteSettings(directory.Path, """
            {
              "Paths": {
                "Inrx": "inrx"
              }
            }
            """);

        var options = AppOptions.Parse(
        [
            "--settings", settingsPath,
            "--db", explicitDb.Path,
            "--stevne-id", "405",
            "--ovelse", "Fripistol",
            "--klasse", "Å",
            "--output", "out.csv"
        ]);

        Assert.Equal(explicitDb.Path, options.DatabasePath);
    }

    [Fact]
    public void Shooter_groups_template_path_can_be_resolved_from_appsettings_siusrank_path()
    {
        using var directory = TempDirectory.Create();
        var inrxDirectory = System.IO.Path.Combine(directory.Path, "inrx");
        Directory.CreateDirectory(inrxDirectory);
        File.WriteAllText(System.IO.Path.Combine(inrxDirectory, "storage.db3"), string.Empty);

        var templatesDirectory = System.IO.Path.Combine(directory.Path, "templates");
        Directory.CreateDirectory(templatesDirectory);
        var templatePath = System.IO.Path.Combine(templatesDirectory, "ShooterGroupsTemplate.xml");
        File.WriteAllText(templatePath, "<ShooterGroups />");

        var settingsPath = WriteSettings(directory.Path, """
            {
              "Paths": {
                "Inrx": "inrx",
                "SiusRankTemplates": "templates"
              }
            }
            """);

        var options = AppOptions.Parse(
        [
            "--settings", settingsPath,
            "--stevne-id", "405",
            "--ovelse", "Fripistol",
            "--klasse", "Å",
            "--output", "out.csv"
        ]);

        Assert.Equal(templatePath, options.ShooterGroupsTemplatePath);
    }

    [Fact]
    public void All_classes_accepts_stevne_id_range_and_output_directory()
    {
        using var db = TempDatabaseFile.Create();

        var options = AppOptions.Parse(
        [
            "--db", db.Path,
            "--stevne-ids", "405-407,409",
            "--all-classes",
            "--output-dir", "imports"
        ]);

        Assert.True(options.AllClasses);
        Assert.Equal(new[] { 405, 406, 407, 409 }, options.StevneIds);
        Assert.Equal("imports", options.OutputDirectory);
        Assert.Null(options.KmNmClass);
    }

    [Fact]
    public void All_classes_rejects_single_output()
    {
        using var db = TempDatabaseFile.Create();

        var ex = Assert.Throws<ArgumentException>(() => AppOptions.Parse(
        [
            "--db", db.Path,
            "--stevne-id", "405",
            "--all-classes",
            "--output", "out.csv"
        ]));

        Assert.Contains("--output-dir", ex.Message);
    }

    [Fact]
    public void All_classes_rejects_class_filter()
    {
        using var db = TempDatabaseFile.Create();

        var ex = Assert.Throws<ArgumentException>(() => AppOptions.Parse(
        [
            "--db", db.Path,
            "--stevne-id", "405",
            "--all-classes",
            "--klasse", "Å",
            "--output-dir", "imports"
        ]));

        Assert.Contains("--klasse", ex.Message);
    }

    private static string WriteSettings(string directoryPath, string content)
    {
        var settingsPath = System.IO.Path.Combine(directoryPath, "appsettings.json");
        File.WriteAllText(settingsPath, content);
        return settingsPath;
    }

    private sealed class TempDatabaseFile : IDisposable
    {
        private TempDatabaseFile(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDatabaseFile Create()
        {
            var path = System.IO.Path.GetTempFileName();
            return new TempDatabaseFile(path);
        }

        public void Dispose()
        {
            File.Delete(Path);
        }
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
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"inrx-siusrank-tests-{Guid.NewGuid():N}");
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
