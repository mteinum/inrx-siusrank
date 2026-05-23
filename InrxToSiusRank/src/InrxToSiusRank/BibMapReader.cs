using System.Globalization;
using System.Text;

namespace InrxToSiusRank;

public static class BibMapReader
{
    public static IReadOnlyList<BibMapEntry> Read(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return [];
        }

        var lines = File.ReadAllLines(path, Encoding.UTF8);
        if (lines.Length == 0)
        {
            return [];
        }

        var header = SplitCsvLine(lines[0])
            .Select((name, index) => new { Name = name.Trim().TrimStart('\uFEFF'), Index = index })
            .ToDictionary(item => item.Name, item => item.Index, StringComparer.OrdinalIgnoreCase);

        var entries = new List<BibMapEntry>();
        foreach (var line in lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            var fields = SplitCsvLine(line);
            var bibNumber = GetField(fields, header, "bibNumber").Trim();
            if (string.IsNullOrWhiteSpace(bibNumber))
            {
                continue;
            }

            entries.Add(new BibMapEntry(
                GetField(fields, header, "nsfId").Trim(),
                bibNumber,
                int.TryParse(GetField(fields, header, "deltakerId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var deltakerId)
                    ? deltakerId
                    : 0,
                GetField(fields, header, "name").Trim(),
                GetField(fields, header, "source").Trim()));
        }

        return entries;
    }

    private static string GetField(
        IReadOnlyList<string> fields,
        IReadOnlyDictionary<string, int> header,
        string name)
    {
        return header.TryGetValue(name, out var index) && index >= 0 && index < fields.Count
            ? fields[index]
            : string.Empty;
    }

    private static IReadOnlyList<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var ch = line[index];
            if (inQuotes)
            {
                if (ch == '"' && index + 1 < line.Length && line[index + 1] == '"')
                {
                    builder.Append('"');
                    index++;
                }
                else if (ch == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    builder.Append(ch);
                }
            }
            else if (ch == ',')
            {
                fields.Add(builder.ToString());
                builder.Clear();
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else
            {
                builder.Append(ch);
            }
        }

        fields.Add(builder.ToString());
        return fields;
    }
}
