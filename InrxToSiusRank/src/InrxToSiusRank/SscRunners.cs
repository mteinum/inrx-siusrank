using System.Globalization;

namespace InrxToSiusRank;

public static class SscUsersRunner
{
    public static SscUsersExportResult Run(ExportSscUsersOptions options)
    {
        using var repository = new InrxRepository(options.DatabasePath);
        var events = SscEventResolver.ResolveEvents(repository, options.StevneIds);
        var starters = events.SelectMany(eventExport => eventExport.Starters).ToList();
        var stevner = events.Select(eventExport => eventExport.Stevne).ToList();
        var bibMapPath = ResolveBibMapPathForUsers(options);
        var startNumbers = ChampionshipStartNumbers.Resolve(starters, stevner, bibMapPath);

        var users = starters
            .GroupBy(starter => starter.DeltakerId)
            .OrderBy(group => startNumbers[group.Key], StringComparer.Ordinal)
            .Select(group =>
            {
                var starter = group.OrderBy(item => item.ResultatId).First();
                return SscUserMapper.Map(
                    starter,
                    startNumbers[starter.DeltakerId],
                    options.OrganizationName,
                    options.OrganizationId);
            })
            .ToList();

        var messages = new List<SscValidationMessage>();
        messages.AddRange(SscUsersValidator.ValidateOrganization(options.OrganizationName, options.OrganizationId));
        messages.AddRange(SscUserMapper.ValidateSourceFields(starters));
        messages.AddRange(SscUsersValidator.ValidateUsers(users));

        var written = false;
        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            messages.Add(new SscValidationMessage(
                SscValidationSeverity.Warning,
                "No --output supplied. Dry-run only; no SSC users CSV or bib-map changes were written."));
        }
        else if (!messages.Any(message => message.Severity == SscValidationSeverity.Error))
        {
            if (!string.IsNullOrWhiteSpace(bibMapPath))
            {
                ChampionshipStartNumbers.Create(starters, stevner, bibMapPath);
            }

            SscUsersCsv.Write(options.OutputPath, users, options.EncodingName);
            written = true;
        }

        return new SscUsersExportResult(
            string.IsNullOrWhiteSpace(options.OutputPath) ? null : Path.GetFullPath(options.OutputPath),
            bibMapPath is null ? null : Path.GetFullPath(bibMapPath),
            written,
            users.Count,
            SscValidationMessageFormatter.Distinct(messages));
    }

    private static string? ResolveBibMapPathForUsers(ExportSscUsersOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.BibMapPath))
        {
            return Path.GetFullPath(options.BibMapPath);
        }

        if (!string.IsNullOrWhiteSpace(options.OutputPath))
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(options.OutputPath));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                return Path.Combine(directory, ChampionshipStartNumbers.BibMapFileName);
            }
        }

        var defaultPath = Path.GetFullPath(Path.Combine("siusrank-import", ChampionshipStartNumbers.BibMapFileName));
        return File.Exists(defaultPath) ? defaultPath : null;
    }
}

public static class SscValidationRunner
{
    public static SscValidationResult Run(ValidateSscOptions options)
    {
        using var repository = new InrxRepository(options.DatabasePath);
        var events = SscEventResolver.ResolveEvents(repository, options.StevneIds);
        var starters = events.SelectMany(eventExport => eventExport.Starters).ToList();
        var stevner = events.Select(eventExport => eventExport.Stevne).ToList();
        var bibMapPath = ResolveExistingBibMapPath(options.BibMapPath);
        var startNumbers = ChampionshipStartNumbers.Resolve(starters, stevner, bibMapPath);
        var users = SscUsersCsv.Read(options.UsersCsvPath);
        var messages = new List<SscValidationMessage>();

        if (bibMapPath is null)
        {
            messages.Add(new SscValidationMessage(
                SscValidationSeverity.Warning,
                "No existing --bib-map supplied. Validation used in-memory 26xxx assignments, which may differ from generated SIUS Rank imports."));
        }

        messages.AddRange(SscSetupValidator.ValidateSetup(events, startNumbers, users));

        return new SscValidationResult(
            starters.Count,
            users.Count,
            SscValidationMessageFormatter.Distinct(messages));
    }

