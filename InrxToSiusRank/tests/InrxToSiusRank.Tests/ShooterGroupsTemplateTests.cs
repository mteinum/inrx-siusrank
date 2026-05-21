namespace InrxToSiusRank.Tests;

using System.Xml.Linq;

public sealed class ShooterGroupsTemplateTests
{
    [Fact]
    public void Loads_shooter_group_names_and_indexes()
    {
        using var file = TempFile.Create(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Configuration ConfigurationType="ShooterGroupConfiguration">
              <ShooterGroupConfiguration>
                <Name>Apen</Name>
                <Index>106</Index>
                <ShortName>Apen</ShortName>
              </ShooterGroupConfiguration>
            </Configuration>
            """);

        var template = ShooterGroupsTemplate.Load(file.Path);

        Assert.True(template.TryGet("Apen", out var group));
        Assert.Equal(106, group.Index);
    }

    [Fact]
    public void Embedded_shooter_group_template_contains_standard_and_nm_25_50m_pistol_classes()
    {
        var template = ShooterGroupsTemplate.Load(Path.Combine(TemplatesDirectory(), "ShooterGroupsTemplate.xml"));

        var groupNames = template.Groups.Select(group => group.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(
            new[]
            {
                "Regulars", "MQS", "SH1", "SH2", "Guests", "Junior", "RPO", "JuniorRecord",
                "GP", "GPMQS", "Substitutes", "Youth", "YouthRecord", "Precision", "Sporter"
            }.All(groupNames.Contains));

        Assert.True(
            new[] { "Apen", "Menn", "Kvinner", "Jr", "Jr-NM", "Jrm", "Jrk", "U", "U-NM", "V55", "V65", "V73" }
                .All(groupNames.Contains));
    }

    [Fact]
    public void Embedded_shoot_events_have_default_groups_from_shooter_group_template()
    {
        var templatesDirectory = TemplatesDirectory();
        var shooterGroupsTemplate = ShooterGroupsTemplate.Load(Path.Combine(templatesDirectory, "ShooterGroupsTemplate.xml"));
        var document = XDocument.Load(Path.Combine(templatesDirectory, "ShootEventsTemplate2026_NM_Pistol.xml"));

        var events = document.Root!
            .Elements("ShootEventConfiguration")
            .Select(element => new
            {
                Name = element.Element("Name")!.Value,
                DefaultGroup = element.Element("DefaultShooterGroup")!.Value
            })
            .ToArray();

        Assert.Equal(11, events.Length);
        Assert.All(events, shootEvent => Assert.True(
            shooterGroupsTemplate.TryGet(shootEvent.DefaultGroup, out _),
            $"{shootEvent.Name} has unknown default group {shootEvent.DefaultGroup}."));
    }

    private static string TemplatesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "InrxToSiusRank", "Templates");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find src/InrxToSiusRank/Templates.");
    }

    private sealed class TempFile : IDisposable
    {
        private TempFile(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempFile Create(string contents)
        {
            var path = System.IO.Path.GetTempFileName();
            File.WriteAllText(path, contents);
            return new TempFile(path);
        }

        public void Dispose()
        {
            File.Delete(Path);
        }
    }
}
