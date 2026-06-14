using System.IO.Compression;
using System.Xml;

namespace InrxToSiusRank;

public sealed record SiusRankXlsxWorksheet(
    string Name,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows);

public static class SiusRankXlsxWriter
{
    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly HashSet<string> NumericColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "StartNumber",
        "AccreditationNumber",
        "BibNumber",
        "TargetNumber",
        "Relay",
        "TeamIndex",
        "DuellIndex",
        "StarterId",
        "TeamPosition",
        "TeamDuellIndex",
        "SiusDataStartNumber"
    };

    public static void Write(string outputPath, IReadOnlyList<SiusRankXlsxWorksheet> worksheets)
    {
        if (worksheets.Count == 0)
        {
            throw new ArgumentException("XLSX export requires at least one worksheet.", nameof(worksheets));
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var sheets = UniqueWorksheetNames(worksheets);
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        WriteContentTypes(archive, sheets.Count);
        WriteRootRelationships(archive);
        WriteWorkbook(archive, sheets);
        WriteWorkbookRelationships(archive, sheets.Count);
        WriteStyles(archive);

        for (var index = 0; index < sheets.Count; index++)
        {
            WriteWorksheet(archive, $"xl/worksheets/sheet{index + 1}.xml", sheets[index]);
        }
    }

    private static IReadOnlyList<SiusRankXlsxWorksheet> UniqueWorksheetNames(IReadOnlyList<SiusRankXlsxWorksheet> worksheets)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<SiusRankXlsxWorksheet>();
        foreach (var worksheet in worksheets)
        {
            var uniqueName = XlsxSheetNames.Unique(worksheet.Name, used);
            result.Add(worksheet with { Name = uniqueName });
        }

        return result;
    }

    private static void WriteContentTypes(ZipArchive archive, int sheetCount)
    {
        using var writer = CreateWriter(archive, "[Content_Types].xml");
        writer.WriteStartDocument();
        writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
        writer.WriteStartElement("Default");
        writer.WriteAttributeString("Extension", "rels");
        writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-package.relationships+xml");
        writer.WriteEndElement();
        writer.WriteStartElement("Default");
        writer.WriteAttributeString("Extension", "xml");
        writer.WriteAttributeString("ContentType", "application/xml");
        writer.WriteEndElement();
        writer.WriteStartElement("Override");
        writer.WriteAttributeString("PartName", "/xl/workbook.xml");
        writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
        writer.WriteEndElement();
        writer.WriteStartElement("Override");
        writer.WriteAttributeString("PartName", "/xl/styles.xml");
        writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
        writer.WriteEndElement();

        for (var index = 1; index <= sheetCount; index++)
        {
            writer.WriteStartElement("Override");
            writer.WriteAttributeString("PartName", $"/xl/worksheets/sheet{index}.xml");
            writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteRootRelationships(ZipArchive archive)
    {
        using var writer = CreateWriter(archive, "_rels/.rels");
        writer.WriteStartDocument();
        writer.WriteStartElement("Relationships", PackageRelationshipsNamespace);
        WriteRelationship(
            writer,
            "rId1",
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument",
            "xl/workbook.xml");
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteWorkbook(ZipArchive archive, IReadOnlyList<SiusRankXlsxWorksheet> sheets)
    {
        using var writer = CreateWriter(archive, "xl/workbook.xml");
        writer.WriteStartDocument();
        writer.WriteStartElement("workbook", SpreadsheetNamespace);
        writer.WriteAttributeString("xmlns", "r", null, RelationshipsNamespace);
        writer.WriteStartElement("sheets");

        for (var index = 0; index < sheets.Count; index++)
        {
            writer.WriteStartElement("sheet");
            writer.WriteAttributeString("name", sheets[index].Name);
            writer.WriteAttributeString("sheetId", (index + 1).ToString());
            writer.WriteAttributeString("r", "id", RelationshipsNamespace, $"rId{index + 1}");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteWorkbookRelationships(ZipArchive archive, int sheetCount)
    {
        using var writer = CreateWriter(archive, "xl/_rels/workbook.xml.rels");
        writer.WriteStartDocument();
        writer.WriteStartElement("Relationships", PackageRelationshipsNamespace);

        for (var index = 1; index <= sheetCount; index++)
        {
            WriteRelationship(
                writer,
                $"rId{index}",
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet",
                $"worksheets/sheet{index}.xml");
        }

        WriteRelationship(
            writer,
            $"rId{sheetCount + 1}",
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles",
            "styles.xml");

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteStyles(ZipArchive archive)
    {
        using var writer = CreateWriter(archive, "xl/styles.xml");
        writer.WriteStartDocument();
        writer.WriteStartElement("styleSheet", SpreadsheetNamespace);
        writer.WriteRaw("""
<fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts>
<fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FFFFFF00"/><bgColor indexed="64"/></patternFill></fill></fills>
<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
<cellXfs count="3"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/></cellXfs>
<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
""");
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteWorksheet(ZipArchive archive, string entryName, SiusRankXlsxWorksheet sheet)
    {
        using var writer = CreateWriter(archive, entryName);
        var columnCount = sheet.Headers.Count;
        var rowCount = sheet.Rows.Count + 1;
        var range = RangeReference(columnCount, rowCount);

        writer.WriteStartDocument();
        writer.WriteStartElement("worksheet", SpreadsheetNamespace);
        writer.WriteStartElement("dimension");
        writer.WriteAttributeString("ref", range);
        writer.WriteEndElement();
        writer.WriteStartElement("sheetViews");
        writer.WriteStartElement("sheetView");
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane");
        writer.WriteAttributeString("ySplit", "1");
        writer.WriteAttributeString("topLeftCell", "A2");
        writer.WriteAttributeString("activePane", "bottomLeft");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("sheetFormatPr");
        writer.WriteAttributeString("defaultRowHeight", "15");
        writer.WriteEndElement();
        WriteColumns(writer, columnCount);
        writer.WriteStartElement("sheetData");
        WriteHeaderRow(writer, sheet.Headers);

        for (var rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
        {
            WriteRow(writer, rowIndex + 2, sheet.Rows[rowIndex], sheet.Headers, styleIndex: 0, allowNumericCells: true);
        }

        writer.WriteEndElement();
        writer.WriteStartElement("autoFilter");
        writer.WriteAttributeString("ref", range);
        WriteDefaultSortState(writer, sheet);
        writer.WriteEndElement();
        WriteIgnoredNumberStoredAsTextErrors(writer, columnCount, rowCount);
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteIgnoredNumberStoredAsTextErrors(XmlWriter writer, int columnCount, int rowCount)
    {
        if (rowCount < 2)
        {
            return;
        }

        writer.WriteStartElement("ignoredErrors");
        writer.WriteStartElement("ignoredError");
        writer.WriteAttributeString("sqref", $"A2:{ColumnName(columnCount)}{rowCount}");
        writer.WriteAttributeString("numberStoredAsText", "1");
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteDefaultSortState(XmlWriter writer, SiusRankXlsxWorksheet sheet)
    {
        if (sheet.Rows.Count == 0)
        {
            return;
        }

        var relayColumn = FindColumn(sheet.Headers, "Relay");
        var targetColumn = FindColumn(sheet.Headers, "TargetNumber");
        if (relayColumn is null || targetColumn is null)
        {
            return;
        }

        var rowCount = sheet.Rows.Count + 1;
        writer.WriteStartElement("sortState");
        writer.WriteAttributeString("ref", $"A2:{ColumnName(sheet.Headers.Count)}{rowCount}");
        WriteSortCondition(writer, relayColumn.Value, rowCount);
        WriteSortCondition(writer, targetColumn.Value, rowCount);
        writer.WriteEndElement();
    }

    private static void WriteSortCondition(XmlWriter writer, int columnNumber, int rowCount)
    {
        var column = ColumnName(columnNumber);
        writer.WriteStartElement("sortCondition");
        writer.WriteAttributeString("ref", $"{column}2:{column}{rowCount}");
        writer.WriteEndElement();
    }

    private static int? FindColumn(IReadOnlyList<string> headers, string headerName)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            if (headers[index].Equals(headerName, StringComparison.OrdinalIgnoreCase))
            {
                return index + 1;
            }
        }

        return null;
    }

    private static void WriteColumns(XmlWriter writer, int columnCount)
    {
        writer.WriteStartElement("cols");
        for (var column = 1; column <= columnCount; column++)
        {
            writer.WriteStartElement("col");
            writer.WriteAttributeString("min", column.ToString());
            writer.WriteAttributeString("max", column.ToString());
            writer.WriteAttributeString("width", column <= 2 ? "14" : "18");
            writer.WriteAttributeString("customWidth", "1");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteHeaderRow(XmlWriter writer, IReadOnlyList<string> headers)
    {
        writer.WriteStartElement("row");
        writer.WriteAttributeString("r", "1");
        for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
        {
            var styleIndex = headers[columnIndex].Equals("Groups", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
            WriteCell(writer, 1, columnIndex + 1, headers[columnIndex], headers[columnIndex], styleIndex, allowNumericCells: false);
        }

        writer.WriteEndElement();
    }

    private static void WriteRow(
        XmlWriter writer,
        int rowIndex,
        IReadOnlyList<string> values,
        IReadOnlyList<string> headers,
        int styleIndex,
        bool allowNumericCells)
    {
        writer.WriteStartElement("row");
        writer.WriteAttributeString("r", rowIndex.ToString());
        for (var columnIndex = 0; columnIndex < values.Count; columnIndex++)
        {
            var header = columnIndex < headers.Count ? headers[columnIndex] : string.Empty;
            WriteCell(writer, rowIndex, columnIndex + 1, values[columnIndex], header, styleIndex, allowNumericCells);
        }

        writer.WriteEndElement();
    }

    private static void WriteCell(
        XmlWriter writer,
        int rowIndex,
        int columnIndex,
        string value,
        string header,
        int styleIndex,
        bool allowNumericCells)
    {
        writer.WriteStartElement("c");
        writer.WriteAttributeString("r", $"{ColumnName(columnIndex)}{rowIndex}");
        if (styleIndex > 0)
        {
            writer.WriteAttributeString("s", styleIndex.ToString());
        }

        if (allowNumericCells && ShouldWriteNumber(header, value))
        {
            writer.WriteStartElement("v");
            writer.WriteString(value.Trim());
            writer.WriteEndElement();
            writer.WriteEndElement();
            return;
        }

        writer.WriteAttributeString("t", "inlineStr");
        writer.WriteStartElement("is");
        writer.WriteStartElement("t");
        if (value.Length != value.Trim().Length)
        {
            writer.WriteAttributeString("xml", "space", null, "preserve");
        }

        writer.WriteString(value);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static bool ShouldWriteNumber(string header, string value)
    {
        var trimmed = value.Trim();
        return NumericColumns.Contains(header) &&
               trimmed.Length > 0 &&
               IsInteger(trimmed) &&
               !HasLeadingZero(trimmed);
    }

    private static bool IsInteger(string value) =>
        value.All(ch => ch >= '0' && ch <= '9');

    private static bool HasLeadingZero(string value) =>
        value.Length > 1 && value[0] == '0';

    private static void WriteRelationship(XmlWriter writer, string id, string type, string target)
    {
        writer.WriteStartElement("Relationship");
        writer.WriteAttributeString("Id", id);
        writer.WriteAttributeString("Type", type);
        writer.WriteAttributeString("Target", target);
        writer.WriteEndElement();
    }

    private static XmlWriter CreateWriter(ZipArchive archive, string entryName)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        return XmlWriter.Create(
            entry.Open(),
            new XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false), Indent = false, CloseOutput = true });
    }

    private static string RangeReference(int columnCount, int rowCount) =>
        $"A1:{ColumnName(Math.Max(1, columnCount))}{Math.Max(1, rowCount)}";

    private static string ColumnName(int columnNumber)
    {
        var dividend = columnNumber;
        var columnName = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }
}

internal static class XlsxSheetNames
{
    private static readonly char[] InvalidSheetNameChars = ['[', ']', ':', '*', '?', '/', '\\'];

    public static string ForStevne(StevneInfo stevne)
    {
        var date = stevne.Date.Length >= 10
            ? stevne.Date[..10].Replace("-", string.Empty, StringComparison.Ordinal)
            : stevne.Id.ToString();
        return Sanitize($"{date} {ShortName(stevne.Name)}");
    }

    public static string Unique(string name, ISet<string> used)
    {
        var baseName = Sanitize(name);
        var candidate = baseName;
        var suffix = 2;
        while (!used.Add(candidate))
        {
            var marker = $" {suffix}";
            candidate = TrimToLimit(baseName, marker.Length) + marker;
            suffix++;
        }

        return candidate;
    }

    private static string ShortName(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 9 && trimmed[..8].All(char.IsDigit) && char.IsWhiteSpace(trimmed[8]))
        {
            trimmed = trimmed[9..].Trim();
        }

        return trimmed;
    }

    private static string Sanitize(string value)
    {
        var text = new string(value
            .Select(ch => InvalidSheetNameChars.Contains(ch) ? ' ' : ch)
            .ToArray())
            .Trim();

        return TrimToLimit(string.IsNullOrWhiteSpace(text) ? "Stevne" : text, 0);
    }

    private static string TrimToLimit(string value, int reservedCharacters)
    {
        var limit = Math.Max(1, 31 - reservedCharacters);
        return value.Length <= limit ? value : value[..limit].Trim();
    }
}
