using System;
using System.IO;
using System.Text.Json;

namespace InrxToSiusRank.Desktop;

public sealed record DesktopSettings
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public int Version { get; init; } = 2;

    public GlobalDesktopSettings Global { get; init; } = new();

    public DesktopSessionSettings Session { get; init; } = new();

    public static string SettingsPath => Path.Combine(GetSettingsDirectory(), "desktop-settings.json");

    public static DesktopSettings Empty { get; } = new();

    public static DesktopSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return Empty;
            }

            return FromJson(File.ReadAllText(SettingsPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return Empty;
        }
    }

    public static DesktopSettings FromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty(nameof(Version), out var version) &&
            version.ValueKind == JsonValueKind.Number &&
            version.GetInt32() >= 2 &&
            root.TryGetProperty(nameof(Global), out _) &&
            root.TryGetProperty(nameof(Session), out _))
        {
            return JsonSerializer.Deserialize<DesktopSettings>(json, SerializerOptions) ?? Empty;
        }

        return MigrateFlatSettings(root);
    }

    public string ToJson() =>
        JsonSerializer.Serialize(this, SerializerOptions);

    public void Save()
    {
        Directory.CreateDirectory(GetSettingsDirectory());
        File.WriteAllText(SettingsPath, ToJson());
    }

    private static DesktopSettings MigrateFlatSettings(JsonElement root)
    {
        var global = new GlobalDesktopSettings
        {
            EncodingName = ReadString(root, "GlobalEncodingName") ?? ReadString(root, "EncodingName"),
            SiusRankFolder = ReadString(root, "GlobalSiusRankFolder") ?? ReadString(root, "SiusRankFolder"),
            DefaultDatabasePath = ReadString(root, "DefaultDatabasePath")
        };
        var session = new DesktopSessionSettings
        {
            LastEventFilePath = ReadString(root, "EventFilePath"),
            StevneIds = ReadString(root, "StevneIds"),
            OvelseFilter = ReadString(root, "OvelseFilter"),
            EventFilter = ReadString(root, "EventFilter"),
            SscStartlag = ReadString(root, "SscStartlag"),
            SscLaneCount = ReadString(root, "SscLaneCount"),
            SscOrganizationName = ReadString(root, "SscOrganizationName"),
            SscOrganizationId = ReadString(root, "SscOrganizationId")
        };

        return new DesktopSettings
        {
            Version = 2,
            Global = global,
            Session = session
        };
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return property.GetString();
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

public sealed record GlobalDesktopSettings
{
    public string? EncodingName { get; init; }

    public string? SiusRankFolder { get; init; }

    public string? DefaultDatabasePath { get; init; }
}

public sealed record DesktopSessionSettings
{
    public string? LastEventFilePath { get; init; }

    public string? StevneIds { get; init; }

    public string? OvelseFilter { get; init; }

    public string? EventFilter { get; init; }

    public string? SscStartlag { get; init; }

    public string? SscLaneCount { get; init; }

    public string? SscOrganizationName { get; init; }

    public string? SscOrganizationId { get; init; }
}
