using System.Globalization;
using System.Text;

namespace InrxToSiusRank;

public static class ChampionshipStartNumbers
{
    public const string BibMapFileName = "bib-map.csv";
    private const int MaxSequence = 9999;

    public static IReadOnlyDictionary<int, string> Create(
        IEnumerable<InrxStarter> starters,
        IEnumerable<StevneInfo> stevner)
    {
        return CreatePlan(starters, stevner, bibMapPath: null).StartNumbers;
    }

    public static IReadOnlyDictionary<int, string> Create(
        IEnumerable<InrxStarter> starters,
        IEnumerable<StevneInfo> stevner,
        string bibMapPath)
    {
        if (string.IsNullOrWhiteSpace(bibMapPath))
        {
            throw new ArgumentException("Bib map path cannot be empty.", nameof(bibMapPath));
        }

        var plan = CreatePlan(starters, stevner, bibMapPath);
        WriteBibMap(bibMapPath, plan.BibMapEntries);
        return plan.StartNumbers;
    }

    private static StartNumberPlan CreatePlan(
        IEnumerable<InrxStarter> starters,
        IEnumerable<StevneInfo> stevner,
        string? bibMapPath)
    {
        var starterList = starters.ToList();
        var stevneList = stevner.ToList();
        var yearPrefix = ResolveYearPrefix(stevneList);
        var shooters = ResolveShooters(starterList);

        if (shooters.Any(shooter => shooter.DeltakerId <= 0))
        {
            throw new InvalidOperationException("Cannot assign start numbers when one or more starters have no Deltaker.Id.");
        }

        if (shooters.Count > MaxSequence)
        {
            throw new InvalidOperationException(
                $"Cannot assign {shooters.Count} start numbers with year prefix {yearPrefix}. " +
                "SIUS start and bib numbers must be maximum 6 digits.");
        }

        var existingEntries = !string.IsNullOrWhiteSpace(bibMapPath) && File.Exists(bibMapPath)
            ? ReadBibMap(bibMapPath, yearPrefix)
            : [];
        var assignments = AssignNumbers(shooters, existingEntries, yearPrefix, out var assignmentSources);
        var bibMapEntries = string.IsNullOrWhiteSpace(bibMapPath)
            ? []
            : MergeBibMap(existingEntries, shooters, assignments, assignmentSources, yearPrefix);

        return new StartNumberPlan(assignments, bibMapEntries);
    }

