using System.Globalization;

namespace InrxToSiusRank;

public static class SiusRankWritebackPlanner
{
    public static IReadOnlyList<SiusRankWritebackEventPlan> Plan(
        IReadOnlyList<SiusRankExportCompetition> exports,
        InrxWritebackInput input,
        IReadOnlyList<BibMapEntry> bibMapEntries,
        out IReadOnlyList<string> warnings)
    {
        var planWarnings = new List<string>();
        var bibMapByBib = BuildBibMapByBib(bibMapEntries, planWarnings);
        var result = new List<SiusRankWritebackEventPlan>();

        foreach (var export in exports)
        {
            result.Add(PlanEvent(export, input, bibMapByBib));
        }

        warnings = planWarnings;
        return result;
    }

    private static SiusRankWritebackEventPlan PlanEvent(
        SiusRankExportCompetition export,
        InrxWritebackInput input,
        IReadOnlyDictionary<string, BibMapEntry> bibMapByBib)
    {
        var warnings = new List<string>();
        var updates = new List<PlannedSiusRankWriteback>();
        var unchanged = new List<UnchangedSiusRankWriteback>();
        var skipped = new List<SkippedSiusRankWriteback>();
        var ovelseId = SiusRankEventDiscipline.ResolveOvelseDefId(export.ShortName, export.EventCode);

        if (ovelseId is null)
        {
            warnings.Add($"Could not map SIUS Rank event '{export.ShortName}' ({export.EventCode}) to an inrX OvelseDef.Id.");
            return new SiusRankWritebackEventPlan(export, null, [], [], [], warnings);
        }

        if (!input.Ovelser.TryGetValue(ovelseId.Value, out var ovelse))
        {
            warnings.Add($"Selected inrX stevner do not contain OvelseDef.Id={ovelseId.Value} for '{export.ShortName}'.");
            return new SiusRankWritebackEventPlan(export, ovelseId, [], [], [], warnings);
        }

        foreach (var athlete in export.Athletes)
        {
            var identity = new SkippedSiusRankWriteback(
                export.ShortName,
                athlete.BibNumber,
                athlete.AccreditationNumber,
                athlete.NameForDisplay,
                string.Empty);

            if (athlete.Result is null || athlete.Shots.Count == 0)
            {
                skipped.Add(identity with { Reason = "No complete result with shots in SIUS Rank export." });
                continue;
            }

            var candidates = ResolveCandidates(input.Results, ovelseId.Value, athlete, bibMapByBib).ToList();
            if (candidates.Count == 0)
            {
                skipped.Add(identity with { Reason = "Could not match SIUS Rank bib/NSF-id/name to a selected inrX Resultat row." });
                continue;
            }

            if (candidates.Count > 1)
            {
                skipped.Add(identity with
                {
                    Reason = "Matched multiple inrX Resultat rows: " +
                        string.Join(", ", candidates.Select(candidate => $"{candidate.ResultatId}/Stevne={candidate.StevneId}"))
                });
                continue;
            }

            InrxResultFields fields;
            try
            {
                fields = SiusRankResultConverter.Convert(athlete, ovelse);
            }
            catch (InvalidOperationException ex)
            {
                skipped.Add(identity with { Reason = ex.Message });
                continue;
            }

            if (fields.TotalScore != athlete.Result.Value)
            {
                skipped.Add(identity with
                {
                    Reason = $"Shot sum {fields.TotalScore} does not match exported total {athlete.Result.Value}."
                });
                continue;
            }

            if (athlete.InnerTens is not null && fields.InnerTens != athlete.InnerTens.Value)
            {
                skipped.Add(identity with
                {
                    Reason = $"Calculated inner tens {fields.InnerTens} does not match exported inner tens {athlete.InnerTens.Value}."
                });
                continue;
            }

            var row = candidates[0];
            var update = new PlannedSiusRankWriteback(
                export.ShortName,
                export.SourcePath,
                row.ResultatId,
                row.StevneId,
                row.OvelseDefId,
                row.DeltakerId,
                athlete.BibNumber,
                athlete.AccreditationNumber,
                athlete.NameForDisplay,
                row.ExistingTotal,
                row.ExistingInnerTens,
                row.ExistingShotCount,
                fields);

            if (ResultatWritebackValues.HasSubstantiveChanges(fields, row.ExistingValues))
            {
                updates.Add(update);
            }
            else
            {
                unchanged.Add(new UnchangedSiusRankWriteback(
                    export.ShortName,
                    athlete.BibNumber,
                    athlete.AccreditationNumber,
                    athlete.NameForDisplay,
                    row.ResultatId,
                    row.StevneId,
                    row.ExistingTotal,
                    row.ExistingInnerTens,
                    row.ExistingShotCount));
            }
        }

        return new SiusRankWritebackEventPlan(export, ovelseId, updates, unchanged, skipped, warnings);
    }

    private static IEnumerable<InrxResultRow> ResolveCandidates(
        IReadOnlyList<InrxResultRow> rows,
        int ovelseId,
        SiusRankExportAthlete athlete,
        IReadOnlyDictionary<string, BibMapEntry> bibMapByBib)
    {
        var candidates = rows.Where(row => row.OvelseDefId == ovelseId).ToList();

        if (bibMapByBib.TryGetValue(athlete.BibNumber, out var bibEntry) && bibEntry.DeltakerId > 0)
        {
            return candidates.Where(row => row.DeltakerId == bibEntry.DeltakerId);
        }

        if (int.TryParse(athlete.BibNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bibNumber))
        {
            var byResultatId = candidates.Where(row => row.ResultatId == bibNumber).ToList();
            if (byResultatId.Count > 0)
            {
                return byResultatId;
            }
        }

        var accreditationNumber = athlete.AccreditationNumber.Trim();
        if (!string.IsNullOrWhiteSpace(accreditationNumber))
        {
            var byNsfId = candidates
                .Where(row =>
                    row.NsfId.Equals(accreditationNumber, StringComparison.Ordinal) ||
                    row.Medlemsnummer.Equals(accreditationNumber, StringComparison.Ordinal))
                .ToList();
            if (byNsfId.Count > 0)
            {
                return byNsfId;
            }
        }

        var normalizedName = NormalizeName(athlete.FamilyName, athlete.GivenName);
        if (!string.IsNullOrWhiteSpace(normalizedName))
        {
            return candidates.Where(row => NormalizeName(row.LastName, row.FirstName) == normalizedName);
        }

        return [];
    }

    private static IReadOnlyDictionary<string, BibMapEntry> BuildBibMapByBib(
        IReadOnlyList<BibMapEntry> entries,
        List<string> warnings)
    {
        var result = new Dictionary<string, BibMapEntry>(StringComparer.Ordinal);
        foreach (var group in entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.BibNumber))
            .GroupBy(entry => entry.BibNumber, StringComparer.Ordinal))
        {
            var distinctDeltakerIds = group
                .Select(entry => entry.DeltakerId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (distinctDeltakerIds.Count > 1)
            {
                warnings.Add(
                    $"{ChampionshipStartNumbers.BibMapFileName} maps bib {group.Key} to multiple Deltaker.Id values: " +
                    string.Join(", ", distinctDeltakerIds));
                continue;
            }

            result[group.Key] = group.First();
        }

        return result;
    }

    private static string NormalizeName(string familyName, string givenName) =>
        $"{familyName} {givenName}"
            .Trim()
            .ToUpperInvariant()
            .Replace("  ", " ", StringComparison.Ordinal);
}
