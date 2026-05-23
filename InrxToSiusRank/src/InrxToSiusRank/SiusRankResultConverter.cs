using System.Globalization;

namespace InrxToSiusRank;

public static class SiusRankResultConverter
{
    private const char InnerTen = 'O';
    private const char Ten = 'X';
    private const char Miss = '-';

    public static InrxResultFields Convert(
        SiusRankExportAthlete athlete,
        InrxOvelseDefinition ovelse)
    {
        if (ovelse.SkuddPerSerie <= 0)
        {
            throw new InvalidOperationException($"{ovelse.Name} has invalid skuddpserie={ovelse.SkuddPerSerie}.");
        }

        if (ovelse.TotalSeries <= 0)
        {
            throw new InvalidOperationException($"{ovelse.Name} has no configured series.");
        }

        var shots = athlete.Shots
            .OrderBy(shot => shot.Position)
            .ToList();
        if (shots.Count != ovelse.ExpectedShots)
        {
            throw new InvalidOperationException(
                $"{athlete.NameForDisplay} has {shots.Count} shots in SIUS Rank export, " +
                $"expected {ovelse.ExpectedShots} for {ovelse.Name}.");
        }

        var innerTenPositions = ResolveInnerTenPositions(shots, athlete.InnerTens ?? 0);
        var series = new List<ConvertedSeries>();
        for (var index = 0; index < shots.Count; index += ovelse.SkuddPerSerie)
        {
            series.Add(ConvertSeries(
                shots.Skip(index).Take(ovelse.SkuddPerSerie).ToList(),
                innerTenPositions));
        }

        var partSeries = new List<string>();
        var partSumText = new List<string>();
        var partInnerText = new List<string>();
        var partXyText = new List<string>();
        var partSums = new List<int>();

        var seriesIndex = 0;
        foreach (var seriesCount in ovelse.SeriesPerPart)
        {
            var items = series.Skip(seriesIndex).Take(seriesCount).ToList();
            partSeries.Add(string.Concat(items.Select(item => item.SeriesText + ";")));
            partSumText.Add(string.Concat(items.Select(item => item.Sum.ToString(CultureInfo.InvariantCulture) + ";")));
            partInnerText.Add(string.Concat(items.Select(item => item.InnerTens.ToString(CultureInfo.InvariantCulture) + ";")));
            partXyText.Add(string.Concat(items.Select(item => item.XyText + ";")));
            partSums.Add(items.Sum(item => item.Sum));
            seriesIndex += seriesCount;
        }

        while (partSeries.Count < 8)
        {
            partSeries.Add(string.Empty);
            partSumText.Add(string.Empty);
            partInnerText.Add(string.Empty);
            partXyText.Add(string.Empty);
            partSums.Add(0);
        }

        var sumRank = GroupSeriesValues(series.Select(item => item.Sum), ovelse.SeriePerRang);
        var innerRank = GroupSeriesValues(series.Select(item => item.InnerTens), ovelse.SeriePerRang);
        var total = series.Sum(item => item.Sum);
        var innerTens = series.Sum(item => item.InnerTens);

        return new InrxResultFields(
            partSeries,
            partSumText,
            partInnerText,
            partXyText,
            partSums,
            sumRank,
            innerRank,
            total,
            innerTens,
            BuildPerShotRanking(string.Concat(series.Select(item => item.SeriesText))),
            ovelse.MlTarget);
    }

    private static ConvertedSeries ConvertSeries(
        IReadOnlyList<SiusRankExportShot> shots,
        IReadOnlySet<int> innerTenPositions)
    {
        var text = new char[shots.Count];
        var sum = 0;
        var innerTens = 0;
        var xy = new List<string>();

        for (var index = 0; index < shots.Count; index++)
        {
            var shot = shots[index];
            if (shot.Score == 10 && innerTenPositions.Contains(shot.Position))
            {
                text[index] = InnerTen;
                sum += 10;
                innerTens++;
                xy.Add(FormatXy(shot));
            }
            else if (shot.Score == 10)
            {
                text[index] = Ten;
                sum += 10;
                xy.Add(FormatXy(shot));
            }
            else if (shot.Score is > 0 and < 10)
            {
                text[index] = (char)('0' + shot.Score);
                sum += shot.Score;
                xy.Add(FormatXy(shot));
            }
            else
            {
                text[index] = Miss;
                xy.Add("#%");
            }
        }

        return new ConvertedSeries(new string(text), sum, innerTens, string.Concat(xy));
    }

    private static IReadOnlySet<int> ResolveInnerTenPositions(
        IReadOnlyList<SiusRankExportShot> shots,
        int innerTenCount)
    {
        if (innerTenCount <= 0)
        {
            return new HashSet<int>();
        }

        return shots
            .Where(shot => shot.Score == 10)
            .OrderBy(shot => shot.X is null || shot.Y is null)
            .ThenBy(shot => DistanceSquared(shot))
            .ThenBy(shot => shot.Position)
            .Take(innerTenCount)
            .Select(shot => shot.Position)
            .ToHashSet();
    }

    private static decimal DistanceSquared(SiusRankExportShot shot)
    {
        if (shot.X is null || shot.Y is null)
        {
            return decimal.MaxValue;
        }

        return shot.X.Value * shot.X.Value + shot.Y.Value * shot.Y.Value;
    }

    private static string FormatXy(SiusRankExportShot shot)
    {
        if (shot.X is null || shot.Y is null)
        {
            return "#%";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{FormatCoordinate(shot.X.Value)}#{FormatCoordinate(shot.Y.Value)}%");
    }

    private static string FormatCoordinate(decimal value) =>
        value.ToString("0.#####", CultureInfo.InvariantCulture);

    private static IReadOnlyList<int> GroupSeriesValues(IEnumerable<int> values, int seriePerRang)
    {
        var groups = new List<int>();
        var groupSize = Math.Max(1, seriePerRang);
        var current = 0;
        var count = 0;
        foreach (var value in values)
        {
            current += value;
            count++;
            if (count == groupSize)
            {
                groups.Add(current);
                current = 0;
                count = 0;
            }
        }

        if (count > 0)
        {
            groups.Add(current);
        }

        while (groups.Count < 16)
        {
            groups.Add(0);
        }

        return groups.Take(16).ToList();
    }

    private static string BuildPerShotRanking(string shotText)
    {
        var result = new char[shotText.Length];
        for (var index = 0; index < shotText.Length; index++)
        {
            var source = shotText[shotText.Length - 1 - index];
            result[index] = source switch
            {
                InnerTen => 'A',
                Ten => 'B',
                '9' => 'C',
                '8' => 'D',
                '7' => 'E',
                '6' => 'F',
                '5' => 'G',
                '4' => 'H',
                '3' => 'I',
                '2' => 'J',
                '1' => 'K',
                _ => 'T'
            };
        }

        return new string(result);
    }

    private sealed record ConvertedSeries(string SeriesText, int Sum, int InnerTens, string XyText);
}
