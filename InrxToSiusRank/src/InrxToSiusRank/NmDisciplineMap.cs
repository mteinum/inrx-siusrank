namespace InrxToSiusRank;

public static class NmDisciplineMap
{
    private static readonly Dictionary<int, string> ByOvelseDefId = new()
    {
        [18] = "6199111f-86c4-42ea-96d2-65de9852b1a1", // 2A, 50m fripistol
        [11] = "3de123d0-b4b1-4181-8009-331cdf6b8a39", // 4, 25m silhuettpistol
        [10] = "3573e6db-2d45-409d-8902-7d3c3d17edd7", // 5, 25m standardpistol
        [9] = "2c014f8d-5f26-4dbf-81a0-18fb9ca28248",  // 6F, 25m finpistol
        [8] = "b9c94047-9442-45c2-a46c-e90bb0e0b8eb",  // 6G, 25m grovpistol
        [7] = "012eb41d-598f-49c9-8c3f-f0dfc6cd616a",  // 7F, 25m hurtigpistol fin
        [6] = "2fce2bdb-831c-4ac4-b092-0015cdf1b947"   // 7G, 25m hurtigpistol grov
    };

    private static readonly Dictionary<string, string> ByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Fripistol"] = ByOvelseDefId[18],
        ["Silhuett"] = ByOvelseDefId[11],
        ["Standard"] = ByOvelseDefId[10],
        ["Finpistol"] = ByOvelseDefId[9],
        ["Grovpistol"] = ByOvelseDefId[8],
        ["Hurtig Fin"] = ByOvelseDefId[7],
        ["Hurtig Grov"] = ByOvelseDefId[6]
    };

    public static string Resolve(OvelseInfo ovelse)
    {
        if (ByOvelseDefId.TryGetValue(ovelse.Id, out var disciplineId))
        {
            return disciplineId;
        }

        if (ByName.TryGetValue(ovelse.Name.Trim(), out disciplineId))
        {
            return disciplineId;
        }

        throw new InvalidOperationException(
            $"No NSF discipline mapping is configured for OvelseDef.Id={ovelse.Id} ({ovelse.Name}).");
    }
}
