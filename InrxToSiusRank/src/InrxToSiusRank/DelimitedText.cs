using System.Text;

namespace InrxToSiusRank;

public static class DelimitedText
{
    public static string Escape(string value, char delimiter)
    {
        if (!value.Contains(delimiter) &&
            !value.Contains('"') &&
            !value.Contains('\r') &&
            !value.Contains('\n'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    public static IReadOnlyList<string> SplitLine(string line, char delimiter)
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
            else if (ch == delimiter)
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

    public static IReadOnlyList<IReadOnlyList<string>> ReadRecords(string text, char delimiter)
    {
        var records = new List<IReadOnlyList<string>>();
        var fields = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < text.Length; index++)
        {
            var ch = text[index];
            if (inQuotes)
            {
                if (ch == '"' && index + 1 < text.Length && text[index + 1] == '"')
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

                continue;
            }

            if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == delimiter)
            {
                fields.Add(builder.ToString());
                builder.Clear();
            }
            else if (ch == '\r' || ch == '\n')
            {
                if (ch == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                fields.Add(builder.ToString());
                builder.Clear();
                if (fields.Count > 1 || !string.IsNullOrWhiteSpace(fields[0]))
                {
                    records.Add(fields.ToList());
                }

                fields.Clear();
            }
            else
            {
                builder.Append(ch);
            }
        }

        if (builder.Length > 0 || fields.Count > 0)
        {
            fields.Add(builder.ToString());
            if (fields.Count > 1 || !string.IsNullOrWhiteSpace(fields[0]))
            {
                records.Add(fields.ToList());
            }
        }

        return records;
    }
}
