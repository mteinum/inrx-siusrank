namespace InrxToSiusRank.Tests;

public sealed class ExportValidatorTests
{
    [Theory]
    [InlineData(2, 39)]
    [InlineData(1, 38)]
    public void Silhouette_validation_accepts_targets_for_selected_layout(int shootersPerStand, int target)
    {
        var errors = ExportValidator.ValidateInrxSilhouetteTargets(
            [Starter(target)],
            Silhouette(),
            shootersPerStand);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(2, 40)]
    [InlineData(1, 39)]
    public void Silhouette_validation_rejects_targets_from_other_layout(int shootersPerStand, int target)
    {
        var errors = ExportValidator.ValidateInrxSilhouetteTargets(
                [Starter(target)],
                Silhouette(),
                shootersPerStand)
            .ToList();

        var error = Assert.Single(errors);
        Assert.Contains($"standplass {target}", error);
        Assert.Contains($"{shootersPerStand} skytter", error);
    }

    private static OvelseInfo Silhouette() =>
        new(11, "Silhuett", "Sil", 8);

    private static InrxStarter Starter(int target) =>
        new(
            ResultatId: 7432,
            DeltakerId: 327,
            Standplass: target,
            SkivenrFra: string.Empty,
            SkivenrTil: string.Empty,
            Relay: 1,
            RelayDate: "2026-05-31 10:00:00",
            NsfId: "905397",
            AccreditationNumber: string.Empty,
            FirstName: "Rune",
            LastName: "Wold",
            BirthDay: "1972-02-27",
            Gender: "M",
            Land: "NOR",
            ClubName: "Kristiansand Pistolskyttere",
            ClubShortName: "KPS",
            InrxClass: "C",
            KmNmClass: "C",
            DmClass: string.Empty,
            OvelseName: "Silhuett",
            StevneName: "20260531 Silhuett");
}
