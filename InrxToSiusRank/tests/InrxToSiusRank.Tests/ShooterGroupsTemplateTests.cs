namespace InrxToSiusRank.Tests;

public sealed class ShooterGroupsTemplateTests
{
    [Fact]
    public void Loads_shooter_group_names_and_indexes()
    {
        using var file = TempFile.Create(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Configuration ConfigurationType="ShooterGroupConfiguration">
              <ShooterGroupConfiguration>
                <Name>Apen</Name>
                <Index>106</Index>
                <ShortName>Apen</ShortName>
              </ShooterGroupConfiguration>
            </Configuration>
            """);

        var template = ShooterGroupsTemplate.Load(file.Path);

        Assert.True(template.TryGet("Apen", out var group));
        Assert.Equal(106, group.Index);
    }

    private sealed class TempFile : IDisposable
    {
        private TempFile(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempFile Create(string contents)
        {
            var path = System.IO.Path.GetTempFileName();
            File.WriteAllText(path, contents);
            return new TempFile(path);
        }

        public void Dispose()
        {
            File.Delete(Path);
        }
    }
}
