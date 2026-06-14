namespace InrxToSiusRank;

public static class SiusRankXlsxExportRunner
{
    public static SiusRankXlsxExportResult Run(SiusRankCsvExportOptions options)
    {
        var plan = SiusRankExportPlanner.Build(options);
        var sheetPlans = plan.Files
            .GroupBy(file => file.Stevne.Id)
            .Select(group => BuildSheet(group.ToList()))
            .ToList();

        var outputPath = Path.Combine(
            plan.OutputDirectory,
            OutputFileName.ForWorkbookImport(sheetPlans.Select(sheet => sheet.Stevne).ToList()));

        SiusRankXlsxWriter.Write(
            outputPath,
            sheetPlans.Select(sheet => sheet.Worksheet).ToList());

        return new SiusRankXlsxExportResult(
            plan.OutputDirectory,
            outputPath,
            plan.ShooterGroupsTemplatePath,
            sheetPlans.Select(sheet => new SiusRankXlsxExportSheetResult(
                sheet.Stevne,
                sheet.Worksheet.Name,
                sheet.StarterCount,
                sheet.Warnings)).ToList());
    }

    private static XlsxSheetPlan BuildSheet(IReadOnlyList<SiusRankPlannedFile> files)
    {
        var stevne = files[0].Stevne;
        var includeSilhouetteImportColumns = files.Any(file => file.IncludeSilhouetteImportColumns);
        var headers = SiusRankExportTable.Headers(includeSilhouetteImportColumns);
        var rows = files
            .SelectMany(file => file.Rows.Select(row => SiusRankExportTable.Fields(row, includeSilhouetteImportColumns)))
            .ToList();
        var sortedRows = SortRowsForWorkbook(headers, rows);

        return new XlsxSheetPlan(
            stevne,
            new SiusRankXlsxWorksheet(
                XlsxSheetNames.ForStevne(stevne),
                headers,
                sortedRows),
            rows.Count,
            files.SelectMany(file => file.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static IReadOnlyList<IReadOnlyList<string>> SortRowsForWorkbook(
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var relayIndex = IndexOfHeader(headers, "Relay");
        var targetIndex = IndexOfHeader(headers, "TargetNumber");
        if (relayIndex < 0 || targetIndex < 0)
        {
            return rows;
        }

        return rows
            .OrderBy(row => SortNumber(row, relayIndex) ?? int.MaxValue)
            .ThenBy(row => Cell(row, relayIndex), StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => SortNumber(row, targetIndex) ?? int.MaxValue)
            .ThenBy(row => Cell(row, targetIndex), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int IndexOfHeader(IReadOnlyList<string> headers, string headerName)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            if (headers[index].Equals(headerName, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static int? SortNumber(IReadOnlyList<string> row, int index) =>
        int.TryParse(Cell(row, index), out var value) ? value : null;

    private static string Cell(IReadOnlyList<string> row, int index) =>
        index >= 0 && index < row.Count ? row[index].Trim() : string.Empty;

    private sealed record XlsxSheetPlan(
        StevneInfo Stevne,
        SiusRankXlsxWorksheet Worksheet,
        int StarterCount,
        IReadOnlyList<string> Warnings);
}
