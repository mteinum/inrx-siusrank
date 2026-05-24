using System.Data;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace InrxToSiusRank;

public sealed record Nm2026TimetableOptions(
    string DatabasePath,
    string SiusRankPath,
    string ProposalPath,
    string OutputDirectory,
    bool Apply);

public sealed record Nm2026TimetableResult(
    bool Applied,
    string? InrxBackupPath,
    string? SiusRankBackupPath,
    IReadOnlyList<string> Messages,
    BulkExportResult? ExportResult);

public static class Nm2026TimetableCommand
{
    public const string Name = "apply-nm2026-timetable";

    public static bool IsCommand(IReadOnlyList<string> args) =>
        args.Count > 0 && args[0].Equals(Name, StringComparison.OrdinalIgnoreCase);

    public static Nm2026TimetableOptions Parse(IReadOnlyList<string> args)
    {
        string? settingsPath = null;
        string? databasePath = null;
        string? siusRankPath = null;
        string? proposalPath = null;
        string? outputDirectory = null;
        var apply = false;

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--settings":
                    settingsPath = ReadValue(args, ref index, arg);
                    break;
                case "--db":
                    databasePath = ReadValue(args, ref index, arg);
                    break;
                case "--siusrank":
                    siusRankPath = ReadValue(args, ref index, arg);
                    break;
                case "--proposal":
                    proposalPath = ReadValue(args, ref index, arg);
                    break;
                case "--output-dir":
                    outputDirectory = ReadValue(args, ref index, arg);
                    break;
                case "--apply":
                    apply = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option for {Name}: {arg}");
            }
        }

        var settings = AppSettings.Load(settingsPath);
        var resolvedDatabasePath = !string.IsNullOrWhiteSpace(databasePath)
            ? databasePath
            : settings.ResolveDatabasePath();
        RequireFile(resolvedDatabasePath, "Database file");
        RequireFile(siusRankPath, "SIUS Rank database");
        RequireFile(proposalPath, "Proposal workbook");
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Use --output-dir to choose where CSV files should be written.");
        }

        return new Nm2026TimetableOptions(
            resolvedDatabasePath,
            siusRankPath!,
            proposalPath!,
            outputDirectory.Trim(),
            apply);
    }

    private static string ReadValue(IReadOnlyList<string> args, ref int index, string optionName)
    {
        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Option '{optionName}' requires a value.");
        }

        index++;
        return args[index];
    }

    private static void RequireFile(string? path, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new ArgumentException($"{label} does not exist: {path}");
        }
    }
}

public static class Nm2026TimetableRunner
{
    public static Nm2026TimetableResult Run(Nm2026TimetableOptions options)
    {
        var preview = Nm2026TimetablePlanner.BuildPreview(options.DatabasePath, options.SiusRankPath);
        if (!options.Apply)
        {
            return new Nm2026TimetableResult(
                Applied: false,
                InrxBackupPath: null,
                SiusRankBackupPath: null,
                preview,
                ExportResult: null);
        }

        var inrxBackup = CreateBackup(options.DatabasePath, "nm2026-timetable");
        var siusBackup = CreateBackup(options.SiusRankPath, "nm2026-timetable");
        var messages = new List<string>(preview)
        {
            $"Backup storage.db3: {inrxBackup}",
            $"Backup SIUS Rank: {siusBackup}"
        };

        Nm2026TimetableRepository.ApplyInrx(options.DatabasePath);
        var exportResult = BulkExportRunner.Run(new AppOptions(
            options.DatabasePath,
            StevneId: null,
            StevneIds: Nm2026TimetablePlan.StevneIds,
            EventDate: null,
            EventName: null,
            OvelseId: null,
            OvelseName: null,
            ShooterGroupsTemplatePath: null,
            OutputDirectory: options.OutputDirectory,
            EncodingName: CsvEncoding.Utf8Bom,
            Wizard: false));
        messages.Add($"CSV files regenerated: {exportResult.Files.Count}");

        Nm2026TimetableRepository.ApplySiusRank(options.SiusRankPath, options.DatabasePath, Path.Combine(options.OutputDirectory, ChampionshipStartNumbers.BibMapFileName));
        messages.Add("SIUS Rank relays, stages and assignments updated.");

        var reportMessage = TryRegenerateReport(options.ProposalPath);
        if (!string.IsNullOrWhiteSpace(reportMessage))
        {
            messages.Add(reportMessage);
        }

        return new Nm2026TimetableResult(
            Applied: true,
            InrxBackupPath: inrxBackup,
            SiusRankBackupPath: siusBackup,
            messages,
            exportResult);
    }

