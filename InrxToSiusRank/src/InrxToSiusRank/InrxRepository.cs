using System.Data;
using Microsoft.Data.Sqlite;

namespace InrxToSiusRank;

public sealed class InrxRepository : IDisposable
{
    private readonly SqliteConnection _connection;

    public InrxRepository(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        };

        _connection = new SqliteConnection(builder.ToString());
        _connection.Open();
    }

    public IReadOnlyList<StevneInfo> SearchStevner(string? filter, int limit = 100)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            SELECT s.Id, s.navn, s.dato, s.ArrangementId
            FROM Stevne s
            WHERE EXISTS (SELECT 1 FROM Resultat r WHERE r.StevneId = s.Id)
              AND ($filter IS NULL OR s.navn LIKE $filter OR s.dato LIKE $filter)
            ORDER BY s.dato DESC, s.Id DESC
            LIMIT $limit;
            """;
        command.Parameters.Add(new SqliteParameter("$filter",
            string.IsNullOrWhiteSpace(filter) ? DBNull.Value : $"%{filter.Trim()}%"));
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = command.ExecuteReader();
        var stevner = new List<StevneInfo>();
        while (reader.Read())
        {
            stevner.Add(ReadStevne(reader));
        }

        return stevner;
    }

    public IReadOnlyList<OvelseSummary> GetOvelserForStevne(int stevneId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                od.Id,
                od.navn,
                od.kortNavn,
                od.HovedOvelseId,
                COUNT(r.Id) AS StarterCount
            FROM Resultat r
            JOIN OvelseDef od ON od.Id = r.OvelseDefId
            WHERE r.StevneId = $stevneId
            GROUP BY od.Id, od.navn, od.kortNavn, od.HovedOvelseId
            ORDER BY od.navn;
            """;
        command.Parameters.AddWithValue("$stevneId", stevneId);

        using var reader = command.ExecuteReader();
        var ovelser = new List<OvelseSummary>();
        while (reader.Read())
        {
            ovelser.Add(new OvelseSummary(
                GetInt(reader, "Id"),
                GetString(reader, "navn"),
                GetString(reader, "kortNavn"),
                GetInt(reader, "HovedOvelseId"),
                GetInt(reader, "StarterCount")));
        }

        return ovelser;
    }

    public StevneInfo GetStevneById(int stevneId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, navn, dato, ArrangementId
            FROM Stevne
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", stevneId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Could not find Stevne.Id={stevneId}.");
        }

        return ReadStevne(reader);
    }

    public IReadOnlyList<KmNmClassSummary> GetKmNmClasses(int stevneId, int ovelseDefId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                COALESCE(NULLIF(m.navn, ''), '-') AS Name,
                COUNT(r.Id) AS StarterCount,
                COALESCE(group_concat(DISTINCT sl.nr), '') AS Relays,
                MIN(COALESCE(m.sort, 9999)) AS ClassSort
            FROM Resultat r
            LEFT JOIN Mklasse m ON m.Id = r.MklasseId1
            LEFT JOIN StartLag sl ON sl.Id = r.startLagId
            WHERE r.StevneId = $stevneId
              AND r.OvelseDefId = $ovelseDefId
            GROUP BY COALESCE(NULLIF(m.navn, ''), '-')
            ORDER BY ClassSort, Name;
            """;
        command.Parameters.AddWithValue("$stevneId", stevneId);
        command.Parameters.AddWithValue("$ovelseDefId", ovelseDefId);

        using var reader = command.ExecuteReader();
        var classes = new List<KmNmClassSummary>();
        while (reader.Read())
        {
            classes.Add(new KmNmClassSummary(
                GetString(reader, "Name"),
                GetInt(reader, "StarterCount"),
                SortCsvNumbers(GetString(reader, "Relays"))));
        }

        return classes;
    }

    public StevneInfo ResolveStevne(AppOptions options)
    {
        if (options.StevneId is not null)
        {
            return GetStevneById(options.StevneId.Value);
        }

        if (options.EventDate is null)
        {
            throw new ArgumentException("Use --stevne-id or --event-date to select an event.");
        }

        using var searchCommand = _connection.CreateCommand();
        searchCommand.CommandText =
            """
            SELECT Id, navn, dato, ArrangementId
            FROM Stevne
            WHERE date(dato) = $eventDate
              AND ($eventName IS NULL OR navn LIKE $eventName)
            ORDER BY dato, Id;
            """;
        searchCommand.Parameters.AddWithValue("$eventDate", options.EventDate.Value.ToString("yyyy-MM-dd"));
        searchCommand.Parameters.Add(
            new SqliteParameter("$eventName", options.EventName is null ? DBNull.Value : $"%{options.EventName}%"));

        using var searchReader = searchCommand.ExecuteReader();
        var matches = new List<StevneInfo>();
        while (searchReader.Read())
        {
            matches.Add(ReadStevne(searchReader));
        }

        return matches.Count switch
        {
            0 => throw new InvalidOperationException("Could not find an event matching the supplied criteria."),
            1 => matches[0],
            _ => throw new InvalidOperationException(
                "Multiple events matched. Use --stevne-id. Matches: " +
                string.Join(", ", matches.Select(match => $"{match.Id}:{match.Name}")))
        };
    }

    public OvelseInfo ResolveOvelse(AppOptions options)
    {
        if (options.OvelseId is not null)
        {
            using var command = _connection.CreateCommand();
            command.CommandText =
                """
                SELECT Id, navn, kortNavn, HovedOvelseId
                FROM OvelseDef
                WHERE Id = $id;
                """;
            command.Parameters.AddWithValue("$id", options.OvelseId.Value);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException($"Could not find OvelseDef.Id={options.OvelseId.Value}.");
            }

            return ReadOvelse(reader);
        }

        using var searchCommand = _connection.CreateCommand();
        searchCommand.CommandText =
            """
            SELECT Id, navn, kortNavn, HovedOvelseId
            FROM OvelseDef
            WHERE navn = $name COLLATE NOCASE
               OR kortNavn = $name COLLATE NOCASE
            ORDER BY Id;
            """;
        searchCommand.Parameters.AddWithValue("$name", options.OvelseName);

        using var searchReader = searchCommand.ExecuteReader();
        var matches = new List<OvelseInfo>();
        while (searchReader.Read())
        {
            matches.Add(ReadOvelse(searchReader));
        }

        return matches.Count switch
        {
            0 => throw new InvalidOperationException($"Could not find exercise '{options.OvelseName}'."),
            1 => matches[0],
            _ => throw new InvalidOperationException(
                "Multiple exercises matched. Use --ovelse-id. Matches: " +
                string.Join(", ", matches.Select(match => $"{match.Id}:{match.Name}")))
        };
    }

    public IReadOnlyList<InrxStarter> GetStarters(int stevneId, int ovelseDefId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                r.Id              AS StartNumber,
                d.Id              AS DeltakerId,
                r.standplass      AS TargetNumber,
                r.skivenrFra      AS SkiveFra,
                r.skivenrTil      AS SkiveTil,
                sl.nr             AS Relay,
                sl.dato           AS RelayDate,
                d.medlemsnr       AS AccreditationNumber,
                d.fnavn           AS FirstName,
                d.enavn           AS LastName,
                d.foedselsaar     AS BirthDay,
                d.gender          AS Gender,
                d.land            AS Land,
                kl.navn           AS ClubName,
                kl.kortnavn       AS ClubShortName,
                k.navn            AS InrxClass,
                km.navn           AS KmNmClass,
                dm.navn           AS DmClass,
                od.navn           AS OvelseName,
                s.navn            AS StevneName
            FROM Resultat r
            JOIN Stevne s       ON s.Id = r.StevneId
            JOIN OvelseDef od   ON od.Id = r.OvelseDefId
            JOIN Deltaker d     ON d.Id = r.DeltakerId
            JOIN Klubb kl       ON kl.Id = r.KlubbId
            JOIN Klasse k       ON k.Id = r.KlasseId
            LEFT JOIN Mklasse km ON km.Id = r.MklasseId1
            LEFT JOIN Mklasse dm ON dm.Id = r.MklasseId2
            LEFT JOIN StartLag sl ON sl.Id = r.startLagId
            WHERE r.StevneId = $stevneId
              AND r.OvelseDefId = $ovelseDefId
            ORDER BY
                COALESCE(sl.nr, 0),
                COALESCE(NULLIF(r.standplass, 0), CAST(NULLIF(r.skivenrFra, '') AS INTEGER), 9999),
                d.enavn,
                d.fnavn;
            """;
        command.Parameters.AddWithValue("$stevneId", stevneId);
        command.Parameters.AddWithValue("$ovelseDefId", ovelseDefId);

        using var reader = command.ExecuteReader();
        var starters = new List<InrxStarter>();
        while (reader.Read())
        {
            starters.Add(new InrxStarter(
                ResultatId: GetInt(reader, "StartNumber"),
                DeltakerId: GetInt(reader, "DeltakerId"),
                Standplass: GetInt(reader, "TargetNumber"),
                SkivenrFra: GetString(reader, "SkiveFra"),
                SkivenrTil: GetString(reader, "SkiveTil"),
                Relay: GetNullableInt(reader, "Relay"),
                RelayDate: GetString(reader, "RelayDate"),
                AccreditationNumber: GetString(reader, "AccreditationNumber"),
                FirstName: GetString(reader, "FirstName"),
                LastName: GetString(reader, "LastName"),
                BirthDay: GetString(reader, "BirthDay"),
                Gender: GetString(reader, "Gender"),
                Land: GetString(reader, "Land"),
                ClubName: GetString(reader, "ClubName"),
                ClubShortName: GetString(reader, "ClubShortName"),
                InrxClass: GetString(reader, "InrxClass"),
                KmNmClass: GetString(reader, "KmNmClass"),
                DmClass: GetString(reader, "DmClass"),
                OvelseName: GetString(reader, "OvelseName"),
                StevneName: GetString(reader, "StevneName")));
        }

        return starters;
    }

    public void Dispose() => _connection.Dispose();

    private static StevneInfo ReadStevne(IDataRecord reader) =>
        new(
            GetInt(reader, "Id"),
            GetString(reader, "navn"),
            GetString(reader, "dato"),
            GetInt(reader, "ArrangementId"));

    private static OvelseInfo ReadOvelse(IDataRecord reader) =>
        new(
            GetInt(reader, "Id"),
            GetString(reader, "navn"),
            GetString(reader, "kortNavn"),
            GetInt(reader, "HovedOvelseId"));

    private static int GetInt(IDataRecord reader, string name)
    {
        var value = reader[name];
        if (value == DBNull.Value)
        {
            return 0;
        }

        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int? GetNullableInt(IDataRecord reader, string name)
    {
        var value = reader[name];
        if (value == DBNull.Value)
        {
            return null;
        }

        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string GetString(IDataRecord reader, string name)
    {
        var value = reader[name];
        return value == DBNull.Value
            ? string.Empty
            : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string SortCsvNumbers(string value)
    {
        var numbers = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var number) ? number : (int?)null)
            .Where(number => number is not null)
            .Select(number => number!.Value)
            .Distinct()
            .OrderBy(number => number)
            .Select(number => number.ToString(System.Globalization.CultureInfo.InvariantCulture));

        return string.Join(", ", numbers);
    }
}
