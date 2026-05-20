using System.Globalization;
using System.Xml.Linq;

namespace InrxToSiusRank;

public sealed record ShooterGroupDefinition(string Name, int Index, string ShortName);

public sealed class ShooterGroupsTemplate
{
    private readonly Dictionary<string, ShooterGroupDefinition> _byName;

    private ShooterGroupsTemplate(string path, IReadOnlyList<ShooterGroupDefinition> groups)
    {
        Path = path;
        Groups = groups;
        _byName = groups
            .GroupBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public string Path { get; }

    public IReadOnlyList<ShooterGroupDefinition> Groups { get; }

    public static ShooterGroupsTemplate Load(string path)
    {
        var document = XDocument.Load(path);
        if (!string.Equals(
                document.Root?.Attribute("ConfigurationType")?.Value,
                "ShooterGroupConfiguration",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"'{path}' is not a SIUS Rank ShooterGroupConfiguration template.");
        }

        var groups = document
            .Descendants("ShooterGroupConfiguration")
            .Select(ReadGroup)
            .Where(group => !string.IsNullOrWhiteSpace(group.Name))
            .ToList();

        if (groups.Count == 0)
        {
            throw new InvalidOperationException($"No shooter groups found in '{path}'.");
        }

        return new ShooterGroupsTemplate(path, groups);
    }

    public bool TryGet(string name, out ShooterGroupDefinition group) =>
        _byName.TryGetValue(name, out group!);

    private static ShooterGroupDefinition ReadGroup(XElement element)
    {
        var name = element.Element("Name")?.Value.Trim() ?? string.Empty;
        var shortName = element.Elements("ShortName")
            .Select(item => item.Value.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        var indexValue = element.Element("Index")?.Value.Trim() ?? "0";
        var index = int.TryParse(indexValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIndex)
            ? parsedIndex
            : 0;

        return new ShooterGroupDefinition(name, index, shortName);
    }
}
