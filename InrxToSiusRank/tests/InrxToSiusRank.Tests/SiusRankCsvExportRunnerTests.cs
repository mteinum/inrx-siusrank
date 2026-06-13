using Microsoft.Data.Sqlite;

namespace InrxToSiusRank.Tests;

public sealed class SiusRankCsvExportRunnerTests
{
    [Fact]
    public void Run_writes_one_file_per_competition_with_multiple_groups()
    {
        using var database = TempInrxDatabase.Create();
        using var output = TempDirectory.Create();

        var result = SiusRankCsvExportRunner.Run(new SiusRankCsvExportOptions(
            DatabasePath: database.Path,
            StevneId: 410,
            StevneIds: [],
            EventDate: null,
            EventName: null,
            OvelseId: 9,
            OvelseName: null,
            ShooterGroupsTemplatePath: null,
            OutputDirectory: output.Path,
            EncodingName: CsvEncoding.Utf8Bom));

        var file = Assert.Single(result.Files);
        Assert.Equal("Apen,V55", file.KmNmClass);
        Assert.Equal("20260711_Fin.csv", Path.GetFileName(file.OutputPath));
        Assert.Equal(2, file.StarterCount);

        var csv = File.ReadAllText(file.OutputPath);
        Assert.Contains(";Apen;", csv);
        Assert.Contains(";V55;", csv);
        Assert.DoesNotContain("20260711_Fin_Apen.csv", Directory.GetFiles(output.Path).Select(Path.GetFileName));
        Assert.DoesNotContain("20260711_Fin_V55.csv", Directory.GetFiles(output.Path).Select(Path.GetFileName));
    }

    private sealed class TempInrxDatabase : IDisposable
    {
        private TempInrxDatabase(string path)
        {
            Path = path;
            CreateSchema();
            InsertData();
        }

        public string Path { get; }

        public static TempInrxDatabase Create()
        {
            var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return new TempInrxDatabase(System.IO.Path.Combine(directory, "storage.db3"));
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
                CREATE TABLE Stevne (
                    Id INTEGER PRIMARY KEY,
                    navn TEXT NOT NULL,
                    dato TEXT NOT NULL,
                    ArrangementId INTEGER NOT NULL
                );
                CREATE TABLE OvelseDef (
                    Id INTEGER PRIMARY KEY,
                    navn TEXT NOT NULL,
                    kortNavn TEXT NOT NULL,
                    HovedOvelseId INTEGER NOT NULL
                );
                CREATE TABLE Deltaker (
                    Id INTEGER PRIMARY KEY,
                    nsfId TEXT NOT NULL,
                    medlemsnr TEXT NOT NULL,
                    fnavn TEXT NOT NULL,
                    enavn TEXT NOT NULL,
                    foedselsaar TEXT NOT NULL,
                    gender TEXT NOT NULL,
                    land TEXT NOT NULL
                );
                CREATE TABLE Klubb (
                    Id INTEGER PRIMARY KEY,
                    navn TEXT NOT NULL,
                    kortnavn TEXT NOT NULL
                );
                CREATE TABLE Klasse (
                    Id INTEGER PRIMARY KEY,
                    navn TEXT NOT NULL
                );
                CREATE TABLE Mklasse (
                    Id INTEGER PRIMARY KEY,
                    navn TEXT NOT NULL,
                    sort INTEGER NOT NULL
                );
                CREATE TABLE StartLag (
                    Id INTEGER PRIMARY KEY,
                    nr INTEGER NOT NULL,
                    dato TEXT NOT NULL
                );
                CREATE TABLE Resultat (
                    Id INTEGER PRIMARY KEY,
                    StevneId INTEGER NOT NULL,
                    OvelseDefId INTEGER NOT NULL,
                    DeltakerId INTEGER NOT NULL,
                    KlubbId INTEGER NOT NULL,
                    KlasseId INTEGER NOT NULL,
                    MklasseId1 INTEGER NULL,
                    MklasseId2 INTEGER NULL,
                    startLagId INTEGER NOT NULL,
                    standplass INTEGER NOT NULL,
                    skivenrFra TEXT NOT NULL,
                    skivenrTil TEXT NOT NULL,
                    kommentar TEXT NOT NULL
                );
                """;
            command.ExecuteNonQuery();
        }

        private void InsertData()
        {
            using var connection = new SqliteConnection($"Data Source={Path}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO Stevne (Id, navn, dato, ArrangementId)
                VALUES (410, '20260711 NM Finpistol 2026', '2026-07-11 09:00:00', 377);

                INSERT INTO OvelseDef (Id, navn, kortNavn, HovedOvelseId)
                VALUES (9, 'Finpistol', 'Fin', 10);

                INSERT INTO Klubb (Id, navn, kortnavn)
                VALUES (1, 'Kristiansand Pistolskyttere', 'KPS');

                INSERT INTO Klasse (Id, navn)
                VALUES (1, 'A');

                INSERT INTO Mklasse (Id, navn, sort)
                VALUES (1, 'Å', 1),
                       (2, 'V55', 2);

                INSERT INTO StartLag (Id, nr, dato)
                VALUES (1, 1, '2026-07-11 09:00:00');

                INSERT INTO Deltaker (Id, nsfId, medlemsnr, fnavn, enavn, foedselsaar, gender, land)
                VALUES (100, '900100', '', 'Anne', 'Aasen', '1980-01-01', 'K', 'NOR'),
                       (101, '900101', '', 'Bjarne', 'Berg', '1960-01-01', 'M', 'NOR');

                INSERT INTO Resultat (
                    Id, StevneId, OvelseDefId, DeltakerId, KlubbId, KlasseId, MklasseId1, MklasseId2,
                    startLagId, standplass, skivenrFra, skivenrTil, kommentar)
                VALUES
                    (1000, 410, 9, 100, 1, 1, 1, NULL, 1, 1, '', '', ''),
                    (1001, 410, 9, 101, 1, 1, 2, NULL, 1, 2, '', '', '');
                """;
            command.ExecuteNonQuery();
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
