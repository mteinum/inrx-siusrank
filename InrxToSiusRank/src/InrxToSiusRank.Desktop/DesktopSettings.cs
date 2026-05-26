using System;
using System.IO;
using System.Text.Json;

namespace InrxToSiusRank.Desktop;

internal sealed record DesktopSettings(
    string? DatabasePath,
    string? OutputDirectory,
    string? ShooterGroupsTemplatePath,
    string? ExportsDirectory,
    string? BibMapPath,
    string? SscBibMapPath,
    string? SscOutputDirectory,
    string? SscUsersCsvPath,
    string? SscStartlag,
    string? SscLaneCount,
    string? SscOrganizationName,
    string? SscOrganizationId,
    string? StevneIds,
    string? EncodingName,
    string? OvelseFilter,
    string? EventFilter)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    public static string SettingsPath => Path.Combine(GetSettingsDirectory(), "desktop-settings.json");

    public static DesktopSettings Empty { get; } = new(
        DatabasePath: null,
        OutputDirectory: null,
        ShooterGroupsTemplatePath: null,
        ExportsDirectory: null,
        BibMapPath: null,
        SscBibMapPath: null,
        SscOutputDirectory: null,
        SscUsersCsvPath: null,
        SscStartlag: null,
        SscLaneCount: null,
        SscOrganizationName: null,
        SscOrganizationId: null,
        StevneIds: null,
        EncodingName: null,
        OvelseFilter: null,
        EventFilter: null);

    public static DesktopSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return Empty;
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<DesktopSettings>(json, SerializerOptions) ?? Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return Empty;
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(GetSettingsDirectory());
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, SerializerOptions));
    }

    private static string GetSettingsDirectory()
    {
        var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Path.Combine(baseDirectory, "InrxToSiusRank");
    }
}
