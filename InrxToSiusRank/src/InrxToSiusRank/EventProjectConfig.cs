using System.Text.Json;

namespace InrxToSiusRank;

public sealed record EventProjectConfig
{
    public int Version { get; init; } = 1;

    public EventExerciseConfig Exercise { get; init; } = new();

    public EventInrxConfig Inrx { get; init; } = new();

    public Dictionary<string, string> EventTypes { get; init; } = new(StringComparer.Ordinal);

    public EventSilhouetteConfig Silhouette { get; init; } = new();

    public string SiusRankFolder { get; init; } = @"C:\SIUS\SiusRank";

    public EventCsvConfig Csv { get; init; } = new();

    public List<EventClassConfig> Classes { get; init; } = [];
}

public sealed record EventExerciseConfig
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ShortName { get; init; } = string.Empty;

    public int HovedOvelseId { get; init; }
}

public sealed record EventInrxConfig
{
    public string Db { get; init; } = string.Empty;

    public string Stevner { get; init; } = string.Empty;
}

public sealed record EventSilhouetteConfig
{
    public int ShootersPerStand { get; init; } = 2;
}

public sealed record EventCsvConfig
{
    public string Output { get; init; } = "./inrX_export";
}

public sealed record EventClassConfig
{
    public string Class { get; init; } = string.Empty;

    public string Folder { get; init; } = string.Empty;

    public string Exports { get; init; } = string.Empty;
}

public static class EventProjectFile
{
    public const string FileName = "event.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static EventProjectConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new ArgumentException($"event.json file does not exist: {path}");
        }

        var config = JsonSerializer.Deserialize<EventProjectConfig>(
            File.ReadAllText(path),
            SerializerOptions) ?? throw new ArgumentException($"event.json is empty or invalid: {path}");

        Validate(config, path);
        return config;
    }

    public static void Save(string path, EventProjectConfig config)
    {
        Validate(config, path);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(config, SerializerOptions));
    }

    public static string ResolvePath(string eventJsonPath, string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        if (Path.IsPathRooted(configuredPath) || IsWindowsRootedPath(configuredPath))
        {
            return configuredPath;
        }

        var baseDirectory = GetEventDirectory(eventJsonPath);
        return IsWindowsRootedPath(baseDirectory)
            ? CombineWindowsPath(baseDirectory, configuredPath)
            : Path.GetFullPath(Path.Combine(baseDirectory, configuredPath));
    }

    public static string ToStoredPath(string eventJsonPath, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        if (!Path.IsPathRooted(path) && !IsWindowsRootedPath(path))
        {
            return path;
        }

        var baseDirectory = GetEventDirectory(eventJsonPath);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return IsWindowsRootedPath(path) ? path : Path.GetFullPath(path);
        }

        if (!IsInsideDirectory(baseDirectory, path))
        {
            return IsWindowsRootedPath(path) ? path : Path.GetFullPath(path);
        }

        var relative = GetRelativePath(baseDirectory, path);
        return relative.StartsWith(".", StringComparison.Ordinal) ? relative : "./" + relative;
    }

    public static string GetEventDirectory(string eventJsonPath)
    {
        if (IsWindowsRootedPath(eventJsonPath))
        {
            var normalized = NormalizeWindowsPath(eventJsonPath);
            var index = normalized.LastIndexOf('/');
            return index <= 2 ? normalized : normalized[..index];
        }

        return Path.GetDirectoryName(Path.GetFullPath(eventJsonPath)) ?? Directory.GetCurrentDirectory();
    }

    public static bool IsInsideDirectory(string baseDirectory, string path)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory) || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (IsWindowsRootedPath(baseDirectory) || IsWindowsRootedPath(path))
        {
            if (!IsWindowsRootedPath(baseDirectory) || !IsWindowsRootedPath(path))
            {
                return false;
            }

            var basePath = TrimTrailingSeparators(NormalizeWindowsPath(baseDirectory));
            var fullPath = TrimTrailingSeparators(NormalizeWindowsPath(path));
            return fullPath.Equals(basePath, StringComparison.OrdinalIgnoreCase) ||
                   fullPath.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase);
        }

        var fullBase = TrimTrailingSeparators(Path.GetFullPath(baseDirectory).Replace('\\', '/'));
        var fullPathUnix = TrimTrailingSeparators(Path.GetFullPath(path).Replace('\\', '/'));
        return fullPathUnix.Equals(fullBase, StringComparison.Ordinal) ||
               fullPathUnix.StartsWith(fullBase + "/", StringComparison.Ordinal);
    }

    private static string GetRelativePath(string baseDirectory, string path)
    {
        if (IsWindowsRootedPath(baseDirectory) || IsWindowsRootedPath(path))
        {
            var basePath = TrimTrailingSeparators(NormalizeWindowsPath(baseDirectory));
            var fullPath = TrimTrailingSeparators(NormalizeWindowsPath(path));
            var relative = fullPath.Length == basePath.Length
                ? "."
                : fullPath[(basePath.Length + 1)..];
            return relative.Replace('\\', '/');
        }

        return Path.GetRelativePath(baseDirectory, Path.GetFullPath(path))
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string NormalizeWindowsPath(string path) =>
        TrimTrailingSeparators(path.Replace('\\', '/'));

    private static string CombineWindowsPath(string baseDirectory, string relativePath)
    {
        var combined = NormalizeWindowsPath(baseDirectory) + "/" + relativePath.Replace('\\', '/');
        return combined.Replace('/', '\\');
    }

    private static string TrimTrailingSeparators(string path)
    {
        var minimumLength = IsWindowsRootedPath(path) ? 3 : 1;
        while (path.Length > minimumLength && (path[^1] == '/' || path[^1] == '\\'))
        {
            path = path[..^1];
        }

        return path;
    }

    public static bool IsWindowsRootedPath(string path) =>
        path.Length >= 3 &&
        char.IsLetter(path[0]) &&
        path[1] == ':' &&
        (path[2] == '\\' || path[2] == '/');

    private static void Validate(EventProjectConfig config, string path)
    {
        if (config.Version <= 0)
        {
            throw new ArgumentException($"event.json has invalid version in {path}.");
        }

        if (config.Exercise.Id <= 0)
        {
            throw new ArgumentException($"event.json must contain exercise.id in {path}.");
        }

        if (string.IsNullOrWhiteSpace(config.Inrx.Db))
        {
            throw new ArgumentException($"event.json must contain inrx.db in {path}.");
        }

        if (string.IsNullOrWhiteSpace(config.Inrx.Stevner))
        {
            throw new ArgumentException($"event.json must contain inrx.stevner in {path}.");
        }

        if (config.Silhouette.ShootersPerStand is not (1 or 2))
        {
            throw new ArgumentException("event.json silhouette.shootersPerStand must be 1 or 2.");
        }
    }
}

