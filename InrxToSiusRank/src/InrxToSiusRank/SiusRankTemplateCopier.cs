namespace InrxToSiusRank;

public static class SiusRankTemplateCopier
{
    public static SiusRankTemplateCopyResult Copy(string sourceDirectory, string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            throw new ArgumentException("SIUS Rank template source directory is required.", nameof(sourceDirectory));
        }

        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            throw new ArgumentException("SIUS Rank template target directory is required.", nameof(targetDirectory));
        }

        var sourceRoot = Path.GetFullPath(sourceDirectory);
        if (!Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException($"SIUS Rank template source directory does not exist: {sourceRoot}");
        }

        var sourceFiles = Directory
            .EnumerateFiles(sourceRoot, "*.xml", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourceFiles.Length == 0)
        {
            throw new InvalidOperationException($"No XML template files found in {sourceRoot}.");
        }

        var targetRoot = Path.GetFullPath(targetDirectory);
        Directory.CreateDirectory(targetRoot);

        var copiedFiles = new List<SiusRankTemplateCopiedFile>();
        foreach (var sourcePath in sourceFiles)
        {
            var fileName = Path.GetFileName(sourcePath);
            var targetPath = Path.Combine(targetRoot, fileName);
            var overwritten = File.Exists(targetPath);
            File.Copy(sourcePath, targetPath, overwrite: true);
            copiedFiles.Add(new SiusRankTemplateCopiedFile(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(targetPath),
                overwritten));
        }

        return new SiusRankTemplateCopyResult(sourceRoot, targetRoot, copiedFiles);
    }
}

public sealed record SiusRankTemplateCopyResult(
    string SourceDirectory,
    string TargetDirectory,
    IReadOnlyList<SiusRankTemplateCopiedFile> Files);

public sealed record SiusRankTemplateCopiedFile(
    string SourcePath,
    string TargetPath,
    bool Overwritten);
