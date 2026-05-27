using Microsoft.Data.Sqlite;

namespace InrxToSiusRank.Tests;

public sealed class SeedStartLagRepositoryTests
{
    [Fact]
    public void ResolveSilhouetteTargets_uses_middle_targets_for_one_shooter_per_stand()
    {
        Assert.Equal([3, 8, 13, 18, 23, 28, 33], SeedStartLagRepository.ResolveSilhouetteTargets(1));
    }

    [Fact]
    public void ResolveSilhouetteTargets_uses_side_targets_for_two_shooters_per_stand()
    {
        Assert.Equal([2, 4, 7, 9, 12, 14, 17, 19, 22, 24, 27, 29, 32, 34], SeedStartLagRepository.ResolveSilhouetteTargets(2));
    }

    [Fact]
    public void Apply_creates_missing_startlag_and_updates_resultat()
    {
        using var db = TempDatabase.Create();
        SeedStartLagRepository.CreateBackup(db.Path);
        var plan = CreatePlan();

        SeedStartLagRepository.Apply(db.Path, [plan]);

        using var connection = new SqliteConnection($"Data Source={db.Path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT sl.Id, sl.dato, r.startLagId, r.standplass, r.skivenrFra, r.skivenrTil
            FROM StartLag sl
            JOIN Resultat r ON r.startLagId = sl.Id
            WHERE sl.nr = 11 AND r.Id = 1001;
            """;
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("2026-07-07 16:30:00", reader.GetString(1));
        Assert.Equal(reader.GetInt32(0), reader.GetInt32(2));
        Assert.Equal(3, reader.GetInt32(3));
        Assert.Equal("3", reader.GetString(4));
        Assert.Equal("3", reader.GetString(5));

        var backup = Directory.GetFiles(System.IO.Path.GetDirectoryName(db.Path)!, "*.bak-seed-*").Single();
        Assert.True(File.Exists(backup));
    }

    [Fact]
    public void Apply_deletes_empty_startlag_beyond_required_count()
    {
        using var db = TempDatabase.Create();
        InsertReferencedResult(db.Path, resultId: 2002, startLagId: 5);
        var plan = CreatePlan(relayNumber: 2);

        SeedStartLagRepository.Apply(db.Path, [plan]);

        using var connection = new SqliteConnection($"Data Source={db.Path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT nr
            FROM StartLag
            ORDER BY nr;
            """;
        using var reader = command.ExecuteReader();
        var relayNumbers = new List<int>();
        while (reader.Read())
        {
            relayNumbers.Add(reader.GetInt32(0));
        }

        Assert.Equal([1, 2, 5], relayNumbers);
    }

    private static SeedStartLagEventPlan CreatePlan(int relayNumber = 11)
    {
        var startLags = Enumerable.Range(1, 10)
            .Select(nr => new StartLagInfo(
                nr,
                nr,
                new DateTime(2026, 7, 7, 9, 0, 0).AddMinutes((nr - 1) * 45).ToString("yyyy-MM-dd HH:mm:ss")))
            .ToList();
        var assignment = new PlannedStartLagAssignment(
            1001,
            "Oddmund Fjerdingen",
            "Rømskog PK",
            "V73",
            relayNumber,
            3,
            10,
            18,
            IsSeed: false,
            RankingPosition: null,
            RankingScore: null);

        return new SeedStartLagEventPlan(
            new StevneInfo(406, "20260707 NM Silhuettpistol 2026", "2026-07-07 09:00:00", 378),
            new OvelseInfo(11, "Silhuett", "Sil", 8),
            "discipline",
            [2, 4, 7, 9, 12, 14, 17, 19, 22, 24, 27, 29, 32, 34],
            TimeSpan.FromMinutes(45),
            startLags,
            [assignment],
            [],
            [],
            []);
    }

    private static void InsertReferencedResult(string databasePath, int resultId, int startLagId)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Resultat (Id, startLagId, standplass, skivenrFra, skivenrTil)
            VALUES ($resultId, $startLagId, 1, '', '');
            """;
        command.Parameters.AddWithValue("$resultId", resultId);
        command.Parameters.AddWithValue("$startLagId", startLagId);
        command.ExecuteNonQuery();
    }

    private sealed class TempDatabase : IDisposable
    {
        private TempDatabase(string path)
        {
            Path = path;
            CreateSchema();
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

        private void CreateSchema()
        {
            using var connection = new SqliteConnection($"Data Source={Path}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE StartLag (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    ArrangementId INTEGER NOT NULL,
                    HovedOvelseId INTEGER NOT NULL,
                    dato TEXT NOT NULL,
                    nr INTEGER NOT NULL
                );

                CREATE TABLE Resultat (
                    Id INTEGER NOT NULL PRIMARY KEY,
                    startLagId INTEGER NOT NULL DEFAULT -1,
                    standplass INTEGER NOT NULL DEFAULT 0,
                    skivenrFra TEXT DEFAULT '',
                    skivenrTil TEXT DEFAULT ''
                );
                """;
            command.ExecuteNonQuery();

            for (var nr = 1; nr <= 10; nr++)
            {
                using var insert = connection.CreateCommand();
                insert.CommandText =
                    """
                    INSERT INTO StartLag (Id, ArrangementId, HovedOvelseId, dato, nr)
                    VALUES ($id, 378, 8, $dato, $nr);
                    """;
                insert.Parameters.AddWithValue("$id", nr);
                insert.Parameters.AddWithValue("$dato", new DateTime(2026, 7, 7, 9, 0, 0).AddMinutes((nr - 1) * 45).ToString("yyyy-MM-dd HH:mm:ss"));
                insert.Parameters.AddWithValue("$nr", nr);
                insert.ExecuteNonQuery();
            }

            using var result = connection.CreateCommand();
            result.CommandText =
                """
                INSERT INTO Resultat (Id, startLagId, standplass, skivenrFra, skivenrTil)
                VALUES (1001, 10, 18, '', '');
                """;
            result.ExecuteNonQuery();
        }
    }
}
