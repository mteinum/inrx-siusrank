namespace InrxToSiusRank;

public static class SiusRankCsvExportRunner
{
    public static SiusRankCsvExportResult Run(SiusRankCsvExportOptions options)
    {
        var plan = SiusRankExportPlanner.Build(options);
        var results = new List<SiusRankCsvExportFileResult>();
        foreach (var fileExport in plan.Files)
        {
            var outputPath = Path.Combine(
                plan.OutputDirectory,
                fileExport.SiusEventCode is null
                    ? OutputFileName.ForCompetitionImport(fileExport.Stevne, fileExport.Ovelse)
                    : OutputFileName.ForSiusEventImport(fileExport.Stevne, fileExport.SiusEventCode));
            SiusRankCsvWriter.Write(
                outputPath,
                fileExport.Rows,
                options.EncodingName,
                fileExport.IncludeSilhouetteImportColumns);

            results.Add(new SiusRankCsvExportFileResult(
                fileExport.Stevne,
                fileExport.Ovelse,
                fileExport.KmNmClass,
                fileExport.Rows.Count,
                outputPath,
                fileExport.Warnings));
        }

        return new SiusRankCsvExportResult(plan.OutputDirectory, plan.ShooterGroupsTemplatePath, results);
    }
}
