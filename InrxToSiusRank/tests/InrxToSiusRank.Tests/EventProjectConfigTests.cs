using Microsoft.Data.Sqlite;

namespace InrxToSiusRank.Tests;

public sealed class EventProjectConfigTests
{
    [Fact]
    public void Event_json_roundtrips_and_resolves_relative_and_windows_paths()
    {
        using var directory = TempDirectory.Create();
        var eventPath = Path.Combine(directory.Path, EventProjectFile.FileName);
        var config = new EventProjectConfig
        {
            Exercise = new EventExerciseConfig { Id = 11, Name = "Silhuett", ShortName = "Sil", HovedOvelseId = 8 },
            Inrx = new EventInrxConfig { Db = @"C:\Users\ms\Dropbox\KPS-Stevne\INRX191\storage.db3", Stevner = "406" },
            EventTypes = new Dictionary<string, string> { ["406"] = EventProjectPlanner.ChampionshipEventType },
            Silhouette = new EventSilhouetteConfig { ShootersPerStand = 1 },
            Csv = new EventCsvConfig { Output = "./inrX_export" },
            Classes =
            [
                new EventClassConfig
                {
                    Class = "Apen",
                    Folder = "./SiusRank_Silhuett_Apen",
                    Exports = "./SiusRank_Silhuett_Apen/Exports"
                }
            ]
        };

        EventProjectFile.Save(eventPath, config);

        var loaded = EventProjectFile.Load(eventPath);
        Assert.Equal(11, loaded.Exercise.Id);
        Assert.Equal("406", loaded.Inrx.Stevner);
        Assert.Equal(1, loaded.Silhouette.ShootersPerStand);
        Assert.Equal(@"C:\Users\ms\Dropbox\KPS-Stevne\INRX191\storage.db3", loaded.Inrx.Db);
        Assert.Equal(
            Path.Combine(directory.Path, "inrX_export"),
            EventProjectFile.ResolvePath(eventPath, loaded.Csv.Output));
        Assert.Equal(
            loaded.Inrx.Db,
            EventProjectFile.ResolvePath(eventPath, loaded.Inrx.Db));
        Assert.Equal(
            Path.Combine(directory.Path, "inrX_export"),
            EventProjectFile.ResolvePath(eventPath, EventProjectFile.ToStoredPath(eventPath, Path.Combine(directory.Path, "inrX_export"))));

        var outsidePath = Path.Combine(Path.GetTempPath(), "outside-storage.db3");
        Assert.Equal(Path.GetFullPath(outsidePath), EventProjectFile.ToStoredPath(eventPath, outsidePath));
    }

    [Fact]
    public void Class_folder_plan_uses_one_folder_per_effective_class()
    {
        var classes = EventProjectPlanner.BuildClassConfigs(
            new OvelseInfo(11, "Silhuett", "Sil", 8),
            ["V55", "Apen", "Jr-NM", "Apen"]);

        Assert.Equal(["Jr-NM", "Apen", "V55"], classes.Select(item => item.Class));
        Assert.Contains(classes, item =>
            item.Folder == "./SiusRank_Silhuett_Apen" &&
            item.Exports == "./SiusRank_Silhuett_Apen/Exports");
    }

    [Fact]
    public void Inrx_event_type_is_detected_from_championship_columns()
    {
        using var db = TempDatabase.Create();

        using var repository = new InrxRepository(db.Path);

        Assert.Equal(EventProjectPlanner.ChampionshipEventType, repository.GetStevneEventType(1));
        Assert.Equal(EventProjectPlanner.ApprovedEventType, repository.GetStevneEventType(2));
    }

    private sealed class TempDatabase : IDisposable
    {
        private TempDatabase(string path)
        {
            Path = path;
            using var connection = new SqliteConnection($"Data Source={Path}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE Stevne (
                    Id INTEGER NOT NULL PRIMARY KEY,
                    navn TEXT,
                    dato TEXT,
                    ArrangementId INTEGER NOT NULL,
                    mestDm INTEGER NOT NULL DEFAULT 0,
                    mestKm INTEGER NOT NULL DEFAULT 0,
                    mestNm INTEGER NOT NULL DEFAULT 0
                );

                INSERT INTO Stevne (Id, navn, dato, ArrangementId, mestNm)
                VALUES (1, 'NM Silhuett', '2026-07-07', 10, 1);

                INSERT INTO Stevne (Id, navn, dato, ArrangementId, mestNm)
                VALUES (2, 'Approbert Silhuett', '2026-05-24', 11, 0);
                """;
            command.ExecuteNonQuery();
        }

        public string Path { get; }

        public static TempDatabase Create()
        {
            var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return new TempDatabase(System.IO.Path.Combine(directory, "storage.db3"));
        }

        public void Dispose()
        {
            var directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
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
