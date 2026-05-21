namespace InrxToSiusRank.Tests;

public sealed class TimetableCommandTests
{
    [Fact]
    public void Parse_defaults_to_nm_stevne_ids()
    {
        using var db = TempFile.Create();

        var options = TimetableCommand.Parse(["--db", db.Path]);

        Assert.Equal(db.Path, options.DatabasePath);
        Assert.Equal([405, 406, 407, 408, 409, 410, 411], options.StevneIds);
    }

    [Fact]
    public void Parse_accepts_stevne_id_range()
    {
        using var db = TempFile.Create();

        var options = TimetableCommand.Parse(["--db", db.Path, "--stevne-ids", "405-406,411"]);

        Assert.Equal([405, 406, 411], options.StevneIds);
    }

    private sealed class TempFile : IDisposable
    {
        private TempFile(string path)
        {
            Path = path;
            File.WriteAllText(path, string.Empty);
        }

        public string Path { get; }

        public static TempFile Create() =>
            new(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.db3"));

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
