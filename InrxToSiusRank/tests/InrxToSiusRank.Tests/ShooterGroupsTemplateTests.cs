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

        Assert.NotEmpty(events);
        Assert.All(events, shootEvent => Assert.True(
            shooterGroupsTemplate.TryGet(shootEvent.DefaultGroup, out _),
            $"{shootEvent.Name} has unknown default group {shootEvent.DefaultGroup}."));
    }

    [Fact]
    public void Embedded_shoot_events_have_unique_event_codes()
    {
        var document = XDocument.Load(Path.Combine(TemplatesDirectory(), "ShootEventsTemplate2026_NM_Pistol.xml"));
        var eventCodes = document.Root!
            .Elements("ShootEventConfiguration")
            .Select(element => element.Element("EventCode")!.Value)
            .ToArray();

        Assert.Equal(eventCodes.Length, eventCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("FP_V73", "50m Fripistol V73", "V73")]
    [InlineData("FP_P4X_SH1-P4", "P4 - Mixed 50m Pistol SH1", "SH1")]
    [InlineData("SPSH1_SH1-P3", "P3 - Mixed 25m Pistol SH1", "SH1")]
    [InlineData("RFP_Jr-NM", "25m Silhuettpistol Jr-NM", "Jr-NM")]
    [InlineData("RFP_NF_V55", "25m Silhuettpistol V55", "V55")]
    [InlineData("STP_M", "25m Standardpistol M", "Menn")]
    [InlineData("SPRF_K", "25m Hurtigpistol Fin K", "Kvinner")]
    public void Embedded_shoot_events_include_class_specific_nm_import_events(
        string eventCode,
        string expectedName,
        string expectedDefaultGroup)
    {
        var document = XDocument.Load(Path.Combine(TemplatesDirectory(), "ShootEventsTemplate2026_NM_Pistol.xml"));
        var shootEvent = document.Root!
            .Elements("ShootEventConfiguration")
            .Single(element => element.Element("EventCode")!.Value == eventCode);

        Assert.Equal(expectedName, shootEvent.Element("Name")!.Value);
        Assert.Equal(expectedDefaultGroup, shootEvent.Element("DefaultShooterGroup")!.Value);
    }

    [Fact]
    public void Embedded_shoot_events_keep_imported_bib_numbers()
    {
        var document = XDocument.Load(Path.Combine(TemplatesDirectory(), "ShootEventsTemplate2026_NM_Pistol.xml"));
        var phases = document.Root!
            .Elements("ShootEventConfiguration")
            .SelectMany(shootEvent => shootEvent
                .Element("PhaseConfigurations")!
                .Elements("PhaseConfiguration")
                .Select(phase => new
                {
                    EventName = shootEvent.Element("Name")!.Value,
                    PhaseName = phase.Element("PhaseName")!.Value,
                    BibNumberAssignment = phase.Element("BibNumberAssignment")?.Value
                }))
            .ToArray();

        Assert.NotEmpty(phases);
        Assert.All(phases, phase => Assert.Equal(
            "KeepTheSame",
            phase.BibNumberAssignment));
    }

    [Fact]
    public void Embedded_shoot_events_give_silhouette_junior_a_final()
    {
        var document = XDocument.Load(Path.Combine(TemplatesDirectory(), "ShootEventsTemplate2026_NM_Pistol.xml"));
        var shootEvent = document.Root!
            .Elements("ShootEventConfiguration")
            .Single(element => element.Element("EventCode")!.Value == "RFP_Jr-NM");
        var phases = shootEvent
            .Element("PhaseConfigurations")!
            .Elements("PhaseConfiguration")
            .Select(phase => phase.Element("PhaseName")!.Value)
            .ToArray();

        Assert.Contains("Final", phases);
        Assert.Equal("8", shootEvent.Element("NumberOfFinalists")!.Value);
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
