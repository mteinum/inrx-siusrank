using Microsoft.Data.Sqlite;

namespace InrxToSiusRank.Tests;

public sealed class SscValidationRunnerTests
{
    [Fact]
    public void Validate_ssc_reports_error_when_selected_starter_is_missing_from_users_csv()
    {
        using var db = TempInrxDatabase.Create();
        using var usersCsv = TempFile.Create(
            SscUsersCsv.ToCsv(
            [
                new SscUser(
                    OrganizationName: "Legacy",
                    OrganizationId: "f95a2bc3-79bd-4c24-98b6-4e17f99bbfaf",
                    UserId: "26099",
                    Name: "Annen",
                    FirstName: "Anna",
                    DisplayName: "ANNEN Anna",
                    NationName: "Norway",
                    DisplayNationName: "Norway",
                    ISOCode: "NOR",
                    IOCCode: "NOR",
                    UserClassName: string.Empty,
                    UserClassId: string.Empty,
                    UserGroupName: string.Empty,
                    UserGroupId: string.Empty,
                    ShootingSportsCloudUserId: string.Empty,
                    DateOfBirth: "1980-01-01",
                    Gender: "F",
                    UserPictureId: string.Empty,
                    UserPreferredLanguage: string.Empty)
            ]));

        var result = SscValidationRunner.Run(new ValidateSscOptions(
            db.Path,
            [405],
            BibMapPath: null,
            usersCsv.Path));

        Assert.True(result.HasErrors);
        Assert.Contains(result.Messages, message =>
            message.Severity == SscValidationSeverity.Error &&
            message.Message.Contains("Missing SSC user", StringComparison.Ordinal) &&
            message.Message.Contains("26001", StringComparison.Ordinal));
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
                    Id INTEGER NOT NULL PRIMARY KEY,
                    navn TEXT NOT NULL,
                    dato TEXT NOT NULL,
                    ArrangementId INTEGER NOT NULL
                );

                CREATE TABLE OvelseDef (
                    Id INTEGER NOT NULL PRIMARY KEY,
                    navn TEXT NOT NULL,
                    kortNavn TEXT NOT NULL,
                    HovedOvelseId INTEGER NOT NULL
                );

                CREATE TABLE Deltaker (
                    Id INTEGER NOT NULL PRIMARY KEY,
                    nsfId TEXT DEFAULT '',
                    medlemsnr TEXT DEFAULT '',
                    fnavn TEXT DEFAULT '',
                    enavn TEXT DEFAULT '',
                    foedselsaar TEXT DEFAULT '',
                    gender TEXT DEFAULT '',
                    land TEXT DEFAULT ''
                );

                CREATE TABLE Klubb (
                    Id INTEGER NOT NULL PRIMARY KEY,
                    navn TEXT NOT NULL,
                    kortnavn TEXT NOT NULL
                );

                CREATE TABLE Klasse (
                    Id INTEGER NOT NULL PRIMARY KEY,
                    navn TEXT NOT NULL
                );

                CREATE TABLE Mklasse (
                    Id INTEGER NOT NULL PRIMARY KEY,
                    navn TEXT NOT NULL,
                    sort INTEGER NOT NULL
                );

                CREATE TABLE StartLag (
                    Id INTEGER NOT NULL PRIMARY KEY,
                    nr INTEGER NOT NULL,
                    dato TEXT NOT NULL
                );

                CREATE TABLE Resultat (
                    Id INTEGER NOT NULL PRIMARY KEY,
                    StevneId INTEGER NOT NULL,
                    OvelseDefId INTEGER NOT NULL,
                    DeltakerId INTEGER NOT NULL,
                    standplass INTEGER NOT NULL DEFAULT 0,
                    skivenrFra TEXT DEFAULT '',
                    skivenrTil TEXT DEFAULT '',
                    startLagId INTEGER,
                    KlubbId INTEGER NOT NULL,
                    KlasseId INTEGER NOT NULL,
                    MklasseId1 INTEGER,
                    MklasseId2 INTEGER
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
                VALUES (405, 'NM Fripistol', '2026-07-06 09:00:00', 1);

                INSERT INTO OvelseDef (Id, navn, kortNavn, HovedOvelseId)
                VALUES (18, 'Fripistol', 'Fri', 2);

                INSERT INTO Deltaker (Id, nsfId, medlemsnr, fnavn, enavn, foedselsaar, gender, land)
                VALUES (100, '905380', '', 'Morten', 'Teinum', '1973-06-23', 'M', 'Norge');

                INSERT INTO Klubb (Id, navn, kortnavn)
                VALUES (1, 'Kristiansand Pistolskyttere', 'KPS');

                INSERT INTO Klasse (Id, navn)
                VALUES (1, '-');

                INSERT INTO Mklasse (Id, navn, sort)
                VALUES (1, 'Å', 1);

                INSERT INTO StartLag (Id, nr, dato)
                VALUES (1, 1, '2026-07-06 09:00:00');

                INSERT INTO Resultat (
                    Id, StevneId, OvelseDefId, DeltakerId, standplass, skivenrFra, skivenrTil,
                    startLagId, KlubbId, KlasseId, MklasseId1, MklasseId2)
                VALUES (1001, 405, 18, 100, 5, '', '', 1, 1, 1, 1, NULL);
                """;
            command.ExecuteNonQuery();
        }
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
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
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
}
