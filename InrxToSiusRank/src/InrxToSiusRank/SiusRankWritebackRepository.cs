using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace InrxToSiusRank;

public sealed class SiusRankWritebackRepository : IDisposable
{
    private readonly SqliteConnection _connection;

    public SiusRankWritebackRepository(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        };

        _connection = new SqliteConnection(builder.ToString());
        _connection.Open();
    }

    public InrxWritebackInput GetInput(IReadOnlyList<int> stevneIds)
    {
        var results = GetResults(stevneIds);
        var ovelser = GetOvelser(results.Select(result => result.OvelseDefId).Distinct().ToList());
        return new InrxWritebackInput(results, ovelser);
    }

    public static string CreateBackup(string databasePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath)) ?? Directory.GetCurrentDirectory();
        var fileName = Path.GetFileName(databasePath);
        var backupPath = Path.Combine(
            directory,
            $"{fileName}.bak-siusrank-writeback-{DateTime.Now:yyyyMMdd-HHmmss}");
        File.Copy(databasePath, backupPath, overwrite: false);
        return backupPath;
    }

    public static void Apply(string databasePath, IReadOnlyList<PlannedSiusRankWriteback> updates)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite
        };

        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        var columns = GetColumns(connection, "Resultat");
        using var transaction = connection.BeginTransaction();

        foreach (var update in updates)
        {
            ApplyUpdate(connection, transaction, columns, update);
        }

        transaction.Commit();
    }

    public void Dispose() => _connection.Dispose();

    private IReadOnlyList<InrxResultRow> GetResults(IReadOnlyList<int> stevneIds)
    {
        using var command = _connection.CreateCommand();
        var placeholders = AddInParameters(command, "$stevne", stevneIds);
        command.CommandText =
            $"""
            SELECT
                r.Id AS ResultatId,
                r.StevneId,
                r.OvelseDefId,
                r.DeltakerId,
                COALESCE(d.nsfId, '') AS NsfId,
                COALESCE(d.medlemsnr, '') AS Medlemsnummer,
                COALESCE(d.fnavn, '') AS FirstName,
                COALESCE(d.enavn, '') AS LastName,
                COALESCE(r.totsum, 0) AS ExistingTotal,
                COALESCE(r.totinnertreff, 0) AS ExistingInnerTens,
                COALESCE(r.serierDelOvelse1, '') AS Series1,
                COALESCE(r.serierDelOvelse2, '') AS Series2,
                COALESCE(r.serierDelOvelse3, '') AS Series3,
                COALESCE(r.serierDelOvelse4, '') AS Series4,
                COALESCE(r.serierDelOvelse5, '') AS Series5,
                COALESCE(r.serierDelOvelse6, '') AS Series6,
                COALESCE(r.serierDelOvelse7, '') AS Series7,
                COALESCE(r.serierDelOvelse8, '') AS Series8
            FROM Resultat r
            JOIN Deltaker d ON d.Id = r.DeltakerId
            WHERE r.StevneId IN ({placeholders})
            ORDER BY r.StevneId, r.OvelseDefId, r.Id;
            """;

        using var reader = command.ExecuteReader();
        var results = new List<InrxResultRow>();
        while (reader.Read())
        {
            results.Add(new InrxResultRow(
                GetInt(reader, "ResultatId"),
                GetInt(reader, "StevneId"),
                GetInt(reader, "OvelseDefId"),
                GetInt(reader, "DeltakerId"),
                GetString(reader, "NsfId"),
                GetString(reader, "Medlemsnummer"),
                GetString(reader, "FirstName"),
                GetString(reader, "LastName"),
                GetInt(reader, "ExistingTotal"),
                GetInt(reader, "ExistingInnerTens"),
                CountSeriesShots(reader)));
        }

        return results;
    }

    private IReadOnlyDictionary<int, InrxOvelseDefinition> GetOvelser(IReadOnlyList<int> ovelseIds)
    {
        if (ovelseIds.Count == 0)
        {
            return new Dictionary<int, InrxOvelseDefinition>();
        }

        using var command = _connection.CreateCommand();
        var placeholders = AddInParameters(command, "$ovelse", ovelseIds);
        command.CommandText =
            $"""
            SELECT
                Id,
                COALESCE(navn, '') AS Name,
                COALESCE(kortNavn, '') AS ShortName,
                skuddpserie,
                seriePerRang,
                serierpdelovelse1,
                serierpdelovelse2,
                serierpdelovelse3,
                serierpdelovelse4,
                serierpdelovelse5,
                serierpdelovelse6,
                serierpdelovelse7,
                serierpdelovelse8,
                mlTarget
            FROM OvelseDef
            WHERE Id IN ({placeholders});
            """;

        using var reader = command.ExecuteReader();
        var ovelser = new Dictionary<int, InrxOvelseDefinition>();
        while (reader.Read())
        {
            var id = GetInt(reader, "Id");
            ovelser[id] = new InrxOvelseDefinition(
                id,
                GetString(reader, "Name"),
                GetString(reader, "ShortName"),
                GetFlexibleInt(reader, "skuddpserie"),
                GetFlexibleInt(reader, "seriePerRang"),
                [
                    GetFlexibleInt(reader, "serierpdelovelse1"),
                    GetFlexibleInt(reader, "serierpdelovelse2"),
                    GetFlexibleInt(reader, "serierpdelovelse3"),
                    GetFlexibleInt(reader, "serierpdelovelse4"),
                    GetFlexibleInt(reader, "serierpdelovelse5"),
                    GetFlexibleInt(reader, "serierpdelovelse6"),
                    GetFlexibleInt(reader, "serierpdelovelse7"),
                    GetFlexibleInt(reader, "serierpdelovelse8")
                ],
                GetFlexibleInt(reader, "mlTarget"));
        }

        return ovelser;
    }

    private static void ApplyUpdate(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlySet<string> columns,
        PlannedSiusRankWriteback update)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        var values = BuildResultValues(update.Fields);
        var assignments = new List<string>();
        foreach (var (column, value) in values.Where(item => columns.Contains(item.Key)))
        {
            var parameterName = "$" + column;
            assignments.Add($"{column} = {parameterName}");
            command.Parameters.AddWithValue(parameterName, value ?? DBNull.Value);
        }

        if (assignments.Count == 0)
        {
            throw new InvalidOperationException("No writable Resultat columns were found.");
        }

        command.CommandText =
            $"""
            UPDATE Resultat
            SET {string.Join(", ", assignments)}
            WHERE Id = $resultatId;
            """;
        command.Parameters.AddWithValue("$resultatId", update.ResultatId);
        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException($"Could not update Resultat.Id={update.ResultatId}.");
        }
    }

    private static Dictionary<string, object?> BuildResultValues(InrxResultFields fields)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["mlCal"] = string.Empty,
            ["mlTarget"] = fields.MlTarget,
            ["mlIsMl"] = 1,
            ["totinnertreff"] = fields.InnerTens,
            ["totsum"] = fields.TotalScore,
            ["perTreffRangStr"] = fields.PerShotRanking,
            ["statcomplete"] = 1,
            ["statincomplete"] = 0,
            ["statinit"] = 0,
            ["statdnf"] = 0,
            ["statdns"] = 0,
            ["statdsq"] = 0,
            ["delsumFinale"] = string.Empty,
            ["totFinale"] = 0,
            ["delsumOmskytingDm"] = string.Empty,
            ["delsumOmskytingKm"] = string.Empty,
            ["delsumOmskytingNm"] = string.Empty,
            ["totOmskytingDm"] = 0,
            ["totOmskytingKm"] = 0,
            ["totOmskytingNm"] = 0,
            ["delsumOmskyting"] = string.Empty,
            ["totOmskyting"] = 0,
            ["oppdatert"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            ["oppdatertAv"] = "InrxToSiusRank"
        };

        for (var index = 1; index <= 8; index++)
        {
            values[$"serierDelOvelse{index}"] = fields.SeriesPerPart[index - 1];
            values[$"delsumDelOvelse{index}"] = fields.PartSumsText[index - 1];
            values[$"innertreffDelOvelse{index}"] = fields.InnerTensPerPart[index - 1];
            values[$"mlXyDelOvelse{index}"] = fields.XyPerPart[index - 1];
            values[$"sumDelOvelse{index}"] = fields.SumPerPart[index - 1];
        }

        for (var index = 1; index <= 16; index++)
        {
            values[$"sumr{index}"] = fields.SumRank[index - 1];
            values[$"ix{index}"] = fields.InnerRank[index - 1];
        }

        return values;
    }

    private static IReadOnlySet<string> GetColumns(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";
        using var reader = command.ExecuteReader();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            columns.Add(GetString(reader, "name"));
        }

        return columns;
    }

    private static string AddInParameters(SqliteCommand command, string prefix, IReadOnlyList<int> values)
    {
        var parameters = new List<string>();
        for (var index = 0; index < values.Count; index++)
        {
            var name = $"{prefix}{index}";
            command.Parameters.AddWithValue(name, values[index]);
            parameters.Add(name);
        }

        return string.Join(", ", parameters);
    }

    private static int CountSeriesShots(IDataRecord reader)
    {
        var count = 0;
        for (var index = 1; index <= 8; index++)
        {
            count += GetString(reader, $"Series{index}")
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Sum(item => item.Length);
        }

        return count;
    }

    private static int GetFlexibleInt(IDataRecord reader, string name)
    {
        var value = reader[name];
        if (value == DBNull.Value)
        {
            return 0;
        }

        return value switch
        {
            int intValue => intValue,
            long longValue => Convert.ToInt32(longValue, CultureInfo.InvariantCulture),
            double doubleValue => Convert.ToInt32(doubleValue, CultureInfo.InvariantCulture),
            decimal decimalValue => Convert.ToInt32(decimalValue, CultureInfo.InvariantCulture),
            _ => int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0
        };
    }

    private static int GetInt(IDataRecord reader, string name)
    {
        var value = reader[name];
        return value == DBNull.Value
            ? 0
            : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static string GetString(IDataRecord reader, string name)
    {
        var value = reader[name];
        return value == DBNull.Value
            ? string.Empty
            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