    private static string CreateBackup(string path, string suffix)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? Directory.GetCurrentDirectory();
        var backupPath = Path.Combine(
            directory,
            $"{Path.GetFileName(path)}.bak-{suffix}-{DateTime.Now:yyyyMMdd-HHmmss}");
        File.Copy(path, backupPath, overwrite: false);
        return backupPath;
    }

    private static string? TryRegenerateReport(string proposalPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(proposalPath));
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var script = Path.Combine(directory, "generate_status_report.py");
        if (!File.Exists(script))
        {
            return null;
        }

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "python3",
                ArgumentList = { script },
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            if (process is null)
            {
                return "HTML report was not regenerated: could not start python3.";
            }

            process.WaitForExit();
            return process.ExitCode == 0
                ? "HTML report regenerated."
                : $"HTML report was not regenerated: {process.StandardError.ReadToEnd().Trim()}";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return $"HTML report was not regenerated: {ex.Message}";
        }
    }
}

public static class Nm2026TimetableReporter
{
    public static void Print(Nm2026TimetableResult result)
    {
        Console.WriteLine(result.Applied
            ? "NM2026 timetable updates applied."
            : "NM2026 timetable update preview. Use --apply to write changes.");
        foreach (var message in result.Messages)
        {
            Console.WriteLine($"- {message}");
        }

        if (result.ExportResult is not null)
        {
            Console.WriteLine($"Output directory: {result.ExportResult.OutputDirectory}");
        }
    }
}

public static class Nm2026TimetablePlan
{
    public static readonly int[] StevneIds = [405, 406, 407, 408, 409, 410, 411];
    public static readonly int[] SilhouetteTargets = [2, 4, 7, 9, 12, 14, 17, 19, 22, 24, 27, 29, 32, 34];

    private static readonly IReadOnlyList<Nm2026EventSpec> Events =
    [
        new(405, 18, "Fripistol", new DateOnly(2026, 7, 6), ["09:00", "11:00", "13:00"]),
        new(406, 11, "Silhuett", new DateOnly(2026, 7, 7), ["09:00", "10:00", "11:00", "12:00", "13:00", "14:00"]),
        new(407, 10, "Standard", new DateOnly(2026, 7, 8), ["09:00", "10:45", "12:30", "14:15"]),
        new(408, 9, "Finpistol", new DateOnly(2026, 7, 9), ["08:30", "09:45", "11:00", "12:15"]),
        new(409, 8, "Grovpistol", new DateOnly(2026, 7, 9), ["14:00", "15:15", "16:30"]),
        new(410, 7, "Hurtig Fin", new DateOnly(2026, 7, 11), ["08:00", "09:05", "10:10", "11:15", "12:20"]),
        new(411, 6, "Hurtig Grov", new DateOnly(2026, 7, 11), ["13:35", "14:45", "15:55"])
    ];

    private static readonly Dictionary<int, IReadOnlyList<DateTime>> DuellStageTimesByStevneId = new()
    {
        [408] = BuildTimes(new DateOnly(2026, 7, 10), ["08:30", "09:30", "10:30", "11:30"]),
        [409] = BuildTimes(new DateOnly(2026, 7, 10), ["13:00", "14:00", "15:00"])
    };

    public static IReadOnlyList<Nm2026EventSpec> EventSpecs => Events;

    public static Nm2026EventSpec EventByStevneId(int stevneId) =>
        Events.Single(item => item.StevneId == stevneId);

    public static Nm2026EventSpec EventByOvelseDefId(int ovelseDefId) =>
        Events.Single(item => item.OvelseDefId == ovelseDefId);

    public static IReadOnlyList<DateTime>? TryGetDuellStageTimes(int stevneId) =>
        DuellStageTimesByStevneId.TryGetValue(stevneId, out var times) ? times : null;

    public static Nm2026SilhouetteSlot SilhouetteSlotForIndex(int index)
    {
        var target = SilhouetteTargets[index % SilhouetteTargets.Length];
        var standIndex = index % SilhouetteTargets.Length / 2 + 1;
        var filter = index % 2 == 0 ? "V" : "H";
        return new Nm2026SilhouetteSlot(target, filter, standIndex * 1000);
    }

