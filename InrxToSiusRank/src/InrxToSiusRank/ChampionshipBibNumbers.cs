using System.Globalization;

namespace InrxToSiusRank;

public static class ChampionshipBibNumbers
{
    public static IReadOnlyDictionary<int, string> Create(IReadOnlyList<InrxStarter> starters)
    {
        return starters
            .Select(starter => starter.DeltakerId)
            .Where(deltakerId => deltakerId > 0)
            .Distinct()
            .Order()
            .Select((deltakerId, index) => new
            {
                DeltakerId = deltakerId,
                BibNumber = (index + 1).ToString(CultureInfo.InvariantCulture)
            })
            .ToDictionary(item => item.DeltakerId, item => item.BibNumber);
    }
}
