using System.Globalization;

namespace InrxToSiusRank;

public static class StarterMapper
{
    public static SiusRankStarter Map(
        InrxStarter starter,
        string? siusGroupOverride,
        bool includeClubTeam,
        string? bibNumberOverride = null)
    {
        var startNumber = starter.ResultatId.ToString(CultureInfo.InvariantCulture);
        var bibNumber = FirstNonEmpty(bibNumberOverride ?? string.Empty, startNumber);
        var targetNumber = ResolveTargetNumber(starter);
        var relay = starter.Relay.GetValueOrDefault(1).ToString(CultureInfo.InvariantCulture);
        var lastName = starter.LastName.Trim();
        var firstName = starter.FirstName.Trim();
        var upperLastName = lastName.ToUpperInvariant();
        var displayName = string.IsNullOrWhiteSpace(firstName)
            ? upperLastName
            : $"{upperLastName} {firstName}";
        var team = includeClubTeam ? FirstNonEmpty(starter.ClubShortName, starter.ClubName) : string.Empty;
        var group = string.IsNullOrWhiteSpace(siusGroupOverride)
            ? GroupNormalizer.Normalize(starter.KmNmClass)
            : siusGroupOverride.Trim();

        return new SiusRankStarter(
            StartNumber: startNumber,
            AccreditationNumber: FirstNonEmpty(starter.AccreditationNumber, startNumber),
            IssfId: string.Empty,
            DisplayNameLong: displayName,
            DisplayName: displayName,
            FirstName: firstName,
            Name: upperLastName,
            BirthDay: NormalizeBirthDate(starter.BirthDay),
            Gender: NormalizeGender(starter.Gender),
            Nation: NormalizeNationForExport(starter.Land),
            BibNumber: bibNumber,
            TargetNumber: targetNumber,
            Relay: relay,
            TeamIndex: "1",
            DuellIndex: "1",
            Groups: group,
            Comment: string.Empty,
            StarterId: startNumber,
            TeamPosition: "1",
            Team: team,
            TeamDisplay: team,
            TeamDuellIndex: "1",
            TeamComment: string.Empty);
    }

    private static string ResolveTargetNumber(InrxStarter starter)
    {
        if (starter.Standplass > 0)
        {
            return starter.Standplass.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(starter.SkivenrFra))
        {
            return starter.SkivenrFra.Trim();
        }

        return string.Empty;
    }

    private static string NormalizeBirthDate(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var formats = new[]
        {
            "dd.MM.yyyy",
            "d.M.yyyy",
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm:ss"
        };

        return DateTime.TryParseExact(trimmed, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
            : trimmed;
    }

    private static string NormalizeGender(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized switch
        {
            "M" or "MALE" or "MANN" => "M",
            "F" or "K" or "FEMALE" or "KVINNE" => "F",
            _ => string.Empty
        };
    }

    public static string NormalizeNationForExport(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized switch
        {
            "" or "NO" or "NOR" or "NORGE" or "NORWAY" => "NOR",
            _ when normalized.Length == 3 => normalized,
            _ => normalized
        };
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