    public static IReadOnlyList<PlannedTargetAssignment> PlanSequentialTargets(
        IReadOnlyList<int> resultatIds,
        IReadOnlyList<int> targets)
    {
        return resultatIds
            .Select((resultatId, index) => new PlannedTargetAssignment(
                resultatId,
                RelayNumber: index / targets.Count + 1,
                TargetNumber: targets[index % targets.Count]))
            .ToList();
    }

    private static IReadOnlyList<DateTime> BuildTimes(DateOnly date, IReadOnlyList<string> times) =>
        times
            .Select(time => date.ToDateTime(TimeOnly.ParseExact(time, "HH:mm", CultureInfo.InvariantCulture)))
            .ToList();
}

public sealed record Nm2026EventSpec(
    int StevneId,
    int OvelseDefId,
    string Name,
    DateOnly Date,
    IReadOnlyList<string> Times)
{
    public IReadOnlyList<DateTime> StartTimes { get; } = Times
        .Select(time => Date.ToDateTime(TimeOnly.ParseExact(time, "HH:mm", CultureInfo.InvariantCulture)))
        .ToList();
}

public sealed record Nm2026SilhouetteSlot(int TargetNumber, string ImportShotFilter, int SiusDataStartNumber);

public sealed record PlannedTargetAssignment(int ResultatId, int RelayNumber, int TargetNumber);

public static class Nm2026TimetablePlanner
{
    public static IReadOnlyList<string> BuildPreview(string databasePath, string siusRankPath)
    {
        using var connection = Open(databasePath, SqliteOpenMode.ReadOnly);
        var messages = new List<string>();
        foreach (var spec in Nm2026TimetablePlan.EventSpecs)
        {
            var current = CountCurrentRelays(connection, spec);
            messages.Add($"{spec.Name}: set {spec.StartTimes.Count} INRX startlag times ({current} currently configured).");
        }

        var silhouetteCount = CountStarters(connection, 406, 11);
        messages.Add($"Silhuett: repack {silhouetteCount} starters into 6 relays with side targets 2/4, 7/9, ..., 32/34.");
        var finCount = CountStarters(connection, 408, 9);
        messages.Add($"Finpistol: rebalance {finCount} starters over 4 relays with max 35 shooters per relay.");
        messages.Add($"SIUS Rank: patch relay/stage times and V/H import setup in {Path.GetFileName(siusRankPath)}.");
        return messages;
    }

    private static int CountCurrentRelays(SqliteConnection connection, Nm2026EventSpec spec)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM StartLag sl
            JOIN Stevne s ON s.ArrangementId = sl.ArrangementId
            JOIN OvelseDef od ON od.HovedOvelseId = sl.HovedOvelseId
            WHERE s.Id = $stevneId
              AND od.Id = $ovelseDefId;
            """;
        command.Parameters.AddWithValue("$stevneId", spec.StevneId);
        command.Parameters.AddWithValue("$ovelseDefId", spec.OvelseDefId);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static int CountStarters(SqliteConnection connection, int stevneId, int ovelseDefId)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM Resultat
            WHERE StevneId = $stevneId
              AND OvelseDefId = $ovelseDefId;
            """;
        command.Parameters.AddWithValue("$stevneId", stevneId);
        command.Parameters.AddWithValue("$ovelseDefId", ovelseDefId);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode
        }.ToString());
        connection.Open();
        return connection;
    }
}

public static class Nm2026TimetableRepository
{
    public static void ApplyInrx(string databasePath)
    {
        using var connection = Open(databasePath, SqliteOpenMode.ReadWrite);
        using var transaction = connection.BeginTransaction();

        foreach (var spec in Nm2026TimetablePlan.EventSpecs)
        {
            var eventRow = GetInrxEventRow(connection, transaction, spec);
            EnsureStartLags(connection, transaction, eventRow, spec.StartTimes);
        }

        RepackEvent(connection, transaction, Nm2026TimetablePlan.EventByStevneId(406), Nm2026TimetablePlan.SilhouetteTargets, deleteUnusedStartLags: true);
        RepackEvent(connection, transaction, Nm2026TimetablePlan.EventByStevneId(408), Enumerable.Range(1, 35).ToArray(), deleteUnusedStartLags: false);

        transaction.Commit();
    }

