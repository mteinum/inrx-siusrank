using System.Text;

namespace InrxToSiusRank.Tests;

public sealed class SiusDataStartListExportTests
{
    [Fact]
    public void Reader_finds_sius_data_start_list_rows_recursively()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var directory = TempDirectory.Create();
        var relay = Path.Combine(directory.Path, "SIUS Data", "Relay 1");
        Directory.CreateDirectory(relay);
        var path = Path.Combine(relay, "start_stl.csv");
        File.WriteAllText(
            path,
            ";26001;MØRENSKOG-BAUMANN;Roy;MØRENSKOG-BAUMANN Roy;NOR;2;123;FSSL;15;15;1;18:00;15;1;0;0\r\n",
            Encoding.GetEncoding(1252));
        File.WriteAllText(Path.Combine(relay, "shots.csv"), "26001;10;0;15;10.4\r\n", Encoding.UTF8);

        var rows = SiusDataStartListReader.ReadDirectory(directory.Path);

        var row = Assert.Single(rows);
        Assert.Equal("26001", row.StartNumber);
        Assert.Equal("MØRENSKOG-BAUMANN", row.LastName);
        Assert.Equal("Roy", row.FirstName);
        Assert.Equal("FSSL", row.Club);
        Assert.Equal(15, row.TargetNumber);
        Assert.Equal(1, row.Relay);
    }

    [Fact]
    public void Match_uses_name_target_and_club_and_handles_diacritics()
    {
        var start = new SiusDataStartListRow(
            "start_stl.csv",
            1,
            "26001",
            "MORENSKOG-BAUMANN",
            "Roy",
            "MORENSKOG-BAUMANN Roy",
            "NOR",
            "FSSL",
            15,
            1,
            "18:00");
        var starters = new[]
        {
            CreateStarter(resultatId: 1, firstName: "Roy", lastName: "Mørenskog-Baumann", clubShortName: "FSSL", target: 15),
            CreateStarter(resultatId: 2, firstName: "Roy", lastName: "Mørenskog-Baumann", clubShortName: "KPS", target: 16)
        };
        var warnings = new List<string>();

        var matched = SiusDataStartListExporter.MatchStartListRows([start], starters, warnings);

        var item = Assert.Single(matched);
        Assert.Equal(1, item.Starter.ResultatId);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Command_parses_required_options()
    {
        using var directory = TempDirectory.Create();
        var db = Path.Combine(directory.Path, "storage.db3");
        File.WriteAllText(db, "");
        var siusData = Path.Combine(directory.Path, "SIUS Data");
        Directory.CreateDirectory(siusData);
        var output = Path.Combine(directory.Path, "import");

        var options = SiusDataStartListCommand.Parse([
            "--db", db,
            "--stevne-id", "422",
            "--ovelse-id", "7",
            "--sius-data", siusData,
            "--output-dir", output,
            "--encoding", "windows-1252"
        ]);

        Assert.Equal(db, options.DatabasePath);
        Assert.Equal([422], options.StevneIds);
        Assert.Equal(7, options.OvelseId);
        Assert.Equal(siusData, options.SiusDataDirectory);
        Assert.Equal(output, options.OutputDirectory);
        Assert.Equal(CsvEncoding.Windows1252, options.EncodingName);
    }

    [Fact]
    public void Command_uses_standard_sius_data_directory_when_not_specified()
    {
        using var directory = TempDirectory.Create();
        var db = Path.Combine(directory.Path, "storage.db3");
        File.WriteAllText(db, "");
        var output = Path.Combine(directory.Path, "import");

        var options = SiusDataStartListCommand.Parse([
            "--db", db,
            "--stevne-id", "422",
            "--output-dir", output
        ]);

        Assert.Equal(Path.GetFullPath(SiusDataStartListCommand.DefaultSiusDataDirectory), options.SiusDataDirectory);
    }

    private static InrxStarter CreateStarter(
        int resultatId,
        string firstName,
        string lastName,
        string clubShortName,
        int target) =>
        new(
            ResultatId: resultatId,
            DeltakerId: resultatId + 100,
            Standplass: target,
            SkivenrFra: string.Empty,
            SkivenrTil: string.Empty,
            Relay: 1,
            RelayDate: "2026-05-27 18:00:00",
            NsfId: "905397",
            AccreditationNumber: string.Empty,
            FirstName: firstName,
            LastName: lastName,
            BirthDay: "1970",
            Gender: "M",
            Land: "NOR",
            ClubName: clubShortName,
            ClubShortName: clubShortName,
            InrxClass: "C",
            KmNmClass: "C",
            DmClass: string.Empty,
            OvelseName: "Hurtig Fin",
            StevneName: "20260527 Hurtig F");

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
