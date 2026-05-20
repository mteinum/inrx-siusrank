using Microsoft.Extensions.Configuration;

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
        var fullPath = Path.GetFullPath(path);
        var baseDirectory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        var fileName = Path.GetFileName(fullPath);
        var configuration = BuildConfiguration(baseDirectory, fileName);
        var inrxPath = ResolveConfiguredPath(
            ReadString(
                configuration,
                "Paths:Inrx",
                "Paths:InrxPath",
                "Paths:InrxDirectory",
                "Inrx",
                "InrxPath",
                "InrxDirectory"),
            baseDirectory);
        var siusRankTemplatesPath = ResolveConfiguredPath(
            ReadString(
                configuration,
                "Paths:SiusRankTemplates",
                "Paths:SiusRankTemplatesPath",
                "Paths:SiusRankTemplatesDirectory",
                "SiusRankTemplates",
                "SiusRankTemplatesPath",
                "SiusRankTemplatesDirectory"),
            baseDirectory);
        var databasePath = ResolveConfiguredPath(
            ReadString(
                configuration,
                "Paths:Database",
                "Paths:DatabasePath",
                "Paths:InrxDatabase",
                "Paths:StorageDatabase",
                "Database",
                "DatabasePath",
                "InrxDatabase",
                "StorageDatabase"),
            baseDirectory);
        var shooterGroupsTemplatePath = ResolveConfiguredPath(
            ReadString(
                configuration,
                "Paths:ShooterGroupsTemplate",
                "Paths:ShooterGroupsTemplatePath",
                "ShooterGroupsTemplate",
                "ShooterGroupsTemplatePath"),
            baseDirectory);

        return new AppSettings(
            inrxPath ?? string.Empty,
            siusRankTemplatesPath ?? string.Empty,
            databasePath,
            shooterGroupsTemplatePath,
            fullPath);
    }

    private static IConfiguration BuildConfiguration(string baseDirectory, string fileName)
    {
        try
        {
            return new ConfigurationBuilder()
                .SetBasePath(baseDirectory)
                .AddJsonFile(fileName, optional: false, reloadOnChange: false)
                .Build();
        }
        catch (InvalidDataException ex)
        {
            throw new ArgumentException(
                $"Settings file is not valid JSON: {Path.Combine(baseDirectory, fileName)}. {ex.Message}",
                ex);
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

    private static string? ReadString(IConfiguration configuration, params string[] names)
    {
        foreach (var name in names)
        {
            var value = configuration[name];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
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
