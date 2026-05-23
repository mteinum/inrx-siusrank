using System.Globalization;

namespace InrxToSiusRank;

public static class ChampionshipStartNumbers
{
    private const int MaxSequence = 9999;

    public static IReadOnlyDictionary<int, string> Create(
        IEnumerable<InrxStarter> starters,
        IEnumerable<StevneInfo> stevner)
    {
        var stevneList = stevner.ToList();
        var yearPrefix = ResolveYearPrefix(stevneList);
        var deltakerIds = starters
            .Select(starter => starter.DeltakerId)
            .Distinct()
            .Order()
            .ToList();

        if (deltakerIds.Any(id => id <= 0))
        {
            throw new InvalidOperationException("Cannot assign start numbers when one or more starters have no Deltaker.Id.");
        }

        if (deltakerIds.Count > MaxSequence)
        {
            throw new InvalidOperationException(
                $"Cannot assign {deltakerIds.Count} start numbers with year prefix {yearPrefix}. " +
                "SIUS start and bib numbers must be maximum 6 digits.");
        }

        return deltakerIds
            .Select((deltakerId, index) => new
            {
                DeltakerId = deltakerId,
                StartNumber = $"{yearPrefix}{index + 1:D3}"
            })
            .ToDictionary(item => item.DeltakerId, item => item.StartNumber);
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
}
