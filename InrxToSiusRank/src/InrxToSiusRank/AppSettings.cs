using System.Text.Json;

namespace InrxToSiusRank;

public sealed record AppSettings(
    string InrxPath,
    string SiusRankTemplatesPath,
    string? DatabasePath,
    string? ShooterGroupsTemplatePath,
    string? SourcePath)
{
    public const string DefaultInrxPath = @"C:\Program Files (x86)\inrX";
    public const string DefaultSiusRankTemplatesPath = @"C:\SIUS\SiusRank\Resources\Templates";

    public static AppSettings Load(string? explicitPath = null)
    {
        var settings = new AppSettings(
            DefaultInrxPath,
            DefaultSiusRankTemplatesPath,
            DatabasePath: null,
            ShooterGroupsTemplatePath: null,
            SourcePath: null);

        var defaultPath = FindDefaultSettingsPath();
        if (defaultPath is not null)
        {
            settings = settings.Merge(Read(defaultPath));
        }

        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            if (!File.Exists(explicitPath))
            {
                throw new ArgumentException($"Settings file does not exist: {explicitPath}");
            }

            settings = settings.Merge(Read(explicitPath));
        }

        return settings;
    }

    public string ResolveDatabasePath() =>
        string.IsNullOrWhiteSpace(DatabasePath)
            ? CombinePath(InrxPath, "storage.db3")
            : DatabasePath;

    public string ResolveShooterGroupsTemplatePath() =>
        string.IsNullOrWhiteSpace(ShooterGroupsTemplatePath)
            ? CombinePath(SiusRankTemplatesPath, "ShooterGroupsTemplate.xml")
            : ShooterGroupsTemplatePath;

    private AppSettings Merge(AppSettings other) =>
        this with
        {
            InrxPath = string.IsNullOrWhiteSpace(other.InrxPath) ? InrxPath : other.InrxPath,
            SiusRankTemplatesPath = string.IsNullOrWhiteSpace(other.SiusRankTemplatesPath)
                ? SiusRankTemplatesPath
                : other.SiusRankTemplatesPath,
            DatabasePath = string.IsNullOrWhiteSpace(other.DatabasePath) ? DatabasePath : other.DatabasePath,
            ShooterGroupsTemplatePath = string.IsNullOrWhiteSpace(other.ShooterGroupsTemplatePath)
                ? ShooterGroupsTemplatePath
                : other.ShooterGroupsTemplatePath,
            SourcePath = other.SourcePath ?? SourcePath
        };

    private static AppSettings Read(string path)
    {
        using var document = ParseJson(path);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException($"Settings file root must be a JSON object: {path}");
        }

        var paths = TryGetProperty(root, "Paths", out var pathsElement)
            ? pathsElement
            : root;
        if (paths.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException($"Settings Paths value must be a JSON object: {path}");
        }

        var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? Directory.GetCurrentDirectory();
        var inrxPath = ResolveConfiguredPath(
            ReadString(paths, "Inrx", "InrxPath", "InrxDirectory"),
            baseDirectory);
        var siusRankTemplatesPath = ResolveConfiguredPath(
            ReadString(paths, "SiusRankTemplates", "SiusRankTemplatesPath", "SiusRankTemplatesDirectory"),
            baseDirectory);
        var databasePath = ResolveConfiguredPath(
            ReadString(paths, "Database", "DatabasePath", "InrxDatabase", "StorageDatabase"),
            baseDirectory);
        var shooterGroupsTemplatePath = ResolveConfiguredPath(
            ReadString(paths, "ShooterGroupsTemplate", "ShooterGroupsTemplatePath"),
            baseDirectory);

        return new AppSettings(
            inrxPath ?? string.Empty,
            siusRankTemplatesPath ?? string.Empty,
            databasePath,
            shooterGroupsTemplatePath,
            Path.GetFullPath(path));
    }

    private static JsonDocument ParseJson(string path)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Settings file is not valid JSON: {path}. {ex.Message}", ex);
        }
    }

    private static string? FindDefaultSettingsPath()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
            Path.Combine(AppContext.BaseDirectory, "appsettings.json")
        };

        return candidates
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(File.Exists);
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var property) && property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement property)
    {
        foreach (var item in element.EnumerateObject())
        {
            if (item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                property = item.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    private static string? ResolveConfiguredPath(string? path, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (Path.IsPathRooted(path) || IsWindowsRootedPath(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    private static string CombinePath(string basePath, string fileName)
    {
        if (basePath.EndsWith('\\') || basePath.EndsWith('/'))
        {
            return basePath + fileName;
        }

        var separator = IsWindowsRootedPath(basePath) ? '\\' : Path.DirectorySeparatorChar;
        return basePath + separator + fileName;
    }

    private static bool IsWindowsRootedPath(string path) =>
        path.Length >= 3 &&
        char.IsLetter(path[0]) &&
        path[1] == ':' &&
        (path[2] == '\\' || path[2] == '/');
}