    private static string? ResolveExistingBibMapPath(string? bibMapPath)
    {
        if (string.IsNullOrWhiteSpace(bibMapPath))
        {
            var defaultPath = Path.GetFullPath(Path.Combine("siusrank-import", ChampionshipStartNumbers.BibMapFileName));
            return File.Exists(defaultPath) ? defaultPath : null;
        }

        var resolved = Path.GetFullPath(bibMapPath);
        if (!File.Exists(resolved))
        {
            throw new ArgumentException($"bib-map.csv file does not exist: {resolved}");
        }

        return resolved;
    }
}

public static class SscLanesRunner
{
    public static SscLanesExportResult Run(ExportSscLanesOptions options)
    {
        SscLanePayloadBuilder.ValidateLaneCount(options.LaneCount);
        if (!DateTime.TryParse(options.Startlag, CultureInfo.InvariantCulture, DateTimeStyles.None, out var startlag))
        {
            throw new ArgumentException("--startlag must be an ISO-like date/time, for example 2026-07-06T09:00:00.");
        }

        using var repository = new InrxRepository(options.DatabasePath);
        var events = SscEventResolver.ResolveEvents(repository, [options.StevneId]);
        var starters = events.SelectMany(eventExport => eventExport.Starters).ToList();
        var startNumbers = ChampionshipStartNumbers.Create(
            starters,
            events.Select(eventExport => eventExport.Stevne),
            ResolveBibMapPathForLanes(options));
        var outputDirectory = Path.GetFullPath(options.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var reset = SscLanePayloadBuilder.BuildReset(options.LaneCount, generatedAtUtc);
        var active = SscLanePayloadBuilder.BuildActive(
            events,
            startlag,
            startNumbers,
            options.LaneCount,
            generatedAtUtc,
            out var messages);

        var resetPath = Path.Combine(outputDirectory, $"ssc-lanes-reset-1-{options.LaneCount}.json");
        var activePath = Path.Combine(outputDirectory, $"ssc-active-lanes-{startlag:yyyyMMddTHHmmss}.json");
        SscLanePayloadBuilder.Write(resetPath, reset);
        SscLanePayloadBuilder.Write(activePath, active);

        var allMessages = new List<SscValidationMessage>
        {
            new(
                SscValidationSeverity.Warning,
                "SA951 monitors are not Watchtower AthleteMonitor clients. Keep AthleteMonitorConnected=false; these files do not enable live result forwarding.")
        };
        allMessages.AddRange(messages);

        return new SscLanesExportResult(
            outputDirectory,
            resetPath,
            activePath,
            options.LaneCount,
            active.Lanes.Count,
            SscValidationMessageFormatter.Distinct(allMessages));
    }

    private static string ResolveBibMapPathForLanes(ExportSscLanesOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.BibMapPath))
        {
            return Path.GetFullPath(options.BibMapPath);
        }

        return Path.Combine(Path.GetFullPath(options.OutputDirectory), ChampionshipStartNumbers.BibMapFileName);
    }
}

public static class SscEventResolver
{
    public static IReadOnlyList<SscEventExport> ResolveEvents(
        InrxRepository repository,
        IReadOnlyList<int> stevneIds)
    {
        if (stevneIds.Count == 0)
        {
            throw new ArgumentException("Use --stevne-id or --stevne-ids to select event(s).");
        }

        return stevneIds
            .Select(repository.GetStevneById)
            .SelectMany(stevne =>
            {
                var ovelser = repository.GetOvelserForStevne(stevne.Id);
                if (ovelser.Count == 0)
                {
                    throw new InvalidOperationException($"No exercises found for Stevne.Id={stevne.Id}.");
                }

                return ovelser.Select(ovelse =>
                {
                    var ovelseInfo = new OvelseInfo(
                        ovelse.Id,
                        ovelse.Name,
                        ovelse.ShortName,
                        ovelse.HovedOvelseId);
                    var starters = repository.GetStarters(stevne.Id, ovelse.Id);
                    if (starters.Count == 0)
                    {
                        throw new InvalidOperationException(
                            $"No starters found for Stevne.Id={stevne.Id} and OvelseDef.Id={ovelse.Id}.");
                    }

                    return new SscEventExport(stevne, ovelseInfo, starters);
                });
            })
            .ToList();
    }
}
