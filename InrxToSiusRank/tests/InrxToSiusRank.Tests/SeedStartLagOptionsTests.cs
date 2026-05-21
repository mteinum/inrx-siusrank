namespace InrxToSiusRank.Tests;

public sealed class SeedStartLagOptionsTests
{
    [Fact]
    public void Parse_accepts_seed_startlag_options()
    {
        using var db = TempFile.Create();

        var options = SeedStartLagCommand.Parse(
        [
            "--db", db.Path,
            "--stevne-ids", "405-407,411",
            "--ranking-period-start", "2025-12-31T23:00:00.000Z",
            "--ranking-period-end", "2026-12-31T22:59:59.999Z",
            "--apply"
        ]);

        Assert.Equal(db.Path, options.DatabasePath);
        Assert.Equal(new[] { 405, 406, 407, 411 }, options.StevneIds);
        Assert.True(options.Apply);
    }

    [Fact]
    public void Parse_defaults_to_dry_run()
    {
        using var db = TempFile.Create();

        var options = SeedStartLagCommand.Parse(["--db", db.Path, "--stevne-id", "406"]);

        Assert.False(options.Apply);
        Assert.Equal([406], options.StevneIds);
        Assert.Equal(SeedStartLagCommand.DefaultRankingPeriodStart, options.RankingPeriodStart);
        Assert.Equal(SeedStartLagCommand.DefaultRankingPeriodEnd, options.RankingPeriodEnd);
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
