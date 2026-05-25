namespace InrxToSiusRank.Tests;

public sealed class SiusRankTemplateCopierTests
{
    [Fact]
    public void Copy_copies_xml_templates_and_overwrites_existing_targets()
    {
        using var workspace = TempDirectory.Create();
        var source = Path.Combine(workspace.Path, "source");
        var target = Path.Combine(workspace.Path, "target");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(source, "ShooterGroupsTemplate.xml"), "new groups");
        File.WriteAllText(Path.Combine(source, "ShootEventsTemplate2026_NM_Pistol.xml"), "events");
        File.WriteAllText(Path.Combine(source, "notes.txt"), "ignore");
        File.WriteAllText(Path.Combine(target, "ShooterGroupsTemplate.xml"), "old groups");

        var result = SiusRankTemplateCopier.Copy(source, target);

        Assert.Equal(2, result.Files.Count);
        Assert.Contains(result.Files, file =>
            Path.GetFileName(file.TargetPath) == "ShooterGroupsTemplate.xml" && file.Overwritten);
        Assert.Contains(result.Files, file =>
            Path.GetFileName(file.TargetPath) == "ShootEventsTemplate2026_NM_Pistol.xml" && !file.Overwritten);
        Assert.Equal("new groups", File.ReadAllText(Path.Combine(target, "ShooterGroupsTemplate.xml")));
        Assert.Equal("events", File.ReadAllText(Path.Combine(target, "ShootEventsTemplate2026_NM_Pistol.xml")));
        Assert.False(File.Exists(Path.Combine(target, "notes.txt")));
    }

    [Fact]
    public void Copy_fails_when_source_has_no_xml_templates()
    {
        using var workspace = TempDirectory.Create();
        var source = Path.Combine(workspace.Path, "source");
        var target = Path.Combine(workspace.Path, "target");
        Directory.CreateDirectory(source);

        var ex = Assert.Throws<InvalidOperationException>(() => SiusRankTemplateCopier.Copy(source, target));

        Assert.Contains("No XML template files", ex.Message, StringComparison.Ordinal);
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
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
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