    private static IReadOnlyList<ShooterInfo> ResolveShooters(IReadOnlyList<InrxStarter> starters)
    {
        return starters
            .GroupBy(starter => starter.DeltakerId)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var ordered = group.OrderBy(starter => starter.ResultatId).ToList();
                var nsfIds = ordered
                    .Select(starter => NormalizeNsfId(starter.NsfId))
                    .Where(value => value.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (nsfIds.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"Deltaker.Id={group.Key} has multiple NSF ids: {string.Join(", ", nsfIds)}.");
                }

                var starter = ordered[0];
                return new ShooterInfo(
                    group.Key,
                    nsfIds.FirstOrDefault() ?? string.Empty,
                    DisplayName(starter));
            })
            .ToList();
    }

    private static IReadOnlyDictionary<int, string> AssignNumbers(
        IReadOnlyList<ShooterInfo> shooters,
        IReadOnlyList<BibMapEntry> existingEntries,
        string yearPrefix,
        out IReadOnlyDictionary<int, string> assignmentSources)
    {
        var byDeltakerId = existingEntries
            .Where(entry => entry.DeltakerId > 0)
            .ToLookup(entry => entry.DeltakerId);
        var usedBibNumbers = existingEntries
            .Select(entry => entry.BibNumber)
            .ToHashSet(StringComparer.Ordinal);

        var assigned = new Dictionary<int, string>();
        var sources = new Dictionary<int, string>();
        var assignedBibOwners = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var shooter in shooters)
        {
            var matches = byDeltakerId[shooter.DeltakerId].ToList();
            var bibNumbers = matches
                .Select(entry => entry.BibNumber)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (bibNumbers.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Existing {BibMapFileName} has conflicting bib numbers for {shooter.Name} " +
                    $"(Deltaker.Id={shooter.DeltakerId}): {string.Join(", ", bibNumbers)}.");
            }

            if (bibNumbers.Count == 0)
            {
                continue;
            }

            var bibNumber = bibNumbers[0];
            if (assignedBibOwners.TryGetValue(bibNumber, out var owner) && owner != shooter.DeltakerId)
            {
                throw new InvalidOperationException(
                    $"Existing {BibMapFileName} maps bib {bibNumber} to multiple current shooters.");
            }

            assigned[shooter.DeltakerId] = bibNumber;
            assignedBibOwners[bibNumber] = shooter.DeltakerId;
            sources[shooter.DeltakerId] = FirstNonEmpty(matches.FirstOrDefault()?.Source, $"existing {BibMapFileName}");
        }

        var nextSequence = 1;
        foreach (var shooter in shooters.Where(shooter => !assigned.ContainsKey(shooter.DeltakerId)))
        {
            string bibNumber;
            do
            {
                if (nextSequence > MaxSequence)
                {
                    throw new InvalidOperationException(
                        $"Cannot assign more start numbers with year prefix {yearPrefix}. " +
                        "SIUS start and bib numbers must be maximum 6 digits.");
                }

                bibNumber = $"{yearPrefix}{nextSequence:D3}";
                nextSequence++;
            }
            while (usedBibNumbers.Contains(bibNumber));

            assigned[shooter.DeltakerId] = bibNumber;
            sources[shooter.DeltakerId] = "allocated by InrxToSiusRank";
            usedBibNumbers.Add(bibNumber);
        }

        assignmentSources = sources;
        return assigned;
    }

    private static IReadOnlyList<BibMapEntry> MergeBibMap(
        IReadOnlyList<BibMapEntry> existingEntries,
        IReadOnlyList<ShooterInfo> shooters,
        IReadOnlyDictionary<int, string> assignments,
        IReadOnlyDictionary<int, string> assignmentSources,
        string yearPrefix)
    {
        var shooterByDeltaker = shooters.ToDictionary(shooter => shooter.DeltakerId);
        var preserved = existingEntries
            .Where(entry => !shooterByDeltaker.ContainsKey(entry.DeltakerId))
            .ToList();

        var current = shooters.Select(shooter => new BibMapEntry(
            shooter.NsfId,
            assignments[shooter.DeltakerId],
            shooter.DeltakerId,
            shooter.Name,
            assignmentSources.TryGetValue(shooter.DeltakerId, out var source)
                ? source
                : "allocated by InrxToSiusRank"));

        return preserved
            .Concat(current)
            .OrderBy(entry => ParseBibSequence(entry.BibNumber, yearPrefix, BibMapFileName))
            .ThenBy(entry => entry.DeltakerId)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<BibMapEntry> ReadBibMap(string bibMapPath, string yearPrefix)
    {
        var lines = File.ReadAllLines(bibMapPath, Encoding.UTF8);
        if (lines.Length == 0)
        {
            return [];
        }

        var header = SplitCsvLine(lines[0])
            .Select((name, index) => new { Name = name.Trim().TrimStart('\uFEFF'), Index = index })
            .ToDictionary(item => item.Name, item => item.Index, StringComparer.OrdinalIgnoreCase);

        var entries = new List<BibMapEntry>();
        foreach (var line in lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            var fields = SplitCsvLine(line);
            var bibNumber = GetField(fields, header, "bibNumber").Trim();
            if (string.IsNullOrWhiteSpace(bibNumber))
            {
                continue;
            }

            ParseBibSequence(bibNumber, yearPrefix, bibMapPath);
            entries.Add(new BibMapEntry(
                NormalizeNsfId(GetField(fields, header, "nsfId")),
                bibNumber,
                int.TryParse(GetField(fields, header, "deltakerId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var deltakerId)
                    ? deltakerId
                    : 0,
                GetField(fields, header, "name").Trim(),
                GetField(fields, header, "source").Trim()));
        }

        return entries;
    }

    private static void WriteBibMap(string bibMapPath, IReadOnlyList<BibMapEntry> entries)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(bibMapPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new StringBuilder();
        builder.Append("nsfId,bibNumber,deltakerId,name,source").Append("\r\n");
        foreach (var entry in entries)
        {
            builder
                .AppendJoin(',', new[]
                {
                    entry.NsfId,
                    entry.BibNumber,
                    entry.DeltakerId.ToString(CultureInfo.InvariantCulture),
                    entry.Name,
                    entry.Source
                }.Select(EscapeCsvField))
                .Append("\r\n");
        }

        File.WriteAllText(bibMapPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static int ParseBibSequence(string bibNumber, string yearPrefix, string context)
    {
        if (bibNumber.Length > 6 ||
            bibNumber.Length <= yearPrefix.Length ||
            !bibNumber.StartsWith(yearPrefix, StringComparison.Ordinal) ||
            bibNumber.Any(ch => ch < '0' || ch > '9') ||
            !int.TryParse(bibNumber[yearPrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) ||
            sequence <= 0 ||
            sequence > MaxSequence)
        {
            throw new InvalidOperationException(
                $"{context} contains invalid bibNumber '{bibNumber}'. Expected {yearPrefix}nnn, digits only, maximum 6 digits.");
        }

        return sequence;
    }

    private static string DisplayName(InrxStarter starter)
    {
        var lastName = starter.LastName.Trim().ToUpperInvariant();
        var firstName = starter.FirstName.Trim();
        return string.IsNullOrWhiteSpace(firstName)
            ? lastName
            : $"{lastName} {firstName}";
    }

    private static string NormalizeNsfId(string value) => value.Trim();

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string GetField(
        IReadOnlyList<string> fields,
        IReadOnlyDictionary<string, int> header,
        string name)
    {
        return header.TryGetValue(name, out var index) && index >= 0 && index < fields.Count
            ? fields[index]
            : string.Empty;
    }

    private static string EscapeCsvField(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static IReadOnlyList<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var ch = line[index];
            if (inQuotes)
            {
                if (ch == '"' && index + 1 < line.Length && line[index + 1] == '"')
                {
                    builder.Append('"');
                    index++;
                }
                else if (ch == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    builder.Append(ch);
                }
            }
            else if (ch == ',')
            {
                fields.Add(builder.ToString());
                builder.Clear();
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else
            {
                builder.Append(ch);
            }
        }

        fields.Add(builder.ToString());
        return fields;
    }

    private static string ResolveYearPrefix(IReadOnlyList<StevneInfo> stevner)
    {
        var years = stevner
            .Select(ResolveYear)
            .Distinct()
            .Order()
            .ToList();

        return years.Count switch
        {
            0 => throw new InvalidOperationException("Cannot assign start numbers without a stevne date."),
            1 => (years[0] % 100).ToString("D2", CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException(
                "Cannot assign shared championship start numbers across multiple calendar years: " +
                string.Join(", ", years))
        };
    }

    private static int ResolveYear(StevneInfo stevne)
    {
        var trimmed = stevne.Date.Trim();
        if (trimmed.Length >= 4 &&
            int.TryParse(trimmed[..4], NumberStyles.None, CultureInfo.InvariantCulture, out var prefixYear))
        {
            return prefixYear;
        }

        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed.Year;
        }

        throw new InvalidOperationException(
            $"Cannot assign start number year prefix from Stevne.Id={stevne.Id} date '{stevne.Date}'.");
    }

    private sealed record StartNumberPlan(
        IReadOnlyDictionary<int, string> StartNumbers,
        IReadOnlyList<BibMapEntry> BibMapEntries);

    private sealed record ShooterInfo(int DeltakerId, string NsfId, string Name);

    private sealed record BibMapEntry(string NsfId, string BibNumber, int DeltakerId, string Name, string Source);
}
