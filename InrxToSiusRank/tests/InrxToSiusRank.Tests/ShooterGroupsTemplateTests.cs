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
    public void Embedded_shooter_group_template_contains_standard_nm_and_approbert_25_50m_pistol_classes()
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
            new[] { "Apen", "Menn", "Kvinner", "Jr", "JrNM", "Jrm", "Jrk", "U", "UNM", "V55", "V65", "V73" }
                .All(groupNames.Contains));

        Assert.True(
            new[] { "A", "B", "C", "D", "U16", "U14", "U12", "ÅR", "SH Å" }
                .All(groupNames.Contains));
    }

    [Fact]
    public void Embedded_shoot_events_have_default_groups_from_shooter_group_template()
    {
        var templatesDirectory = TemplatesDirectory();
        var shooterGroupsTemplate = ShooterGroupsTemplate.Load(Path.Combine(templatesDirectory, "ShooterGroupsTemplate.xml"));
        foreach (var templatePath in ShootEventTemplatePaths())
        {
            var document = XDocument.Load(templatePath);
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
                $"{Path.GetFileName(templatePath)}: {shootEvent.Name} has unknown default group {shootEvent.DefaultGroup}."));
        }
    }

    [Fact]
    public void Embedded_shoot_events_have_unique_event_codes()
    {
        foreach (var templatePath in ShootEventTemplatePaths())
        {
            var document = XDocument.Load(templatePath);
            var eventCodes = document.Root!
                .Elements("ShootEventConfiguration")
                .Select(element => element.Element("EventCode")!.Value)
                .ToArray();

            Assert.Equal(eventCodes.Length, eventCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    [Theory]
    [InlineData("Fri_V73", "50m Fripistol V73", "V73")]
    [InlineData("Fri_SH1-P4", "P4 - Mixed 50m Pistol SH1", "SH1")]
    [InlineData("Fin_SH1-P3", "P3 - Mixed 25m Pistol SH1", "SH1")]
    [InlineData("Silhuett_Jr-NM", "25m Silhuettpistol Jr-NM", "JrNM")]
    [InlineData("Silhuett_V55", "25m Silhuettpistol V55", "V55")]
    [InlineData("Standard_M", "25m Standardpistol M", "Menn")]
    [InlineData("HurtigFin_K", "25m Hurtigpistol Fin K", "Kvinner")]
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

    [Theory]
    [InlineData("2A_A", "2A_A", "50m Fripistol A", "A")]
    [InlineData("2C_V73", "2C_V73", "50m pistol, skyting med støtte, vet. V73", "V73")]
    [InlineData("2B_U14", "2B_U14", "25m Fripistol, Coltskive U14", "U14")]
    [InlineData("4_U16", "4_U16", "25m Silhuettpistol U16", "U16")]
    [InlineData("5_SH1", "5_SH1", "25m Standardpistol SH1", "SH1")]
    [InlineData("6F_SH-Apen", "6F_SH-Apen", "25m Finpistol SH Å", "SH Å")]
    [InlineData("6G_D", "6G_D", "25m Grovpistol D", "D")]
    [InlineData("7F_SH1", "7F_SH1", "25m Hurtigpistol Fin SH1", "SH1")]
    [InlineData("7G_A", "7G_A", "25m Hurtigpistol Grov A", "A")]
    public void Embedded_shoot_events_include_approbert_25_50m_pistol_events(
        string eventCode,
        string expectedShortEventCode,
        string expectedName,
        string expectedDefaultGroup)
    {
        var document = XDocument.Load(Path.Combine(TemplatesDirectory(), "ShootEventsTemplate2026_Approberte_Pistol.xml"));
        var shootEvent = document.Root!
            .Elements("ShootEventConfiguration")
            .Single(element => element.Element("EventCode")!.Value == eventCode);

        Assert.Equal(expectedShortEventCode, shootEvent.Element("ShortEventCode")!.Value);
        Assert.Equal(expectedName, shootEvent.Element("Name")!.Value);
        Assert.Equal(expectedDefaultGroup, shootEvent.Element("DefaultShooterGroup")!.Value);
    }

    [Fact]
    public void Embedded_approbert_shoot_events_exclude_nais_fin_and_grov()
    {
        var document = XDocument.Load(Path.Combine(TemplatesDirectory(), "ShootEventsTemplate2026_Approberte_Pistol.xml"));
        var eventNames = document.Root!
            .Elements("ShootEventConfiguration")
            .Select(element => element.Element("Name")!.Value)
            .ToArray();

        Assert.DoesNotContain(eventNames, name => name.Contains("NAIS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Embedded_50m_support_pistol_uses_30_shots()
    {
        var document = XDocument.Load(Path.Combine(TemplatesDirectory(), "ShootEventsTemplate2026_Approberte_Pistol.xml"));
        var shootEvent = document.Root!
            .Elements("ShootEventConfiguration")
            .Single(element => element.Element("EventCode")!.Value == "2C_V55");
        var phase = shootEvent
            .Element("PhaseConfigurations")!
            .Elements("PhaseConfiguration")
            .Single(phase => phase.Element("PhaseName")!.Value == "Individual");

        Assert.Equal("3", phase.Element("NumberOfEnabledShotGroups")!.Value);
        Assert.Equal(3, phase.Element("ShotGroupConfigurations")!.Elements("ShotGroupConfiguration").Count());
    }

    [Fact]
    public void Embedded_shoot_events_keep_imported_bib_numbers()
    {
        foreach (var templatePath in ShootEventTemplatePaths())
        {
            var document = XDocument.Load(templatePath);
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
    }

    [Fact]
    public void Embedded_shoot_events_give_silhouette_junior_a_final()
    {
        var document = XDocument.Load(Path.Combine(TemplatesDirectory(), "ShootEventsTemplate2026_NM_Pistol.xml"));
        var shootEvent = document.Root!
            .Elements("ShootEventConfiguration")
            .Single(element => element.Element("EventCode")!.Value == "Silhuett_Jr-NM");
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
            var candidate = Path.Combine(directory.FullName, "Templates");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Templates directory.");
    }

    private static string[] ShootEventTemplatePaths() =>
        Directory.GetFiles(TemplatesDirectory(), "ShootEventsTemplate*.xml");

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
