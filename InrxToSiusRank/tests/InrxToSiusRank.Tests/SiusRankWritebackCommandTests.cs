namespace InrxToSiusRank.Tests;

public sealed class SiusRankWritebackCommandTests
{
    [Fact]
    public void Parse_accepts_writeback_options_and_defaults_to_dry_run()
    {
        using var db = TempFile.Create("storage.db3");
        using var exports = TempDirectory.Create();
        using var bibMap = TempFile.Create("bib-map.csv", "nsfId,bibNumber,deltakerId,name,source\r\n");

        var options = SiusRankWritebackCommand.Parse(
        [
            "--db", db.Path,
            "--exports", exports.Path,
            "--stevne-ids", "413-417",
            "--bib-map", bibMap.Path,
            "--event", "HurtigFin_M,HurtigGrov_Apen"
        ]);

        Assert.Equal(db.Path, options.DatabasePath);
        Assert.Equal(exports.Path, options.ExportsDirectory);
        Assert.Equal([413, 414, 415, 416, 417], options.StevneIds);
        Assert.Equal(bibMap.Path, options.BibMapPath);
        Assert.False(options.Apply);
        Assert.Contains("HURTIGFINM", options.EventFilters);
        Assert.Contains("HURTIGGROVAPEN", options.EventFilters);
    }

    [Fact]
    public void Parse_accepts_apply()
    {
        using var db = TempFile.Create("storage.db3");
        using var exports = TempDirectory.Create();

        var options = SiusRankWritebackCommand.Parse(
        [
            "--db", db.Path,
            "--exports", exports.Path,
            "--stevne-id", "413",
            "--apply"
        ]);

        Assert.True(options.Apply);
        Assert.Equal([413], options.StevneIds);
    }

    private sealed class TempFile : IDisposable
    {
        private TempFile(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempFile Create(string fileName, string contents = "")
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}-{fileName}");
            File.WriteAllText(path, contents);
            return new TempFile(path);
        }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