public static class EventProjectPlanner
{
    public const string ChampionshipEventType = "mesterskap";
    public const string ApprovedEventType = "approbert";

    public static EventProjectConfig Build(
        InrxRepository repository,
        string databasePath,
        IReadOnlyList<int> stevneIds,
        OvelseInfo ovelse,
        int silhouetteShootersPerStand,
        string siusRankFolder = @"C:\SIUS\SiusRank")
    {
        if (stevneIds.Count == 0)
        {
            throw new ArgumentException("At least one Stevne.Id is required.");
        }

        var eventTypes = stevneIds
            .Distinct()
            .ToDictionary(
                id => id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                repository.GetStevneEventType,
                StringComparer.Ordinal);

        return new EventProjectConfig
        {
            Version = 1,
            Exercise = new EventExerciseConfig
            {
                Id = ovelse.Id,
                Name = ovelse.Name,
                ShortName = ovelse.ShortName,
                HovedOvelseId = ovelse.HovedOvelseId
            },
            Inrx = new EventInrxConfig
            {
                Db = databasePath,
                Stevner = FormatIds(stevneIds)
            },
            EventTypes = eventTypes,
            Silhouette = new EventSilhouetteConfig
            {
                ShootersPerStand = silhouetteShootersPerStand
            },
            SiusRankFolder = siusRankFolder,
            Csv = new EventCsvConfig
            {
                Output = "./inrX_export"
            },
            Classes = BuildClassConfigs(ovelse, ResolveEffectiveClasses(repository, stevneIds, ovelse)).ToList()
        };
    }

    public static IReadOnlyList<EventClassConfig> BuildClassConfigs(
        OvelseInfo ovelse,
        IEnumerable<string> classes)
    {
        var exercisePart = SanitizePathPart(ovelse.Name);
        return classes
            .Where(className => !string.IsNullOrWhiteSpace(className))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(EffectiveKmNmClass.SortKey)
            .ThenBy(className => className, StringComparer.OrdinalIgnoreCase)
            .Select(className =>
            {
                var classPart = SanitizePathPart(className);
                var folder = $"./SiusRank_{exercisePart}_{classPart}";
                return new EventClassConfig
                {
                    Class = className,
                    Folder = folder,
                    Exports = $"{folder}/Exports"
                };
            })
            .ToList();
    }

    public static string ResolveEventType(int mestDm, int mestKm, int mestNm) =>
        mestDm != 0 || mestKm != 0 || mestNm != 0
            ? ChampionshipEventType
            : ApprovedEventType;

    public static string FormatIds(IReadOnlyList<int> ids) =>
        string.Join(",", ids.Distinct().OrderBy(id => id));

    public static string SanitizePathPart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value
            .Trim()
            .Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '_' : ch)
            .ToArray();
        var result = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(result) ? "event" : result;
    }

    private static IReadOnlyList<string> ResolveEffectiveClasses(
        InrxRepository repository,
        IReadOnlyList<int> stevneIds,
        OvelseInfo ovelse)
    {
        var classes = new List<string>();
        foreach (var stevneId in stevneIds.Distinct())
        {
            classes.AddRange(repository
                .GetStarters(stevneId, ovelse.Id)
                .Select(starter => EffectiveKmNmClass.Resolve(starter, ovelse)));
        }

        if (classes.Count == 0)
        {
            throw new InvalidOperationException(
                $"No starters found for selected stevner and OvelseDef.Id={ovelse.Id}.");
        }

        return classes;
    }
}
