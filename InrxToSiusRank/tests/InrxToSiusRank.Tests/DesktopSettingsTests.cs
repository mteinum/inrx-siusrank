using System.Text.Json;
using InrxToSiusRank.Desktop;

namespace InrxToSiusRank.Tests;

public sealed class DesktopSettingsTests
{
    [Fact]
    public void Old_flat_desktop_settings_migrate_to_grouped_version_2()
    {
        const string json =
            """
            {
              "GlobalEncodingName": "utf8-bom",
              "EncodingName": "windows-1252",
              "GlobalSiusRankFolder": "C:\\SIUS\\SiusRank",
              "SiusRankFolder": "/event/SiusRank",
              "DefaultDatabasePath": "/default/storage.db3",
              "EventFilePath": "/event/event.json",
              "DatabasePath": "/event/storage.db3",
              "OutputDirectory": "/event/siusrank-import",
              "ExportsDirectory": "/event/SiusRank_Finpistol_B/Exports",
              "BibMapPath": "/event/siusrank-import/bib-map.csv",
              "SscBibMapPath": "/event/siusrank-import/bib-map.csv",
              "SscOutputDirectory": "/repo/ssc-setup",
              "SscUsersCsvPath": "/repo/ssc-setup/ssc-users.csv",
              "ShooterGroupsTemplatePath": "/event/ShooterGroupsTemplate.xml",
              "StevneIds": "413-417",
              "OvelseFilter": "9",
              "EventFilter": null,
              "SscStartlag": "2026-07-06T09:00:00",
              "SscLaneCount": "40",
              "SscOrganizationName": "Legacy",
              "SscOrganizationId": "f95a2bc3-79bd-4c24-98b6-4e17f99bbfaf"
            }
            """;

        var settings = DesktopSettings.FromJson(json);

        Assert.Equal(2, settings.Version);
        Assert.Equal("utf8-bom", settings.Global.EncodingName);
        Assert.Equal(@"C:\SIUS\SiusRank", settings.Global.SiusRankFolder);
        Assert.Equal("/default/storage.db3", settings.Global.DefaultDatabasePath);
        Assert.Equal("/event/event.json", settings.Recent.LastEventFilePath);
    }

    [Fact]
    public void Saved_desktop_settings_write_only_grouped_shape()
    {
        var settings = new DesktopSettings
        {
            Global = new GlobalDesktopSettings
            {
                EncodingName = CsvEncoding.Utf8Bom,
                SiusRankFolder = @"C:\SIUS\SiusRank",
                DefaultDatabasePath = "/default/storage.db3"
            },
            Recent = new RecentDesktopSettings
            {
                LastEventFilePath = "/event/event.json"
            }
        };

        using var document = JsonDocument.Parse(settings.ToJson());
        var root = document.RootElement;

        Assert.Equal(2, root.GetProperty("Version").GetInt32());
        Assert.True(root.TryGetProperty("Global", out _));
        Assert.True(root.TryGetProperty("Recent", out _));
        Assert.False(root.TryGetProperty("Session", out _));

        foreach (var oldProperty in new[]
        {
            "StevneIds",
            "OvelseFilter",
            "EventFilter",
            "DatabasePath",
            "OutputDirectory",
            "ExportsDirectory",
            "BibMapPath",
            "SscBibMapPath",
            "SscOutputDirectory",
            "SscUsersCsvPath",
            "ShooterGroupsTemplatePath",
            "SiusRankFolder",
            "EncodingName"
        })
        {
            Assert.False(root.TryGetProperty(oldProperty, out _), oldProperty);
        }
    }

    [Fact]
    public void Old_project_path_fields_are_not_migrated()
    {
        const string json =
            """
            {
              "DatabasePath": "/event/storage.db3",
              "OutputDirectory": "/event/siusrank-import",
              "SscOutputDirectory": "/repo/ssc-setup",
              "SscUsersCsvPath": "/repo/ssc-setup/ssc-users.csv"
            }
            """;

        var settings = DesktopSettings.FromJson(json);

        Assert.Null(settings.Global.DefaultDatabasePath);
        Assert.Null(settings.Recent.LastEventFilePath);
    }
}
