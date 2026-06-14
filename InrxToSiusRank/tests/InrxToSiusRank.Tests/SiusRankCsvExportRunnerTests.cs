using System.IO.Compression;
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

    [Fact]
    public void Run_can_split_nm_silhouette_into_final_events_and_combined_non_final_event()
    {
        using var database = TempInrxDatabase.Create();
        using var output = TempDirectory.Create();

        var result = SiusRankCsvExportRunner.Run(new SiusRankCsvExportOptions(
            DatabasePath: database.Path,
            StevneId: 410,
            StevneIds: [],
            EventDate: null,
            EventName: null,
            OvelseId: 11,
            OvelseName: null,
            ShooterGroupsTemplatePath: null,
            OutputDirectory: output.Path,
            EncodingName: CsvEncoding.Utf8Bom,
            FinalClasses: ["Apen", "Jm"]));

        Assert.Equal(
            ["20260711_Silhuett_Apen.csv", "20260711_Silhuett_Jm.csv", "20260711_Silhuett.csv"],
            result.Files.Select(file => Path.GetFileName(file.OutputPath)).ToArray());
        Assert.Equal("Apen", result.Files[0].KmNmClass);
        Assert.Equal("Jrm", result.Files[1].KmNmClass);
        Assert.Equal("V55,V65,V73", result.Files[2].KmNmClass);

        var veteranCsv = File.ReadAllText(Path.Combine(output.Path, "20260711_Silhuett.csv"));
        Assert.Contains(";V55;", veteranCsv);
        Assert.Contains(";V65;", veteranCsv);
        Assert.Contains(";V73;", veteranCsv);
        Assert.DoesNotContain(";Apen;", veteranCsv);
        Assert.DoesNotContain(";Jrm;", veteranCsv);
    }

    [Fact]
    public void Run_keeps_non_final_classes_in_combined_file_even_when_template_has_final_option()
    {
        using var database = TempInrxDatabase.Create();
        using var output = TempDirectory.Create();

        var result = SiusRankCsvExportRunner.Run(new SiusRankCsvExportOptions(
            DatabasePath: database.Path,
            StevneId: 410,
            StevneIds: [],
            EventDate: null,
            EventName: null,
            OvelseId: 18,
            OvelseName: null,
            ShooterGroupsTemplatePath: null,
            OutputDirectory: output.Path,
            EncodingName: CsvEncoding.Utf8Bom,
            FinalClasses: ["Apen"]));

        var file = Assert.Single(result.Files);
        Assert.Equal("20260711_Fri.csv", Path.GetFileName(file.OutputPath));
        Assert.Equal("SH1", file.KmNmClass);

        var csv = File.ReadAllText(file.OutputPath);
        Assert.Contains(";SH1;", csv);
    }

    [Fact]
    public void Run_uses_per_exercise_final_class_rules_when_exporting_all_exercises()
    {
        using var database = TempInrxDatabase.Create();
        using var output = TempDirectory.Create();

        var result = SiusRankCsvExportRunner.Run(new SiusRankCsvExportOptions(
            DatabasePath: database.Path,
            StevneId: null,
            StevneIds: [410],
            EventDate: null,
            EventName: null,
            OvelseId: null,
            OvelseName: null,
            ShooterGroupsTemplatePath: null,
            OutputDirectory: output.Path,
            EncodingName: CsvEncoding.Utf8Bom,
            FinalClasses: SiusRankCsvFinalClassRules.ParseText(
                """
                Fin: Apen
                Silhuett: Apen,Jm
                """)));

        Assert.Equal(
            [
                "20260711_Fin_Apen.csv",
                "20260711_Fin.csv",
                "20260711_Fri.csv",
                "20260711_Silhuett_Apen.csv",
                "20260711_Silhuett_Jm.csv",
                "20260711_Silhuett.csv"
            ],
            result.Files.Select(file => Path.GetFileName(file.OutputPath)).ToArray());

        var fripistolCsv = File.ReadAllText(Path.Combine(output.Path, "20260711_Fri.csv"));
        Assert.Contains(";SH1;", fripistolCsv);
        Assert.DoesNotContain("20260711_Fri_Apen.csv", Directory.GetFiles(output.Path).Select(Path.GetFileName));
    }

    [Fact]
    public void Run_xlsx_writes_one_workbook_with_one_sheet_per_selected_event_and_filter()
    {
        using var database = TempInrxDatabase.Create();
        using var output = TempDirectory.Create();
        AddSecondStevne(database.Path);

        var result = SiusRankXlsxExportRunner.Run(new SiusRankCsvExportOptions(
            DatabasePath: database.Path,
            StevneId: null,
            StevneIds: [410, 411],
            EventDate: null,
            EventName: null,
            OvelseId: null,
            OvelseName: null,
            ShooterGroupsTemplatePath: null,
            OutputDirectory: output.Path,
            EncodingName: CsvEncoding.Utf8Bom));

        Assert.Equal("20260711-20260712_SIUS_Rank.xlsx", Path.GetFileName(result.OutputPath));
        Assert.True(File.Exists(result.OutputPath));
        Assert.Equal(2, result.Sheets.Count);
        Assert.Equal([410, 411], result.Sheets.Select(sheet => sheet.Stevne.Id).ToArray());

        using var archive = ZipFile.OpenRead(result.OutputPath);
        var workbookXml = ReadZipEntry(archive, "xl/workbook.xml");
        Assert.Contains("20260711 NM Finpistol 2026", workbookXml);
        Assert.Contains("20260712 NM Standard 2026", workbookXml);
        var stylesXml = ReadZipEntry(archive, "xl/styles.xml");
        Assert.Contains("fgColor rgb=\"FFFFFF00\"", stylesXml);
        Assert.Contains("<cellXfs count=\"3\">", stylesXml);

        var firstSheetXml = ReadZipEntry(archive, "xl/worksheets/sheet1.xml");
        Assert.Contains("<autoFilter ref=\"A1:Y9\"", firstSheetXml);
        Assert.Contains("<sortState ref=\"A2:Y9\"", firstSheetXml);
        Assert.Contains("<sortCondition ref=\"M2:M9\" />", firstSheetXml);
        Assert.Contains("<sortCondition ref=\"L2:L9\" />", firstSheetXml);
        Assert.Contains("<ignoredError sqref=\"A2:Y9\" numberStoredAsText=\"1\" />", firstSheetXml);
        Assert.Contains("<c r=\"P1\" s=\"2\" t=\"inlineStr\"><is><t>Groups</t></is></c>", firstSheetXml);
        Assert.Contains("<c r=\"A2\"><v>", firstSheetXml);
        Assert.DoesNotContain("<c r=\"A2\" t=\"inlineStr\">", firstSheetXml);
        Assert.Contains("<t>StartNumber</t>", firstSheetXml);
        Assert.Contains("<t>SiusDataStartNumber</t>", firstSheetXml);

        var secondSheetXml = ReadZipEntry(archive, "xl/worksheets/sheet2.xml");
        Assert.Contains("<autoFilter ref=\"A1:W2\"", secondSheetXml);
        Assert.Contains("<sortState ref=\"A2:W2\"", secondSheetXml);
        Assert.Contains("<ignoredError sqref=\"A2:W2\" numberStoredAsText=\"1\" />", secondSheetXml);
    }

    [Fact]
    public void Run_xlsx_exports_nm_junior_group_with_sius_rank_internal_name()
    {
        using var database = TempInrxDatabase.Create();
        using var output = TempDirectory.Create();
        AddNmJuniorFripistolStarter(database.Path);

        var result = SiusRankXlsxExportRunner.Run(new SiusRankCsvExportOptions(
            DatabasePath: database.Path,
            StevneId: 410,
            StevneIds: [],
            EventDate: null,
            EventName: null,
            OvelseId: 18,
            OvelseName: null,
            ShooterGroupsTemplatePath: null,
            OutputDirectory: output.Path,
            EncodingName: CsvEncoding.Utf8Bom));

        using var archive = ZipFile.OpenRead(result.OutputPath);
        var sheetXml = ReadZipEntry(archive, "xl/worksheets/sheet1.xml");

        Assert.Contains("<t>JrNM</t>", sheetXml);
        Assert.DoesNotContain("<t>Jr-NM</t>", sheetXml);
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
                VALUES (9, 'Finpistol', 'Fin', 10),
                       (11, 'Silhuett', 'Sil', 8),
                       (18, 'Fripistol', 'Fri', 2);

                INSERT INTO Klubb (Id, navn, kortnavn)
                VALUES (1, 'Kristiansand Pistolskyttere', 'KPS');

                INSERT INTO Klasse (Id, navn)
                VALUES (1, 'A');

                INSERT INTO Mklasse (Id, navn, sort)
                VALUES (1, 'Å', 1),
                       (2, 'V55', 2),
                       (3, 'Jm', 3),
                       (4, 'V65', 4),
                       (5, 'V73', 5),
                       (6, 'SH1', 6);

                INSERT INTO StartLag (Id, nr, dato)
                VALUES (1, 1, '2026-07-11 09:00:00');

                INSERT INTO Deltaker (Id, nsfId, medlemsnr, fnavn, enavn, foedselsaar, gender, land)
                VALUES (100, '900100', '', 'Anne', 'Aasen', '1980-01-01', 'K', 'NOR'),
                       (101, '900101', '', 'Bjarne', 'Berg', '1960-01-01', 'M', 'NOR'),
                       (102, '900102', '', 'Jan', 'Junior', '2005-01-01', 'M', 'NOR'),
                       (103, '900103', '', 'Vera', 'Vang', '1961-01-01', 'K', 'NOR'),
                       (104, '900104', '', 'Svein', 'Sund', '1955-01-01', 'M', 'NOR'),
                       (105, '900105', '', 'Per', 'Pedersen', '1949-01-01', 'M', 'NOR'),
                       (106, '900106', '', 'Siri', 'SH', '1977-01-01', 'K', 'NOR');

                INSERT INTO Resultat (
                    Id, StevneId, OvelseDefId, DeltakerId, KlubbId, KlasseId, MklasseId1, MklasseId2,
                    startLagId, standplass, skivenrFra, skivenrTil, kommentar)
                VALUES
                    (1000, 410, 9, 100, 1, 1, 1, NULL, 1, 1, '', '', ''),
                    (1001, 410, 9, 101, 1, 1, 2, NULL, 1, 2, '', '', ''),
                    (1002, 410, 11, 100, 1, 1, 1, NULL, 1, 2, '', '', ''),
                    (1003, 410, 11, 102, 1, 1, 3, NULL, 1, 4, '', '', ''),
                    (1004, 410, 11, 103, 1, 1, 2, NULL, 1, 7, '', '', ''),
                    (1005, 410, 11, 104, 1, 1, 4, NULL, 1, 9, '', '', ''),
                    (1006, 410, 11, 105, 1, 1, 5, NULL, 1, 12, '', '', ''),
                    (1007, 410, 18, 106, 1, 1, 6, NULL, 1, 1, '', '', '');
                """;
            command.ExecuteNonQuery();
        }
    }

    private static void AddSecondStevne(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Stevne (Id, navn, dato, ArrangementId)
            VALUES (411, '20260712 NM Standard 2026', '2026-07-12 09:00:00', 377);

            INSERT INTO OvelseDef (Id, navn, kortNavn, HovedOvelseId)
            VALUES (10, 'Standard', 'Std', 12);

            INSERT INTO Deltaker (Id, nsfId, medlemsnr, fnavn, enavn, foedselsaar, gender, land)
            VALUES (107, '900107', '', 'Stine', 'Standard', '1982-01-01', 'K', 'NOR');

            INSERT INTO Resultat (
                Id, StevneId, OvelseDefId, DeltakerId, KlubbId, KlasseId, MklasseId1, MklasseId2,
                startLagId, standplass, skivenrFra, skivenrTil, kommentar)
            VALUES (1008, 411, 10, 107, 1, 1, 1, NULL, 1, 3, '', '', '');
            """;
        command.ExecuteNonQuery();
    }

    private static void AddNmJuniorFripistolStarter(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Mklasse (Id, navn, sort)
            VALUES (7, 'Jr-NM', 7);

            INSERT INTO Deltaker (Id, nsfId, medlemsnr, fnavn, enavn, foedselsaar, gender, land)
            VALUES (108, '900108', '', 'Jonas', 'Juniornm', '2006-01-01', 'M', 'NOR');

            INSERT INTO Resultat (
                Id, StevneId, OvelseDefId, DeltakerId, KlubbId, KlasseId, MklasseId1, MklasseId2,
                startLagId, standplass, skivenrFra, skivenrTil, kommentar)
            VALUES (1009, 410, 18, 108, 1, 1, 7, NULL, 1, 3, '', '', '');
            """;
        command.ExecuteNonQuery();
    }

    private static string ReadZipEntry(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName) ?? throw new InvalidOperationException($"Missing zip entry {entryName}.");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
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
