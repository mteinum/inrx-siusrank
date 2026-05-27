using System.Globalization;
using System.Text;

namespace InrxToSiusRank;

public sealed record SiusDataStartListExportOptions(
    string DatabasePath,
    string SiusDataDirectory,
    IReadOnlyList<int> StevneIds,
    int? OvelseId,
    string? OvelseName,
    string OutputDirectory,
    string EncodingName);

public sealed record SiusDataStartListExportResult(
    string OutputDirectory,
    string BibMapPath,
    int StartListRows,
    int MatchedRows,
    int UnmatchedRows,
    IReadOnlyList<BulkExportFileResult> Files,
    IReadOnlyList<string> Warnings);

public sealed record SiusDataStartListRow(
    string SourcePath,
    int LineNumber,
    string StartNumber,
    string LastName,
    string FirstName,
    string DisplayName,
    string Nation,
    string Club,
    int? TargetNumber,
    int? Relay,
    string StartTime);

public static class SiusDataStartListExporter
{
    public static SiusDataStartListExportResult Run(SiusDataStartListExportOptions options)
    {
        if (!File.Exists(options.DatabasePath))
        {
            throw new ArgumentException($"Database file does not exist: {options.DatabasePath}");
        }

        if (!Directory.Exists(options.SiusDataDirectory))
        {
            throw new ArgumentException($"SIUS Data directory does not exist: {options.SiusDataDirectory}");
        }

        if (options.StevneIds.Count == 0)
        {
            throw new ArgumentException("Use at least one Stevne.Id.");
        }

        var outputDirectory = Path.GetFullPath(options.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var startListRows = SiusDataStartListReader.ReadDirectory(options.SiusDataDirectory);
        if (startListRows.Count == 0)
        {
            throw new InvalidOperationException(
                $"No SIUS Data start list rows found in {Path.GetFullPath(options.SiusDataDirectory)}.");
        }

        using var repository = new InrxRepository(options.DatabasePath);
        var eventExports = ResolveEventExports(repository, options);
        var allStarters = eventExports.SelectMany(item => item.Starters).ToList();
        var warnings = new List<string>();
        var matched = MatchStartListRows(startListRows, allStarters, eventExports.Select(item => item.Stevne), warnings);
        if (matched.Count == 0)
        {
            throw new InvalidOperationException(
                "No SIUS Data start list rows matched inrX starters. " +
                "Check that the selected Stevne.Id/Ovelse and SIUS Data folder belong to the same event.");
        }

        var matchedByResultatId = matched.ToDictionary(item => item.Starter.ResultatId);
        var results = new List<BulkExportFileResult>();
        foreach (var eventExport in eventExports)
        {
            var selectedStarters = eventExport.Starters
                .Where(starter => matchedByResultatId.ContainsKey(starter.ResultatId))
                .ToList();
            if (selectedStarters.Count == 0)
            {
                continue;
            }

            var classGroups = selectedStarters
                .GroupBy(starter => EffectiveKmNmClass.Resolve(starter, eventExport.Ovelse))
                .OrderBy(group => EffectiveKmNmClass.SortKey(group.Key))
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var classGroup in classGroups)
            {
                var rows = classGroup
                    .Select(starter =>
                    {
                        var matchedStart = matchedByResultatId[starter.ResultatId];
                        var startList = matchedStart.StartList;
                        var assignedStarter = starter with
                        {
                            AccreditationNumber = startList.StartNumber,
                            KmNmClass = classGroup.Key,
                            Standplass = startList.TargetNumber ?? starter.Standplass,
                            Relay = startList.Relay ?? starter.Relay
                        };
                        return StarterMapper.Map(
                            assignedStarter,
                            siusGroupOverride: null,
                            includeClubTeam: true,
                            startNumber: matchedStart.ImportStartNumber);
                    })
                    .ToList();

                var outputPath = Path.Combine(
                    outputDirectory,
                    OutputFileName.ForImport(eventExport.Stevne, eventExport.Ovelse, classGroup.Key));
                SiusRankCsvWriter.Write(outputPath, rows, options.EncodingName);
                results.Add(new BulkExportFileResult(
                    eventExport.Stevne,
                    eventExport.Ovelse,
                    classGroup.Key,
                    rows.Count,
                    outputPath,
                    ExportValidator.Validate(rows).ToList()));
            }
        }

        var bibMapPath = Path.Combine(outputDirectory, ChampionshipStartNumbers.BibMapFileName);
        WriteBibMap(bibMapPath, matched);

        return new SiusDataStartListExportResult(
            outputDirectory,
            bibMapPath,
            startListRows.Count,
            matched.Count,
            startListRows.Count - matched.Count,
            results,
            warnings);
    }

