namespace InrxToSiusRank.Tests;

public sealed class NmPistolFinalClassDefaultsTests
{
    [Fact]
    public void BuildText_suggests_silhouette_final_classes_with_enough_participants()
    {
        var text = NmPistolFinalClassDefaults.BuildText(
            [
                new NmPistolFinalClassExercise(
                    11,
                    "Silhuett",
                    "Sil",
                    new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Apen"] = 4,
                        ["Jrm"] = 2,
                        ["V55"] = 6
                    })
            ]);

        Assert.Equal("Silhuett: Apen,Jm", text);
    }

    [Fact]
    public void BuildText_skips_final_classes_with_only_one_participant()
    {
        var text = NmPistolFinalClassDefaults.BuildText(
            [
                new NmPistolFinalClassExercise(
                    18,
                    "Fripistol",
                    "Fri",
                    new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["SH1"] = 1
                    })
            ]);

        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void BuildText_suggests_finpistol_women_junior_women_and_sh1()
    {
        var text = NmPistolFinalClassDefaults.BuildText(
            [
                new NmPistolFinalClassExercise(
                    9,
                    "Finpistol",
                    "Fin",
                    new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Kvinner"] = 3,
                        ["Jrk"] = 2,
                        ["SH1-P3"] = 2,
                        ["Menn"] = 8
                    })
            ]);

        Assert.Equal("Finpistol: K,Jk,SH1", text);
    }
}