    public static void ApplySiusRank(string siusRankPath, string inrxPath, string bibMapPath)
    {
        var assignments = LoadInrxAssignments(inrxPath, bibMapPath);
        using var connection = Open(siusRankPath, SqliteOpenMode.ReadWrite);
        using var transaction = connection.BeginTransaction();

        foreach (var spec in Nm2026TimetablePlan.EventSpecs)
        {
            ApplySiusRankTimes(connection, transaction, spec);
        }

        ApplySiusAssignments(connection, transaction, assignments);
        transaction.Commit();
    }

    private static void EnsureStartLags(
        SqliteConnection connection,
        SqliteTransaction transaction,
        InrxEventRow eventRow,
        IReadOnlyList<DateTime> startTimes)
    {
        var existing = GetStartLagIds(connection, transaction, eventRow.ArrangementId, eventRow.HovedOvelseId);
        for (var index = 0; index < startTimes.Count; index++)
        {
            var relayNumber = index + 1;
            var date = FormatDateTime(startTimes[index]);
            if (existing.TryGetValue(relayNumber, out var startLagId))
            {
                using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText =
                    """
                    UPDATE StartLag
                    SET dato = $dato
                    WHERE Id = $id;
                    """;
                update.Parameters.AddWithValue("$dato", date);
                update.Parameters.AddWithValue("$id", startLagId);
                update.ExecuteNonQuery();
                continue;
            }

            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO StartLag (ArrangementId, HovedOvelseId, dato, nr)
                VALUES ($arrangementId, $hovedOvelseId, $dato, $nr);
                """;
            insert.Parameters.AddWithValue("$arrangementId", eventRow.ArrangementId);
            insert.Parameters.AddWithValue("$hovedOvelseId", eventRow.HovedOvelseId);
            insert.Parameters.AddWithValue("$dato", date);
            insert.Parameters.AddWithValue("$nr", relayNumber);
            insert.ExecuteNonQuery();
        }
    }

    private static void RepackEvent(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Nm2026EventSpec spec,
        IReadOnlyList<int> targets,
        bool deleteUnusedStartLags)
    {
        var eventRow = GetInrxEventRow(connection, transaction, spec);
        var startLagIds = GetStartLagIds(connection, transaction, eventRow.ArrangementId, eventRow.HovedOvelseId);
        var resultatIds = GetResultatIdsInCurrentOrder(connection, transaction, spec);
        var assignments = Nm2026TimetablePlan.PlanSequentialTargets(resultatIds, targets);

        foreach (var assignment in assignments)
        {
            if (!startLagIds.TryGetValue(assignment.RelayNumber, out var startLagId))
            {
                throw new InvalidOperationException($"{spec.Name} missing StartLag nr {assignment.RelayNumber}.");
            }

            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE Resultat
                SET startLagId = $startLagId,
                    standplass = $standplass,
                    skivenrFra = $skivenr,
                    skivenrTil = $skivenr
                WHERE Id = $resultatId;
                """;
            update.Parameters.AddWithValue("$startLagId", startLagId);
            update.Parameters.AddWithValue("$standplass", assignment.TargetNumber);
            update.Parameters.AddWithValue("$skivenr", assignment.TargetNumber.ToString(CultureInfo.InvariantCulture));
            update.Parameters.AddWithValue("$resultatId", assignment.ResultatId);
            update.ExecuteNonQuery();
        }

        if (deleteUnusedStartLags)
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
            delete.Parameters.AddWithValue("$arrangementId", eventRow.ArrangementId);
            delete.Parameters.AddWithValue("$hovedOvelseId", eventRow.HovedOvelseId);
            delete.Parameters.AddWithValue("$requiredRelayCount", spec.StartTimes.Count);
            delete.ExecuteNonQuery();
        }
    }

    private static IReadOnlyList<int> GetResultatIdsInCurrentOrder(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Nm2026EventSpec spec)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT r.Id
            FROM Resultat r
            LEFT JOIN StartLag sl ON sl.Id = r.startLagId
            WHERE r.StevneId = $stevneId
              AND r.OvelseDefId = $ovelseDefId
            ORDER BY
                COALESCE(sl.nr, 9999),
                COALESCE(NULLIF(r.standplass, 0), CAST(NULLIF(r.skivenrFra, '') AS INTEGER), 9999),
                r.Id;
            """;
        command.Parameters.AddWithValue("$stevneId", spec.StevneId);
        command.Parameters.AddWithValue("$ovelseDefId", spec.OvelseDefId);

        using var reader = command.ExecuteReader();
        var ids = new List<int>();
        while (reader.Read())
        {
            ids.Add(reader.GetInt32(0));
        }

        return ids;
    }

    private static InrxEventRow GetInrxEventRow(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Nm2026EventSpec spec)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                s.Id AS StevneId,
                s.ArrangementId AS ArrangementId,
                od.Id AS OvelseDefId,
                od.HovedOvelseId AS HovedOvelseId
            FROM Stevne s
            JOIN Resultat r ON r.StevneId = s.Id
            JOIN OvelseDef od ON od.Id = r.OvelseDefId
            WHERE s.Id = $stevneId
              AND od.Id = $ovelseDefId
            GROUP BY s.Id, s.ArrangementId, od.Id, od.HovedOvelseId;
            """;
        command.Parameters.AddWithValue("$stevneId", spec.StevneId);
        command.Parameters.AddWithValue("$ovelseDefId", spec.OvelseDefId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Could not find {spec.Name} in INRX database.");
        }

        return new InrxEventRow(
            reader.GetInt32(reader.GetOrdinal("StevneId")),
            reader.GetInt32(reader.GetOrdinal("ArrangementId")),
            reader.GetInt32(reader.GetOrdinal("OvelseDefId")),
            reader.GetInt32(reader.GetOrdinal("HovedOvelseId")));
    }

    private static IReadOnlyDictionary<int, int> GetStartLagIds(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int arrangementId,
        int hovedOvelseId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT Id, nr
            FROM StartLag
            WHERE ArrangementId = $arrangementId
              AND HovedOvelseId = $hovedOvelseId
            ORDER BY nr, Id;
            """;
        command.Parameters.AddWithValue("$arrangementId", arrangementId);
        command.Parameters.AddWithValue("$hovedOvelseId", hovedOvelseId);

        using var reader = command.ExecuteReader();
        var startLagIds = new Dictionary<int, int>();
        while (reader.Read())
        {
            startLagIds[reader.GetInt32(1)] = reader.GetInt32(0);
        }

        return startLagIds;
    }

    private static IReadOnlyDictionary<(int OvelseDefId, string BibNumber), InrxSiusAssignment> LoadInrxAssignments(
        string inrxPath,
        string bibMapPath)
    {
        var bibByDeltakerId = LoadBibMap(bibMapPath);
        using var connection = Open(inrxPath, SqliteOpenMode.ReadOnly);
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                r.OvelseDefId,
                r.DeltakerId,
                COALESCE(sl.nr, 1) AS RelayNumber,
                COALESCE(NULLIF(r.standplass, 0), CAST(NULLIF(r.skivenrFra, '') AS INTEGER), 0) AS TargetNumber
            FROM Resultat r
            LEFT JOIN StartLag sl ON sl.Id = r.startLagId
            WHERE r.StevneId IN (405, 406, 407, 408, 409, 410, 411);
            """;
        using var reader = command.ExecuteReader();
        var assignments = new Dictionary<(int, string), InrxSiusAssignment>();
        while (reader.Read())
        {
            var deltakerId = reader.GetInt32(reader.GetOrdinal("DeltakerId"));
            if (!bibByDeltakerId.TryGetValue(deltakerId, out var bibNumber))
            {
                continue;
            }

            var ovelseDefId = reader.GetInt32(reader.GetOrdinal("OvelseDefId"));
            var targetNumber = reader.GetInt32(reader.GetOrdinal("TargetNumber"));
            var relayNumber = reader.GetInt32(reader.GetOrdinal("RelayNumber"));
            var silhouetteSlot = ovelseDefId == 11
                ? SilhouetteSlotFromTarget(targetNumber)
                : null;
            assignments[(ovelseDefId, bibNumber)] = new InrxSiusAssignment(
                ovelseDefId,
                bibNumber,
                relayNumber - 1,
                targetNumber,
                silhouetteSlot?.ImportShotFilter ?? string.Empty,
                silhouetteSlot?.SiusDataStartNumber ?? 0);
        }

        return assignments;
    }

    private static IReadOnlyDictionary<int, string> LoadBibMap(string bibMapPath)
    {
        var result = new Dictionary<int, string>();
        foreach (var line in File.ReadLines(bibMapPath).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length < 3 ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var deltakerId))
            {
                continue;
            }

            result[deltakerId] = parts[1].Trim();
        }

        return result;
    }

    private static Nm2026SilhouetteSlot? SilhouetteSlotFromTarget(int targetNumber)
    {
        var index = Array.IndexOf(Nm2026TimetablePlan.SilhouetteTargets, targetNumber);
        return index < 0 ? null : Nm2026TimetablePlan.SilhouetteSlotForIndex(index);
    }

    private static void ApplySiusRankTimes(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Nm2026EventSpec spec)
    {
        var duellTimes = Nm2026TimetablePlan.TryGetDuellStageTimes(spec.StevneId);
        using var phases = connection.CreateCommand();
        phases.Transaction = transaction;
        phases.CommandText =
            """
            SELECT p.UniqueIdentifier
            FROM ShootEvent se
            JOIN Phase p ON p.xpoShootEvent = se.UniqueIdentifier
            WHERE p.PhaseType NOT IN (6, 7)
              AND $ovelseDefId = CASE
                  WHEN se.ShortName LIKE 'Fri_%' THEN 18
                  WHEN se.ShortName LIKE 'Silhuett_%' THEN 11
                  WHEN se.ShortName LIKE 'Standard_%' THEN 10
                  WHEN se.ShortName LIKE 'Fin_%' THEN 9
                  WHEN se.ShortName LIKE 'Grov_%' THEN 8
                  WHEN se.ShortName LIKE 'HurtigFin_%' THEN 7
                  WHEN se.ShortName LIKE 'HurtigGrov_%' THEN 6
                  ELSE 0
              END;
            """;
        phases.Parameters.AddWithValue("$ovelseDefId", spec.OvelseDefId);

        using var reader = phases.ExecuteReader();
        var phaseIds = new List<string>();
        while (reader.Read())
        {
            phaseIds.Add(reader.GetString(0));
        }

        foreach (var phaseId in phaseIds)
        {
            UpdatePhaseStartTime(connection, transaction, phaseId, spec.StartTimes[0]);
            for (var index = 0; index < spec.StartTimes.Count; index++)
            {
                UpdateRelayTime(connection, transaction, phaseId, index, spec.StartTimes[index]);
                UpdateStageTime(connection, transaction, phaseId, index, stageIndex: 0, spec.StartTimes[index]);
                if (duellTimes is not null && index < duellTimes.Count)
                {
                    UpdateStageTime(connection, transaction, phaseId, index, stageIndex: 1, duellTimes[index]);
                }
            }
        }
    }

    private static void UpdatePhaseStartTime(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string phaseId,
        DateTime startTime)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE Phase
            SET StartDateTime = $startDateTime
            WHERE UniqueIdentifier = $phaseId;
            """;
        command.Parameters.AddWithValue("$startDateTime", FormatDateTime(startTime));
        command.Parameters.AddWithValue("$phaseId", phaseId);
        command.ExecuteNonQuery();
    }

    private static void UpdateRelayTime(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string phaseId,
        int relayIndex,
        DateTime startTime)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE Relay
            SET StartDateTime = $startDateTime
            WHERE xpoPhase = $phaseId
              AND [Index] = $relayIndex;
            """;
        command.Parameters.AddWithValue("$startDateTime", FormatDateTime(startTime));
        command.Parameters.AddWithValue("$phaseId", phaseId);
        command.Parameters.AddWithValue("$relayIndex", relayIndex);
        command.ExecuteNonQuery();
    }

    private static void UpdateStageTime(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string phaseId,
        int relayIndex,
        int stageIndex,
        DateTime startTime)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE Stage
            SET StartDateTime = $startDateTime
            WHERE [Index] = $stageIndex
              AND xpoRelay IN (
                  SELECT UniqueIdentifier
                  FROM Relay
                  WHERE xpoPhase = $phaseId
                    AND [Index] = $relayIndex
              );
            """;
        command.Parameters.AddWithValue("$startDateTime", FormatDateTime(startTime));
        command.Parameters.AddWithValue("$phaseId", phaseId);
        command.Parameters.AddWithValue("$relayIndex", relayIndex);
        command.Parameters.AddWithValue("$stageIndex", stageIndex);
        command.ExecuteNonQuery();
    }

    private static void ApplySiusAssignments(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyDictionary<(int OvelseDefId, string BibNumber), InrxSiusAssignment> assignments)
    {
        using var shootEvents = connection.CreateCommand();
        shootEvents.Transaction = transaction;
        shootEvents.CommandText =
            """
            SELECT UniqueIdentifier, ShortName
            FROM ShootEvent
            WHERE ShortName LIKE 'Silhuett_%';
            """;
        using (var reader = shootEvents.ExecuteReader())
        {
            var silhouetteEventIds = new List<string>();
            while (reader.Read())
            {
                silhouetteEventIds.Add(reader.GetString(0));
            }

            foreach (var shootEventId in silhouetteEventIds)
            {
                using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText =
                    """
                    UPDATE ShootEvent
                    SET TwoShootersPerLane = 1
                    WHERE UniqueIdentifier = $shootEventId;
                    """;
                update.Parameters.AddWithValue("$shootEventId", shootEventId);
                update.ExecuteNonQuery();
            }
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                ea.UniqueIdentifier AS AssignmentId,
                se.ShortName AS ShortName,
                e.StartNumber AS StartNumber
            FROM EntryAssignment ea
            JOIN Entry e ON e.UniqueIdentifier = ea.xpoEntry
            JOIN Phase p ON p.UniqueIdentifier = ea.xpoPhase
            JOIN ShootEvent se ON se.UniqueIdentifier = p.xpoShootEvent
            WHERE p.PhaseType NOT IN (6, 7);
            """;

        using var assignmentReader = command.ExecuteReader();
        var updates = new List<(string AssignmentId, InrxSiusAssignment Assignment)>();
        while (assignmentReader.Read())
        {
            var ovelseDefId = ResolveOvelseDefIdFromShortName(assignmentReader.GetString(assignmentReader.GetOrdinal("ShortName")));
            var startNumber = Convert.ToString(assignmentReader["StartNumber"], CultureInfo.InvariantCulture) ?? string.Empty;
            if (ovelseDefId is null || !assignments.TryGetValue((ovelseDefId.Value, startNumber), out var assignment))
            {
                continue;
            }

            updates.Add((assignmentReader.GetString(assignmentReader.GetOrdinal("AssignmentId")), assignment));
        }

        foreach (var update in updates)
        {
            using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText =
                """
                UPDATE EntryAssignment
                SET RelayIndexDraft = $relayIndex,
                    RelayIndexOriginalDraw = $relayIndex,
                    TargetNumberOriginalDraw = $targetNumber,
                    TargetNumber = $targetNumber,
                    CompetitionBibNumber = $bibNumber,
                    StarterId = $bibNumber,
                    ImportShotFilter = $importShotFilter,
                    SiusDataStartNumber = $siusDataStartNumber
                WHERE UniqueIdentifier = $assignmentId;
                """;
            updateCommand.Parameters.AddWithValue("$relayIndex", update.Assignment.RelayIndex);
            updateCommand.Parameters.AddWithValue("$targetNumber", update.Assignment.TargetNumber);
            updateCommand.Parameters.AddWithValue("$bibNumber", update.Assignment.BibNumber);
            updateCommand.Parameters.AddWithValue("$importShotFilter", update.Assignment.ImportShotFilter);
            updateCommand.Parameters.AddWithValue("$siusDataStartNumber", update.Assignment.SiusDataStartNumber);
            updateCommand.Parameters.AddWithValue("$assignmentId", update.AssignmentId);
            updateCommand.ExecuteNonQuery();
        }
    }

    private static int? ResolveOvelseDefIdFromShortName(string shortName)
    {
        if (shortName.StartsWith("Fri_", StringComparison.OrdinalIgnoreCase)) return 18;
        if (shortName.StartsWith("Silhuett_", StringComparison.OrdinalIgnoreCase)) return 11;
        if (shortName.StartsWith("Standard_", StringComparison.OrdinalIgnoreCase)) return 10;
        if (shortName.StartsWith("Fin_", StringComparison.OrdinalIgnoreCase)) return 9;
        if (shortName.StartsWith("Grov_", StringComparison.OrdinalIgnoreCase)) return 8;
        if (shortName.StartsWith("HurtigFin_", StringComparison.OrdinalIgnoreCase)) return 7;
        if (shortName.StartsWith("HurtigGrov_", StringComparison.OrdinalIgnoreCase)) return 6;
        return null;
    }

    private static string FormatDateTime(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode
        }.ToString());
        connection.Open();
        return connection;
    }

    private sealed record InrxEventRow(int StevneId, int ArrangementId, int OvelseDefId, int HovedOvelseId);

    private sealed record InrxSiusAssignment(
        int OvelseDefId,
        string BibNumber,
        int RelayIndex,
        int TargetNumber,
        string ImportShotFilter,
        int SiusDataStartNumber);
}
