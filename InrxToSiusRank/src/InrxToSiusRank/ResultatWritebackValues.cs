using System.Globalization;

namespace InrxToSiusRank;

internal static class ResultatWritebackValues
{
    private static readonly HashSet<string> AuditColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "oppdatert",
        "oppdatertAv"
    };

    public static IReadOnlySet<string> SubstantiveColumns { get; } = Build(new InrxResultFields(
            SeriesPerPart: Enumerable.Repeat(string.Empty, 8).ToList(),
            PartSumsText: Enumerable.Repeat(string.Empty, 8).ToList(),
            InnerTensPerPart: Enumerable.Repeat(string.Empty, 8).ToList(),
            XyPerPart: Enumerable.Repeat(string.Empty, 8).ToList(),
            SumPerPart: Enumerable.Repeat(0, 8).ToList(),
            SumRank: Enumerable.Repeat(0, 16).ToList(),
            InnerRank: Enumerable.Repeat(0, 16).ToList(),
            TotalScore: 0,
            InnerTens: 0,
            PerShotRanking: string.Empty,
            MlTarget: 0))
        .Keys
        .Where(column => !AuditColumns.Contains(column))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static Dictionary<string, object?> Build(InrxResultFields fields)
    {
        var values = BuildSubstantive(fields);
        values["oppdatert"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        values["oppdatertAv"] = "InrxToSiusRank";
        return values;
    }

    public static Dictionary<string, object?> BuildSubstantive(InrxResultFields fields)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["mlCal"] = string.Empty,
            ["mlTarget"] = fields.MlTarget,
            ["mlIsMl"] = 1,
            ["totinnertreff"] = fields.InnerTens,
            ["totsum"] = fields.TotalScore,
            ["perTreffRangStr"] = fields.PerShotRanking,
            ["statcomplete"] = 1,
            ["statincomplete"] = 0,
            ["statinit"] = 0,
            ["statdnf"] = 0,
            ["statdns"] = 0,
            ["statdsq"] = 0,
            ["delsumFinale"] = string.Empty,
            ["totFinale"] = 0,
            ["delsumOmskytingDm"] = string.Empty,
            ["delsumOmskytingKm"] = string.Empty,
            ["delsumOmskytingNm"] = string.Empty,
            ["totOmskytingDm"] = 0,
            ["totOmskytingKm"] = 0,
            ["totOmskytingNm"] = 0,
            ["delsumOmskyting"] = string.Empty,
            ["totOmskyting"] = 0
        };

        for (var index = 1; index <= 8; index++)
        {
            values[$"serierDelOvelse{index}"] = fields.SeriesPerPart[index - 1];
            values[$"delsumDelOvelse{index}"] = fields.PartSumsText[index - 1];
            values[$"innertreffDelOvelse{index}"] = fields.InnerTensPerPart[index - 1];
            values[$"mlXyDelOvelse{index}"] = fields.XyPerPart[index - 1];
            values[$"sumDelOvelse{index}"] = fields.SumPerPart[index - 1];
        }

        for (var index = 1; index <= 16; index++)
        {
            values[$"sumr{index}"] = fields.SumRank[index - 1];
            values[$"ix{index}"] = fields.InnerRank[index - 1];
        }

        return values;
    }

    public static bool HasSubstantiveChanges(
        InrxResultFields fields,
        IReadOnlyDictionary<string, object?> existingValues)
    {
        var plannedValues = BuildSubstantive(fields);
        foreach (var (column, plannedValue) in plannedValues)
        {
            if (!existingValues.TryGetValue(column, out var existingValue))
            {
                continue;
            }

            if (!ValuesEqual(plannedValue, existingValue))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ValuesEqual(object? plannedValue, object? existingValue)
    {
        if (plannedValue is string plannedString)
        {
            return plannedString.Equals(NormalizeString(existingValue), StringComparison.Ordinal);
        }

        if (plannedValue is int plannedInt)
        {
            return plannedInt == NormalizeInt(existingValue);
        }

        return Equals(plannedValue, existingValue);
    }

    private static string NormalizeString(object? value) =>
        value is null || value == DBNull.Value
            ? string.Empty
            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    private static int NormalizeInt(object? value)
    {
        if (value is null || value == DBNull.Value)
        {
            return 0;
        }

        return value switch
        {
            int intValue => intValue,
            long longValue => Convert.ToInt32(longValue, CultureInfo.InvariantCulture),
            double doubleValue => Convert.ToInt32(doubleValue, CultureInfo.InvariantCulture),
            decimal decimalValue => Convert.ToInt32(decimalValue, CultureInfo.InvariantCulture),
            _ => int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0
        };
    }
}
