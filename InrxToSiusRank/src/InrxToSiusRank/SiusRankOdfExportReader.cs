using System.Globalization;
using System.Xml.Linq;

namespace InrxToSiusRank;

public static class SiusRankOdfExportReader
{
    public static IReadOnlyList<SiusRankExportCompetition> ReadLatestIndividualResults(
        string exportsDirectory,
        IReadOnlySet<string> eventFilters)
    {
        var competitions = Directory
            .EnumerateFiles(exportsDirectory, "*.odf.xml", SearchOption.AllDirectories)
            .Select(Parse)
            .Where(competition => competition is not null)
            .Select(competition => competition!)
            .Where(competition => competition.ProductType.Equals("IndividualResults", StringComparison.OrdinalIgnoreCase))
            .Where(competition => SiusRankEventDiscipline.MatchesFilters(
                competition.ShortName,
                competition.EventCode,
                eventFilters))
            .ToList();

        return competitions
            .GroupBy(competition => competition.ShortName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(competition => competition.ShotResultCount > 0)
                .ThenByDescending(competition => competition.LastWriteTime)
                .ThenByDescending(competition => competition.ResultCount)
                .First())
            .OrderBy(competition => competition.ShortName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static SiusRankExportCompetition? Parse(string path)
    {
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var header = document.Descendants("ExtendedHeader").FirstOrDefault();
        if (header is null)
        {
            return null;
        }

        var eventCode = GetAttribute(header, "EventCode");
        var shortName = GetAttribute(header, "ShortName");
        var productType = GetAttribute(header, "ProductType");
        if (string.IsNullOrWhiteSpace(shortName) || string.IsNullOrWhiteSpace(productType))
        {
            return null;
        }

        var athletes = document
            .Descendants("CumulativeResult")
            .Select(ParseAthlete)
            .Where(athlete => athlete is not null)
            .Select(athlete => athlete!)
            .ToList();

        return new SiusRankExportCompetition(
            Path.GetFullPath(path),
            eventCode,
            shortName,
            GetAttribute(header, "EventUnitName"),
            productType,
            GetAttribute(document.Root, "ResultStatus"),
            File.GetLastWriteTime(path),
            athletes);
    }

    private static SiusRankExportAthlete? ParseAthlete(XElement cumulativeResult)
    {
        var competitor = cumulativeResult.Element("Competitor");
        var athlete = competitor?
            .Element("Composition")?
            .Elements("Athlete")
            .FirstOrDefault();
        if (competitor is null || athlete is null)
        {
            return null;
        }

        var extendedResults = athlete
            .Element("ExtendedResults")?
            .Elements("ExtendedResult")
            .ToList() ?? [];

        var result = ParseInt(GetAttribute(cumulativeResult, "Result")) ??
            ParseInt(ValueForCode(extendedResults, "SH_TOTAL")?.Split('-', 2)[0] ?? string.Empty);
        var innerTens = ParseInt(ValueForCode(extendedResults, "SH_INNER_TENS"));
        var shots = extendedResults
            .Where(element => GetAttribute(element, "Code").Equals("SH_SHOT", StringComparison.OrdinalIgnoreCase))
            .Select(ParseShot)
            .OrderBy(shot => shot.Position)
            .ToList();

        return new SiusRankExportAthlete(
            FirstNonEmpty(
                GetAttribute(athlete, "Bib"),
                GetAttribute(competitor, "Bib"),
                GetAttribute(athlete.Element("ExtendedDataItems"), "CompetitionBibNumber")),
            FirstNonEmpty(
                GetAttribute(athlete, "AccreditationNumber"),
                GetAttribute(competitor, "AccreditationNumber")),
            GetAttribute(athlete, "FamilyName"),
            GetAttribute(athlete, "GivenName"),
            GetAttribute(competitor, "NameDisplay"),
            result,
            innerTens,
            shots);
    }

    private static SiusRankExportShot ParseShot(XElement shotElement)
    {
        var extensions = shotElement
            .Element("Extensions")?
            .Elements("Extension")
            .ToLookup(element => GetAttribute(element, "Code"), StringComparer.OrdinalIgnoreCase);

        return new SiusRankExportShot(
            ParseInt(GetAttribute(shotElement, "Pos")) ?? 0,
            ParseInt(GetAttribute(shotElement, "Value")) ?? 0,
            ParseOdfCoordinate(ValueForCode(extensions, "SH_SHOT_X")),
            ParseOdfCoordinate(ValueForCode(extensions, "SH_SHOT_Y")),
            ValueForCode(extensions, "SH_TIMESTAMP") ?? string.Empty);
    }

    private static decimal? ParseOdfCoordinate(string? value)
    {
        if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return null;
        }

        return parsed / 100m;
    }

    private static string? ValueForCode(IReadOnlyList<XElement> elements, string code) =>
        elements
            .FirstOrDefault(element => GetAttribute(element, "Code").Equals(code, StringComparison.OrdinalIgnoreCase))
            ?.Attribute("Value")
            ?.Value;

    private static string? ValueForCode(ILookup<string, XElement>? lookup, string code) =>
        lookup?[code].FirstOrDefault()?.Attribute("Value")?.Value;

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
        {
            return decimal.ToInt32(decimal.Truncate(decimalValue));
        }

        return null;
    }

    private static string GetAttribute(XElement? element, string name) =>
        element?.Attribute(name)?.Value.Trim() ?? string.Empty;

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
