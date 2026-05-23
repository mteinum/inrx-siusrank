using System.Text;
using Microsoft.Data.Sqlite;

namespace InrxToSiusRank.Tests;

public sealed class SiusRankWritebackRepositoryTests
{
    [Fact]
    public void Apply_writes_converted_result_fields_to_inrx_resultat()
    {
        using var db = TempDatabase.Create();
        var export = new SiusRankExportCompetition(
            "/tmp/HurtigFin_M.odf.xml",
            "SPRF_M",
            "HurtigFin_M",
            "25m Hurtigpistol Fin M",
            "IndividualResults",
            "INTERIM",
            DateTime.Now,
            [
                new SiusRankExportAthlete(
                    "26008",
                    "1273763",
                    "VRÅLSTAD",
                    "Tore",
                    "VRÅLSTAD Tore",
                    Result: 20,
                    InnerTens: 1,
                    [
                        new SiusRankExportShot(1, 10, 0.1m, 0.1m, "t1"),
                        new SiusRankExportShot(2, 10, 5m, 5m, "t2")
                    ])
            ]);
        var bibMap = new[]
        {
            new BibMapEntry("1273763", "26008", 198, "VRÅLSTAD Tore", "test")
        };

        IReadOnlyList<PlannedSiusRankWriteback> updates;
        using (var repository = new SiusRankWritebackRepository(db.Path))
        {
            var input = repository.GetInput([413]);
            var plans = SiusRankWritebackPlanner.Plan([export], input, bibMap, out var warnings);
            Assert.Empty(warnings);
            var plan = Assert.Single(plans);
            updates = plan.Updates;
            Assert.Single(updates);
        }

        SiusRankWritebackRepository.Apply(db.Path, updates);

        using var connection = new SqliteConnection($"Data Source={db.Path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                totsum,
                totinnertreff,
                serierDelOvelse1,
                delsumDelOvelse1,
                innertreffDelOvelse1,
                sumr1,
                ix1,
                statcomplete,
                statinit,
                oppdatertAv
            FROM Resultat
            WHERE Id = 1001;
            """;
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(20, reader.GetInt32(0));
        Assert.Equal(1, reader.GetInt32(1));
        Assert.Equal("OX;", reader.GetString(2));
        Assert.Equal("20;", reader.GetString(3));
        Assert.Equal("1;", reader.GetString(4));
        Assert.Equal(20, reader.GetInt32(5));
        Assert.Equal(1, reader.GetInt32(6));
        Assert.Equal(1, reader.GetInt32(7));
        Assert.Equal(0, reader.GetInt32(8));
        Assert.Equal("InrxToSiusRank", reader.GetString(9));
    }

    private sealed class TempDatabase : IDisposable
    {
        private TempDatabase(string path)
        {
            Path = path;
            CreateSchema();
            InsertRows();
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

            var resultColumns = new StringBuilder();
            for (var index = 1; index <= 8; index++)
            {
                resultColumns.AppendLine($", serierDelOvelse{index} TEXT DEFAULT ''");
                resultColumns.AppendLine($", delsumDelOvelse{index} TEXT DEFAULT ''");
                resultColumns.AppendLine($", innertreffDelOvelse{index} TEXT DEFAULT ''");
                resultColumns.AppendLine($", mlXyDelOvelse{index} TEXT DEFAULT ''");
                resultColumns.AppendLine($", sumDelOvelse{index} INTEGER DEFAULT 0");
            }

            for (var index = 1; index <= 16; index++)
            {
                resultColumns.AppendLine($", sumr{index} INTEGER DEFAULT 0");
                resultColumns.AppendLine($", ix{index} INTEGER DEFAULT 0");
            }

            command.CommandText =
                $"""
                CREATE TABLE Deltaker (
                    Id INTEGER PRIMARY KEY,
                    nsfId TEXT DEFAULT '',
                    medlemsnr TEXT DEFAULT '',
                    fnavn TEXT DEFAULT '',
                    enavn TEXT DEFAULT ''
                );

                CREATE TABLE OvelseDef (
                    Id INTEGER PRIMARY KEY,
                    navn TEXT DEFAULT '',
                    kortNavn TEXT DEFAULT '',
                    skuddpserie INTEGER DEFAULT 0,
                    seriePerRang INTEGER DEFAULT 0,
                    serierpdelovelse1 INTEGER DEFAULT 0,
                    serierpdelovelse2 INTEGER DEFAULT 0,
                    serierpdelovelse3 INTEGER DEFAULT 0,
                    serierpdelovelse4 INTEGER DEFAULT 0,
                    serierpdelovelse5 INTEGER DEFAULT 0,
                    serierpdelovelse6 INTEGER DEFAULT 0,
                    serierpdelovelse7 INTEGER DEFAULT 0,
                    serierpdelovelse8 INTEGER DEFAULT 0,
                    mlTarget INTEGER DEFAULT 0
                );

                CREATE TABLE Resultat (
                    Id INTEGER PRIMARY KEY,
                    StevneId INTEGER NOT NULL,
                    OvelseDefId INTEGER NOT NULL,
                    DeltakerId INTEGER NOT NULL,
                    totsum INTEGER DEFAULT 0,
                    totinnertreff INTEGER DEFAULT 0,
                    mlCal TEXT DEFAULT '',
                    mlTarget INTEGER DEFAULT 0,
                    mlIsMl INTEGER DEFAULT 0,
                    perTreffRangStr TEXT DEFAULT '',
                    statcomplete INTEGER DEFAULT 0,
                    statincomplete INTEGER DEFAULT 0,
                    statinit INTEGER DEFAULT 1,
                    statdnf INTEGER DEFAULT 0,
                    statdns INTEGER DEFAULT 0,
                    statdsq INTEGER DEFAULT 0,
                    delsumFinale TEXT DEFAULT '',
                    totFinale INTEGER DEFAULT 0,
                    delsumOmskyting TEXT DEFAULT '',
                    totOmskyting INTEGER DEFAULT 0,
                    delsumOmskytingDm TEXT DEFAULT '',
                    delsumOmskytingKm TEXT DEFAULT '',
                    delsumOmskytingNm TEXT DEFAULT '',
                    totOmskytingDm INTEGER DEFAULT 0,
                    totOmskytingKm INTEGER DEFAULT 0,
                    totOmskytingNm INTEGER DEFAULT 0,
                    oppdatert TEXT DEFAULT '',
                    oppdatertAv TEXT DEFAULT ''
                    {resultColumns}
                );
                """;
            command.ExecuteNonQuery();
        }

        private void InsertRows()
        {
            using var connection = new SqliteConnection($"Data Source={Path}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO Deltaker (Id, nsfId, fnavn, enavn)
                VALUES (198, '1273763', 'Tore', 'VRÅLSTAD');

                INSERT INTO OvelseDef (
                    Id, navn, kortNavn, skuddpserie, seriePerRang,
                    serierpdelovelse1, serierpdelovelse2, serierpdelovelse3, serierpdelovelse4,
                    serierpdelovelse5, serierpdelovelse6, serierpdelovelse7, serierpdelovelse8,
                    mlTarget)
                VALUES (7, 'Hurtig Fin', 'HFin', 2, 1, 1, 0, 0, 0, 0, 0, 0, 0, 100);

                INSERT INTO Resultat (Id, StevneId, OvelseDefId, DeltakerId)
                VALUES (1001, 413, 7, 198);
                """;
            command.ExecuteNonQuery();
        }
    }
}
