using System.Globalization;
using System.Text;

namespace InrxToSiusRank;

public static class SiusRankCsvWriter
{
    private static readonly string[] SilhouetteImportHeader =
    [
        "ImportShotFilter",
        "SiusDataStartNumber"
    ];

    public static readonly string[] Header =
    [
        "StartNumber",
        "AccreditationNumber",
        "IssfId",
        "DisplayNameLong",
        "DisplayName",
        "FirstName",
        "Name",
        "BirthDay",
        "Gender",
        "Nation",
        "BibNumber",
        "TargetNumber",
        "Relay",
        "TeamIndex",
        "DuellIndex",
        "Groups",
        "Comment",
        "StarterId",
        "TeamPosition",
        "Team",
        "TeamDisplay",
        "TeamDuellIndex",
        "TeamComment"
    ];

    public static string HeaderLine => string.Join(';', Header);

    public static string SilhouetteImportHeaderLine => string.Join(';', Header.Concat(SilhouetteImportHeader));

    public static void Write(
        string outputPath,
        IReadOnlyList<SiusRankStarter> rows,
        string encodingName,
        bool includeSilhouetteImportColumns = false)
    {
        Write(outputPath, ToCsv(rows, includeSilhouetteImportColumns), encodingName);
    }

    public static void Write(string outputPath, string csv, string encodingName)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputPath, csv, CsvEncoding.GetEncoding(encodingName));
    }

    public static string ToCsv(
        IReadOnlyList<SiusRankStarter> rows,
        bool includeSilhouetteImportColumns = false)
    {
        var builder = new StringBuilder();
        builder
            .Append(includeSilhouetteImportColumns ? SilhouetteImportHeaderLine : HeaderLine)
            .Append("\r\n");

        foreach (var row in rows)
        {
            var fields = includeSilhouetteImportColumns
                ? row.ToFields().Concat(SilhouetteImportFields(row))
                : row.ToFields();
            builder.AppendJoin(';', fields.Select(EscapeField));
            builder.Append("\r\n");
        }

        return builder.ToString();
    }

    private static IEnumerable<string> SilhouetteImportFields(SiusRankStarter row)
    {
        var mapping = SilhouetteImportMapping.ForTarget(row.TargetNumber);
        return mapping is null
            ? [string.Empty, string.Empty]
            : [mapping.ImportShotFilter, mapping.SiusDataStartNumber.ToString(CultureInfo.InvariantCulture)];
    }

    private static string EscapeField(string value)
    {
        if (!value.Contains(';') && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}

public static class CsvEncoding
{
    public const string Utf8Bom = "utf8-bom";
    public const string Windows1252 = "windows-1252";

    public static string NormalizeName(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "utf8-bom" or "utf-8-bom" or "utf8" or "utf-8" => Utf8Bom,
            "windows-1252" or "win-1252" or "cp1252" or "ansi" => Windows1252,
            _ => throw new ArgumentException("Encoding must be utf8-bom or windows-1252.")
        };
    }

    public static Encoding GetEncoding(string encodingName)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return NormalizeName(encodingName) switch
        {
            Utf8Bom => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            Windows1252 => Encoding.GetEncoding(1252),
            _ => throw new ArgumentException("Encoding must be utf8-bom or windows-1252.")
        };
    }
}