    public static IReadOnlyList<SiusDataMatchedStart> MatchStartListRows(
        IReadOnlyList<SiusDataStartListRow> startListRows,
        IReadOnlyList<InrxStarter> starters,
        IEnumerable<StevneInfo> stevner,
        List<string>? warnings = null)
    {
        var remaining = starters.ToList();
        var matched = new List<SiusDataMatchedStart>();
        var seenStartNumbers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var startList in startListRows)
        {
            if (!seenStartNumbers.Add(startList.StartNumber))
            {
                warnings?.Add($"{DisplaySource(startList)}: startnummer {startList.StartNumber} finnes flere ganger; hopper over duplikat.");
                continue;
            }

            var candidates = remaining
                .Where(starter => IsNameMatch(startList, starter))
                .ToList();
            candidates = PreferByTarget(startList, candidates);
            candidates = PreferByClub(startList, candidates);

            if (candidates.Count == 0)
            {
                warnings?.Add($"{DisplaySource(startList)}: fant ikke inrX-starter for {startList.DisplayName} / start {startList.StartNumber}.");
                continue;
            }

            if (candidates.Count > 1)
            {
                warnings?.Add($"{DisplaySource(startList)}: flere mulige inrX-startere for {startList.DisplayName}; hopper over.");
                continue;
            }

            var starter = candidates[0];
            remaining.RemoveAll(item => item.ResultatId == starter.ResultatId);
            matched.Add(new SiusDataMatchedStart(startList, starter, ImportStartNumber: string.Empty));
        }

