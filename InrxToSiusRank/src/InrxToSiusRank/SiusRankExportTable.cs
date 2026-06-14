using System.Globalization;

namespace InrxToSiusRank;

public static class SiusRankExportTable
{
    private static readonly string[] SilhouetteImportHeader =
    [
        "ImportShotFilter",
        "SiusDataStartNumber"
    ];

    public static readonly string[] Header =
    [
        "StartNumber",
        "AccreditationNumber",
        "IssfId",
        "DisplayNameLong",
        "DisplayName",
        "FirstName",
        "Name",
        "BirthDay",
        "Gender",
        "Nation",
        "BibNumber",
        "TargetNumber",
        "Relay",
        "TeamIndex",
        "DuellIndex",
        "Groups",
        "Comment",
        "StarterId",
        "TeamPosition",
        "Team",
        "TeamDisplay",
        "TeamDuellIndex",
        "TeamComment"
    ];

    public static IReadOnlyList<string> Headers(bool includeSilhouetteImportColumns = false) =>
        includeSilhouetteImportColumns
            ? Header.Concat(SilhouetteImportHeader).ToArray()
            : Header;

    public static IReadOnlyList<string> Fields(
        SiusRankStarter row,
        bool includeSilhouetteImportColumns = false) =>
        includeSilhouetteImportColumns
            ? row.ToFields().Concat(SilhouetteImportFields(row)).ToArray()
            : row.ToFields();

    private static IEnumerable<string> SilhouetteImportFields(SiusRankStarter row)
    {
        var mapping = SilhouetteImportMapping.ForTarget(row.TargetNumber);
        return mapping is null
            ? [string.Empty, string.Empty]
            : [mapping.ImportShotFilter, mapping.SiusDataStartNumber.ToString(CultureInfo.InvariantCulture)];
    }
}
