using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace InrxToSiusRank;

public sealed class SeedStartLagRepository : IDisposable
{
    private static readonly int[] SilhouetteTargets = [2, 4, 7, 9, 12, 14, 17, 19, 22, 24, 27, 29, 32, 34];
    private static readonly HashSet<int> TwentyFiveMeterOvelseIds = [6, 7, 8, 9, 10];
    private static readonly int[] TwentyFiveMeterCompetitionTargets = Enumerable.Range(1, 35).ToArray();
    private readonly SqliteConnection _connection;

    public SeedStartLagRepository(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        };

        _connection = new SqliteConnection(builder.ToString());
        _connection.Open();
    }

    public IReadOnlyList<SeedStartLagEventInput> GetEventInputs(IReadOnlyList<int> stevneIds)
    {
        var inputs = new List<SeedStartLagEventInput>();
        foreach (var stevneId in stevneIds)
        {
            inputs.Add(GetEventInput(stevneId));
        }

        return inputs;
    }

    public static string CreateBackup(string databasePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath)) ?? Directory.GetCurrentDirectory();
        var fileName = Path.GetFileName(databasePath);
        var backupPath = Path.Combine(
            directory,
            $"{fileName}.bak-seed-{DateTime.Now:yyyyMMdd-HHmmss}");
        File.Copy(databasePath, backupPath, overwrite: false);
        return backupPath;
    }

    public static void Apply(string databasePath, IReadOnlyList<SeedStartLagEventPlan> plans)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite
        };

        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var transaction = connection.BeginTransaction();

        foreach (var plan in plans)
        {
            var startLagIds = EnsureStartLags(connection, transaction, plan);
            UpdateAssignments(connection, transaction, plan, startLagIds);
            DeleteUnusedStartLags(connection, transaction, plan);
        }

        transaction.Commit();
    }

    public void Dispose() => _connection.Dispose();

    private SeedStartLagEventInput GetEventInput(int stevneId)
    {
        var eventRows = GetEventRows(stevneId);
        if (eventRows.Count == 0)
        {
            throw new InvalidOperationException($"Could not find NM event with starters for Stevne.Id={stevneId}.");
        }

        if (eventRows.Count > 1)
        {
            throw new InvalidOperationException(
                $"Stevne.Id={stevneId} has multiple exercises. seed-startlag expects one exercise per NM event.");
        }

        var eventRow = eventRows[0];
        var stevne = new StevneInfo(
            eventRow.StevneId,
            eventRow.StevneName,
            eventRow.StevneDate,
            eventRow.ArrangementId);
        var ovelse = new OvelseInfo(
            eventRow.OvelseDefId,
            eventRow.OvelseName,
            eventRow.OvelseShortName,
            eventRow.HovedOvelseId);
        var startLags = GetStartLags(eventRow.ArrangementId, eventRow.HovedOvelseId);
        var shooters = GetShooters(stevneId, eventRow.OvelseDefId);
        var targets = ResolveTargets(ovelse, eventRow.BaneStandpl, shooters);
        var relayInterval = ResolveRelayInterval(startLags);

        return new SeedStartLagEventInput(
            stevne,
            ovelse,
            NmDisciplineMap.Resolve(ovelse),
            targets,
            relayInterval,
            startLags,
            shooters);
    }

    private IReadOnlyList<EventRow> GetEventRows(int stevneId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                s.Id AS StevneId,
                s.navn AS StevneName,
                s.dato AS StevneDate,
                s.ArrangementId AS ArrangementId,
                od.Id AS OvelseDefId,
                od.navn AS OvelseName,
                od.kortNavn AS OvelseShortName,
                od.HovedOvelseId AS HovedOvelseId,
                COALESCE(MAX(b.standpl), 0) AS BaneStandpl
            FROM Stevne s
            JOIN Resultat r ON r.StevneId = s.Id
            JOIN OvelseDef od ON od.Id = r.OvelseDefId
            JOIN HovedOvelse ho ON ho.Id = od.HovedOvelseId
            LEFT JOIN Bane b ON b.BaneLokId = s.BaneLokId AND b.BaneTypeId = ho.BaneTypeId
            WHERE s.Id = $stevneId
            GROUP BY s.Id, s.navn, s.dato, s.ArrangementId, od.Id, od.navn, od.kortNavn, od.HovedOvelseId
            ORDER BY od.Id;
            """;
        command.Parameters.AddWithValue("$stevneId", stevneId);

        using var reader = command.ExecuteReader();
        var rows = new List<EventRow>();
        while (reader.Read())
        {
            rows.Add(new EventRow(
                GetInt(reader, "StevneId"),
                GetString(reader, "StevneName"),
                GetString(reader, "StevneDate"),
                GetInt(reader, "ArrangementId"),
                GetInt(reader, "OvelseDefId"),
                GetString(reader, "OvelseName"),
                GetString(reader, "OvelseShortName"),
                GetInt(reader, "HovedOvelseId"),
                GetInt(reader, "BaneStandpl")));
        }

        return rows;
    }

    private IReadOnlyList<StartLagInfo> GetStartLags(int arrangementId, int hovedOvelseId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, nr, dato
            FROM StartLag
            WHERE ArrangementId = $arrangementId
              AND HovedOvelseId = $hovedOvelseId
            ORDER BY nr, Id;
            """;
        command.Parameters.AddWithValue("$arrangementId", arrangementId);
        command.Parameters.AddWithValue("$hovedOvelseId", hovedOvelseId);

        using var reader = command.ExecuteReader();
        var startLags = new List<StartLagInfo>();
        while (reader.Read())
        {
            startLags.Add(new StartLagInfo(
                GetInt(reader, "Id"),
                GetInt(reader, "nr"),
                GetString(reader, "dato")));
        }

        return startLags;
    }

    private IReadOnlyList<SeedStartLagShooter> GetShooters(int stevneId, int ovelseDefId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                r.Id AS ResultatId,
                TRIM(COALESCE(d.fnavn, '') || ' ' || COALESCE(d.enavn, '')) AS DisplayName,
                COALESCE(kl.kortnavn, '') AS ClubShortName,
                COALESCE(d.sa2Id, '') AS Sa2Id,
                COALESCE(NULLIF(m.navn, ''), '-') AS KmNmClass,
                sl.nr AS OldRelay,
                COALESCE(NULLIF(r.standplass, 0), CAST(NULLIF(r.skivenrFra, '') AS INTEGER), 0) AS OldTarget
            FROM Resultat r
            JOIN Deltaker d ON d.Id = r.DeltakerId
            JOIN Klubb kl ON kl.Id = r.KlubbId
            LEFT JOIN Mklasse m ON m.Id = r.MklasseId1
            LEFT JOIN StartLag sl ON sl.Id = r.startLagId
            WHERE r.StevneId = $stevneId
              AND r.OvelseDefId = $ovelseDefId
            ORDER BY
                COALESCE(sl.nr, 9999),
                COALESCE(NULLIF(r.standplass, 0), CAST(NULLIF(r.skivenrFra, '') AS INTEGER), 9999),
                r.Id;
            """;
        command.Parameters.AddWithValue("$stevneId", stevneId);
        command.Parameters.AddWithValue("$ovelseDefId", ovelseDefId);

        using var reader = command.ExecuteReader();
        var shooters = new List<SeedStartLagShooter>();
        while (reader.Read())
        {
            shooters.Add(new SeedStartLagShooter(
                GetInt(reader, "ResultatId"),
                GetString(reader, "DisplayName"),
                GetString(reader, "ClubShortName"),
                GetString(reader, "Sa2Id"),
                GetString(reader, "KmNmClass"),
                GetNullableInt(reader, "OldRelay"),
                GetInt(reader, "OldTarget")));
        }

        return shooters;
    }

    private static IReadOnlyList<int> ResolveTargets(
        OvelseInfo ovelse,
        int baneStandpl,
        IReadOnlyList<SeedStartLagShooter> shooters)
    {
        if (ovelse.Id == 11 || ovelse.Name.Equals("Silhuett", StringComparison.OrdinalIgnoreCase))
        {
            return SilhouetteTargets;
        }

        if (TwentyFiveMeterOvelseIds.Contains(ovelse.Id))
        {
            return TwentyFiveMeterCompetitionTargets;
        }

        var targetCount = baneStandpl > 0
            ? baneStandpl
            : shooters.Select(shooter => shooter.OldTarget).DefaultIfEmpty(0).Max();
        if (targetCount <= 0)
        {
            throw new InvalidOperationException($"Could not resolve target count for {ovelse.Name}.");
        }

        return Enumerable.Range(1, targetCount).ToList();
    }

    private static TimeSpan ResolveRelayInterval(IReadOnlyList<StartLagInfo> startLags)
    {
        var parsedDates = startLags
            .OrderBy(startLag => startLag.Nr)
            .Select(startLag => TryParseDate(startLag.Date))
            .Where(date => date is not null)
            .Select(date => date!.Value)
            .ToList();
        if (parsedDates.Count < 2)
        {
            return TimeSpan.FromHours(1);
        }

        var interval = parsedDates[^1] - parsedDates[^2];
        return interval > TimeSpan.Zero ? interval : TimeSpan.FromHours(1);
    }

    private static IReadOnlyDictionary<int, int> EnsureStartLags(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SeedStartLagEventPlan plan)
    {
        var startLagIds = plan.ExistingStartLags.ToDictionary(startLag => startLag.Nr, startLag => startLag.Id);
        var lastExisting = plan.ExistingStartLags
            .OrderBy(startLag => startLag.Nr)
            .LastOrDefault();
        var lastExistingNr = lastExisting?.Nr ?? 0;
        var lastExistingDate = TryParseDate(lastExisting?.Date) ??
            TryParseDate(plan.Stevne.Date) ??
            DateTime.Now;

        for (var relayNumber = 1; relayNumber <= plan.RequiredRelayCount; relayNumber++)
        {
            if (startLagIds.ContainsKey(relayNumber))
            {
                continue;
            }

            var relayDate = lastExistingDate + TimeSpan.FromTicks(plan.RelayInterval.Ticks * Math.Max(1, relayNumber - lastExistingNr));
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO StartLag (ArrangementId, HovedOvelseId, dato, nr)
                VALUES ($arrangementId, $hovedOvelseId, $dato, $nr);
                SELECT last_insert_rowid();
                """;
            insert.Parameters.AddWithValue("$arrangementId", plan.Stevne.ArrangementId);
            insert.Parameters.AddWithValue("$hovedOvelseId", plan.Ovelse.HovedOvelseId);
            insert.Parameters.AddWithValue("$dato", relayDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            insert.Parameters.AddWithValue("$nr", relayNumber);
            var insertedId = Convert.ToInt32(insert.ExecuteScalar(), CultureInfo.InvariantCulture);
            startLagIds[relayNumber] = insertedId;
        }

        return startLagIds;
    }

    private static void UpdateAssignments(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SeedStartLagEventPlan plan,
        IReadOnlyDictionary<int, int> startLagIds)
    {
        foreach (var assignment in plan.Assignments)
        {
            if (!startLagIds.TryGetValue(assignment.RelayNumber, out var startLagId))
            {
                throw new InvalidOperationException($"Missing StartLag for relay {assignment.RelayNumber}.");
            }

            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE Resultat
                SET startLagId = $startLagId,
                    standplass = $standplass,
                    skivenrFra = $skivenrFra,
                    skivenrTil = $skivenrTil
                WHERE Id = $resultatId;
                """;
            update.Parameters.AddWithValue("$startLagId", startLagId);
            update.Parameters.AddWithValue("$standplass", assignment.TargetNumber);
            update.Parameters.AddWithValue("$skivenrFra", assignment.TargetNumber.ToString(CultureInfo.InvariantCulture));
            update.Parameters.AddWithValue("$skivenrTil", assignment.TargetNumber.ToString(CultureInfo.InvariantCulture));
            update.Parameters.AddWithValue("$resultatId", assignment.ResultatId);
            if (update.ExecuteNonQuery() != 1)
            {
                throw new InvalidOperationException($"Could not update Resultat.Id={assignment.ResultatId}.");
            }
        }
    }

    private static void DeleteUnusedStartLags(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SeedStartLagEventPlan plan)
    {
        using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText =
            """
            DELETE FROM StartLag
            WHERE ArrangementId = $arrangementId
              AND HovedOvelseId = $hovedOvelseId
              AND nr > $requiredRelayCount
              AND NOT EXISTS (
                  SELECT 1
                  FROM Resultat r
                  WHERE r.startLagId = StartLag.Id
              );
            """;
        delete.Parameters.AddWithValue("$arrangementId", plan.Stevne.ArrangementId);
        delete.Parameters.AddWithValue("$hovedOvelseId", plan.Ovelse.HovedOvelseId);
        delete.Parameters.AddWithValue("$requiredRelayCount", plan.RequiredRelayCount);
        delete.ExecuteNonQuery();
    }

    private static DateTime? TryParseDate(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : null;

    private static int GetInt(IDataRecord reader, string name)
    {
        var value = reader[name];
        return value == DBNull.Value
            ? 0
            : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static int? GetNullableInt(IDataRecord reader, string name)
    {
        var value = reader[name];
        return value == DBNull.Value
            ? null
            : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static string GetString(IDataRecord reader, string name)
    {
        var value = reader[name];
        return value == DBNull.Value
            ? string.Empty
            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private sealed record EventRow(
        int StevneId,
        string StevneName,
        string StevneDate,
        int ArrangementId,
        int OvelseDefId,
        string OvelseName,
        string OvelseShortName,
        int HovedOvelseId,
        int BaneStandpl);
}