        return AssignImportStartNumbers(matched, stevner, warnings);
    }

    private static IReadOnlyList<SiusDataMatchedStart> AssignImportStartNumbers(
        IReadOnlyList<SiusDataMatchedStart> matched,
        IEnumerable<StevneInfo> stevner,
        List<string>? warnings)
    {
        var yearPrefix = ResolveYearPrefix(stevner);
        var used = new HashSet<string>(StringComparer.Ordinal);
        var nextSequence = 1;
        var result = new List<SiusDataMatchedStart>();

        foreach (var item in matched
                     .OrderBy(item => item.StartList.Relay ?? int.MaxValue)
                     .ThenBy(item => item.StartList.TargetNumber ?? int.MaxValue)
                     .ThenBy(item => item.Starter.LastName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Starter.FirstName, StringComparer.OrdinalIgnoreCase))
        {
            var importStartNumber = IsValidSiusRankStartNumber(item.StartList.StartNumber) &&
                                    used.Add(item.StartList.StartNumber)
                ? item.StartList.StartNumber
                : AllocateStartNumber(yearPrefix, used, ref nextSequence);

            if (!string.Equals(importStartNumber, item.StartList.StartNumber, StringComparison.Ordinal))
            {
                warnings?.Add(
                    $"{DisplaySource(item.StartList)}: SIUS Data-id {item.StartList.StartNumber} kan ikke brukes som SIUS Rank StartNumber. " +
                    $"Bruker {importStartNumber} og beholder {item.StartList.StartNumber} som AccreditationNumber.");
            }

            result.Add(item with { ImportStartNumber = importStartNumber });
        }

        return result;
    }

    private static bool IsValidSiusRankStartNumber(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length is > 0 and <= 6 && trimmed.All(ch => ch is >= '0' and <= '9');
    }

    private static string AllocateStartNumber(string yearPrefix, HashSet<string> used, ref int nextSequence)
    {
        string value;
        do
        {
            if (nextSequence > 9999)
            {
                throw new InvalidOperationException("Cannot allocate more SIUS Rank start numbers.");
            }

            value = $"{yearPrefix}{nextSequence:D3}";
            nextSequence++;
        }
        while (!used.Add(value));

        return value;
    }

    private static string ResolveYearPrefix(IEnumerable<StevneInfo> stevner)
    {
        var year = stevner
            .Select(stevne => DateTime.TryParse(
                stevne.Date,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed)
                    ? parsed.Year
                    : 0)
            .Where(value => value > 0)
            .DefaultIfEmpty(DateTime.Now.Year)
            .Min();
        return (year % 100).ToString("D2", CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<EventExport> ResolveEventExports(InrxRepository repository, SiusDataStartListExportOptions options)
    {
        return options.StevneIds
            .Select(repository.GetStevneById)
            .SelectMany(stevne =>
            {
                var ovelser = ResolveOvelser(repository, options, stevne);
                return ovelser.Select(ovelse =>
                {
                    var starters = repository.GetStarters(stevne.Id, ovelse.Id);
                    if (starters.Count == 0)
                    {
                        throw new InvalidOperationException(
                            $"No starters found for Stevne.Id={stevne.Id} and OvelseDef.Id={ovelse.Id}.");
                    }

                    return new EventExport(stevne, ovelse, starters);
                });
            })
            .ToList();
    }

    private static IReadOnlyList<OvelseInfo> ResolveOvelser(
        InrxRepository repository,
        SiusDataStartListExportOptions options,
        StevneInfo stevne)
    {
        if (options.OvelseId is not null || !string.IsNullOrWhiteSpace(options.OvelseName))
        {
            return [repository.ResolveOvelse(new AppOptions(
                options.DatabasePath,
                StevneId: stevne.Id,
                StevneIds: [],
                EventDate: null,
                EventName: null,
                options.OvelseId,
                options.OvelseName,
                ShooterGroupsTemplatePath: null,
                OutputDirectory: options.OutputDirectory,
                options.EncodingName,
                Wizard: false))];
        }

        return repository.GetOvelserForStevne(stevne.Id)
            .Select(ovelse => new OvelseInfo(ovelse.Id, ovelse.Name, ovelse.ShortName, ovelse.HovedOvelseId))
            .ToList();
    }

    private static List<InrxStarter> PreferByTarget(SiusDataStartListRow startList, List<InrxStarter> candidates)
    {
        if (startList.TargetNumber is null || candidates.Count <= 1)
        {
            return candidates;
        }

        var targetMatches = candidates
            .Where(starter => starter.Standplass == startList.TargetNumber.Value ||
                              int.TryParse(starter.SkivenrFra, NumberStyles.Integer, CultureInfo.InvariantCulture, out var skiveFra) &&
                              skiveFra == startList.TargetNumber.Value)
            .ToList();
        return targetMatches.Count == 0 ? candidates : targetMatches;
    }

    private static List<InrxStarter> PreferByClub(SiusDataStartListRow startList, List<InrxStarter> candidates)
    {
        if (string.IsNullOrWhiteSpace(startList.Club) || candidates.Count <= 1)
        {
            return candidates;
        }

        var club = NormalizeToken(startList.Club);
        var clubMatches = candidates
            .Where(starter => NormalizeToken(starter.ClubShortName) == club ||
                              NormalizeToken(starter.ClubName) == club)
            .ToList();
        return clubMatches.Count == 0 ? candidates : clubMatches;
    }

    private static bool IsNameMatch(SiusDataStartListRow startList, InrxStarter starter)
    {
        var siusLast = NormalizeToken(startList.LastName);
        var inrxLast = NormalizeToken(starter.LastName);
        if (siusLast.Length == 0 || inrxLast.Length == 0 || siusLast != inrxLast)
        {
            return false;
        }

        var siusFirst = NormalizeToken(startList.FirstName);
        var inrxFirst = NormalizeToken(starter.FirstName);
        return siusFirst.Length == 0 ||
               inrxFirst.Length == 0 ||
               siusFirst == inrxFirst ||
               siusFirst.StartsWith(inrxFirst, StringComparison.Ordinal) ||
               inrxFirst.StartsWith(siusFirst, StringComparison.Ordinal);
    }

    private static string NormalizeToken(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToUpperInvariant(ch) switch
                {
                    'Æ' => "AE",
                    'Ø' => "O",
                    'Å' => "A",
                    _ => ch.ToString().ToUpperInvariant()
                });
            }
        }

        return builder.ToString();
    }

    private static string DisplaySource(SiusDataStartListRow row) =>
        $"{Path.GetFileName(row.SourcePath)}:{row.LineNumber}";

    private static void WriteBibMap(string bibMapPath, IReadOnlyList<SiusDataMatchedStart> matched)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(bibMapPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new StringBuilder();
        builder.Append("nsfId,bibNumber,deltakerId,name,source").Append("\r\n");
        foreach (var item in matched
                     .OrderBy(item => item.ImportStartNumber, StringComparer.Ordinal)
                     .ThenBy(item => item.Starter.DeltakerId))
        {
            var name = $"{item.Starter.LastName} {item.Starter.FirstName}".Trim();
            builder.AppendJoin(',', new[]
            {
                item.Starter.NsfId.Trim(),
                item.ImportStartNumber,
                item.Starter.DeltakerId.ToString(CultureInfo.InvariantCulture),
                name,
                $"SIUS Data startliste: {Path.GetFileName(item.StartList.SourcePath)}"
            }.Select(value => DelimitedText.Escape(value, ',')));
            builder.Append("\r\n");
        }

        File.WriteAllText(bibMapPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private sealed record EventExport(
        StevneInfo Stevne,
        OvelseInfo Ovelse,
        IReadOnlyList<InrxStarter> Starters);
}

public sealed record SiusDataMatchedStart(SiusDataStartListRow StartList, InrxStarter Starter, string ImportStartNumber);

public static class SiusDataStartListReader
{
    public static IReadOnlyList<SiusDataStartListRow> ReadDirectory(string directory)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Directory
            .EnumerateFiles(directory, "*_stl.csv", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .SelectMany(ReadFile)
            .ToList();
    }

    public static IReadOnlyList<SiusDataStartListRow> ReadFile(string path)
    {
        var rows = new List<SiusDataStartListRow>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path, Encoding.GetEncoding(1252)))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = DelimitedText.SplitLine(line, ';');
            if (!LooksLikeStartListRow(fields))
            {
                continue;
            }

            rows.Add(new SiusDataStartListRow(
                Path.GetFullPath(path),
                lineNumber,
                fields[1].Trim(),
                fields[2].Trim(),
                fields[3].Trim(),
                fields[4].Trim(),
                fields[5].Trim(),
                fields[8].Trim(),
                ParseTargetNumber(fields),
                ResolveRelay(path, fields),
                fields[12].Trim()));
        }

        return rows;
    }

    private static bool LooksLikeStartListRow(IReadOnlyList<string> fields) =>
        fields.Count >= 17 &&
        string.IsNullOrWhiteSpace(fields[0]) &&
        IsInteger(fields[1]) &&
        !string.IsNullOrWhiteSpace(fields[2]) &&
        !string.IsNullOrWhiteSpace(fields[3]) &&
        ParseTargetNumber(fields) is not null;

    private static int? ParseTargetNumber(IReadOnlyList<string> fields)
    {
        foreach (var index in new[] { 9, 10 })
        {
            if (index < fields.Count && ParseNullableInt(fields[index]) is { } target)
            {
                return target;
            }
        }

        return null;
    }

    private static int? ResolveRelay(string path, IReadOnlyList<string> fields)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty);
        while (directory is not null)
        {
            var name = directory.Name.Trim();
            if (name.StartsWith("Relay ", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(name["Relay ".Length..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var relayFromFolder))
            {
                return relayFromFolder;
            }

            directory = directory.Parent;
        }

        return ParseNullableInt(fields[11]);
    }

    private static int? ParseNullableInt(string value) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static bool IsInteger(string value) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
}

public static class SiusDataStartListCommand
{
    public const string Name = "export-siusdata-startlist";
    public const string Alias = "export-siusdata";
    public const string DefaultSiusDataDirectory = @"C:\SIUS\SiusData\Data";

    public static bool IsCommand(IReadOnlyList<string> args) =>
        args.Count > 0 &&
        (args[0].Equals(Name, StringComparison.OrdinalIgnoreCase) ||
         args[0].Equals(Alias, StringComparison.OrdinalIgnoreCase));

    public static SiusDataStartListExportOptions Parse(IReadOnlyList<string> args)
    {
        string? settingsPath = null;
        string? databasePath = null;
        string? siusDataDirectory = null;
        string? stevneId = null;
        string? stevneIds = null;
        string? ovelseId = null;
        string? ovelseName = null;
        string? outputDirectory = null;
        string? encoding = null;

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
                case "--sius-data":
                case "--sius-data-dir":
                    siusDataDirectory = ReadValue(args, ref index, arg);
                    break;
                case "--stevne-id":
                    stevneId = ReadValue(args, ref index, arg);
                    break;
                case "--stevne-ids":
                    stevneIds = ReadValue(args, ref index, arg);
                    break;
                case "--ovelse-id":
                    ovelseId = ReadValue(args, ref index, arg);
                    break;
                case "--ovelse":
                    ovelseName = ReadValue(args, ref index, arg);
                    break;
                case "--output-dir":
                    outputDirectory = ReadValue(args, ref index, arg);
                    break;
                case "--encoding":
                    encoding = ReadValue(args, ref index, arg);
                    break;
                default:
                    throw new ArgumentException($"Unknown option for {Name}: {arg}");
            }
        }

        var settings = AppSettings.Load(settingsPath);
        var resolvedDatabasePath = !string.IsNullOrWhiteSpace(databasePath)
            ? databasePath
            : settings.ResolveDatabasePath();
        if (!File.Exists(resolvedDatabasePath))
        {
            throw new ArgumentException($"Database file does not exist: {resolvedDatabasePath}");
        }

        var resolvedSiusDataDirectory = string.IsNullOrWhiteSpace(siusDataDirectory)
            ? DefaultSiusDataDirectory
            : siusDataDirectory;

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Use --output-dir to choose where CSV files should be written.");
        }

        var selectedStevneIds = !string.IsNullOrWhiteSpace(stevneIds)
            ? ParseIdList(stevneIds, "stevne-ids")
            : !string.IsNullOrWhiteSpace(stevneId)
                ? ParseIdList(stevneId, "stevne-id")
                : [];
        if (selectedStevneIds.Count == 0)
        {
            throw new ArgumentException("Use --stevne-id or --stevne-ids to select inrX event(s).");
        }

        return new SiusDataStartListExportOptions(
            resolvedDatabasePath,
            Path.GetFullPath(resolvedSiusDataDirectory),
            selectedStevneIds,
            ParseNullableInt(ovelseId, "ovelse-id"),
            string.IsNullOrWhiteSpace(ovelseName) ? null : ovelseName.Trim(),
            outputDirectory.Trim(),
            string.IsNullOrWhiteSpace(encoding) ? CsvEncoding.Utf8Bom : CsvEncoding.NormalizeName(encoding));
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

    private static int? ParseNullableInt(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new ArgumentException($"Option '--{name}' must be an integer.");
    }

    private static IReadOnlyList<int> ParseIdList(string value, string name)
    {
        var ids = new List<int>();
        foreach (var item in value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var range = item.Split('-', 2, StringSplitOptions.TrimEntries);
            if (range.Length == 2)
            {
                if (!int.TryParse(range[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var from) ||
                    !int.TryParse(range[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var to) ||
                    from > to)
                {
                    throw new ArgumentException($"Option '--{name}' has invalid id range '{item}'.");
                }

                ids.AddRange(Enumerable.Range(from, to - from + 1));
                continue;
            }

            if (!int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                throw new ArgumentException($"Option '--{name}' must contain comma-separated integer ids or ranges.");
            }

            ids.Add(id);
        }

        return ids.Distinct().ToList();
    }
}
