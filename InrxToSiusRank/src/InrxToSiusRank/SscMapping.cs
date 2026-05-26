using System.Globalization;

namespace InrxToSiusRank;

public static class SscUserMapper
{
    public static SscUser Map(
        InrxStarter starter,
        string userId,
        string organizationName,
        string organizationId)
    {
        var firstName = starter.FirstName.Trim();
        var name = starter.LastName.Trim();
        var displayName = BuildDisplayName(firstName, name);
        var nation = NormalizeNationName(starter.Land);
        var nationCode = NormalizeNationCode(starter.Land);

        return new SscUser(
            OrganizationName: organizationName.Trim(),
            OrganizationId: organizationId.Trim(),
            UserId: userId.Trim(),
            Name: name,
            FirstName: firstName,
            DisplayName: displayName,
            NationName: nation,
            DisplayNationName: nation,
            ISOCode: nationCode,
            IOCCode: nationCode,
            UserClassName: string.Empty,
            UserClassId: string.Empty,
            UserGroupName: string.Empty,
            UserGroupId: string.Empty,
            ShootingSportsCloudUserId: string.Empty,
            DateOfBirth: NormalizeDateOfBirth(starter.BirthDay),
            Gender: NormalizeGender(starter.Gender),
            UserPictureId: string.Empty,
            UserPreferredLanguage: string.Empty);
    }

    public static IEnumerable<SscValidationMessage> ValidateSourceFields(IReadOnlyList<InrxStarter> starters)
    {
        foreach (var starter in starters)
        {
            if (!string.IsNullOrWhiteSpace(starter.Gender) && string.IsNullOrWhiteSpace(NormalizeGender(starter.Gender)))
            {
                yield return new SscValidationMessage(
                    SscValidationSeverity.Warning,
                    $"Deltaker.Id={starter.DeltakerId} has unexpected Gender value '{starter.Gender}'. Gender is exported empty.");
            }

            if (!string.IsNullOrWhiteSpace(starter.BirthDay) && string.IsNullOrWhiteSpace(NormalizeDateOfBirth(starter.BirthDay)))
            {
                yield return new SscValidationMessage(
                    SscValidationSeverity.Warning,
                    $"Deltaker.Id={starter.DeltakerId} has DateOfBirth value '{starter.BirthDay}' that is not a full supported date. DateOfBirth is exported empty.");
            }
        }
    }

    public static string BuildDisplayName(string firstName, string name)
    {
        var trimmedFirstName = firstName.Trim();
        var trimmedName = name.Trim();
        var upperName = trimmedName.ToUpperInvariant();
        return string.IsNullOrWhiteSpace(trimmedFirstName)
            ? upperName
            : $"{upperName} {trimmedFirstName}";
    }

    public static string NormalizeDateOfBirth(string value)
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
            ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    public static string NormalizeGender(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized switch
        {
            "M" or "MALE" or "MANN" => "M",
            "F" or "K" or "FEMALE" or "KVINNE" => "F",
            _ => string.Empty
        };
    }

    private static string NormalizeNationName(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized switch
        {
            "" or "NO" or "NOR" or "NORGE" or "NORWAY" => "Norway",
            _ => value.Trim()
        };
    }

    private static string NormalizeNationCode(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized switch
        {
            "" or "NO" or "NOR" or "NORGE" or "NORWAY" => "NOR",
            _ when normalized.Length == 3 => normalized,
            _ => normalized
        };
    }
}

public static class SscExerciseMapper
{
    public static string Resolve(OvelseInfo ovelse)
    {
        if (ovelse.Id == 18 || ovelse.Name.Contains("Fripistol", StringComparison.OrdinalIgnoreCase))
        {
            return "50m Fripistol";
        }

        if (ovelse.Id == 11 || ovelse.Name.Contains("Silhuett", StringComparison.OrdinalIgnoreCase))
        {
            return "25m Silhuettpistol";
        }

        if (ovelse.Id == 10 || ovelse.Name.Contains("Standard", StringComparison.OrdinalIgnoreCase))
        {
            return "25m Standardpistol";
        }

        if (ovelse.Id == 9 || ovelse.Name.Contains("Finpistol", StringComparison.OrdinalIgnoreCase))
        {
            return "25m Finpistol";
        }

        if (ovelse.Id == 8 || ovelse.Name.Contains("Grovpistol", StringComparison.OrdinalIgnoreCase))
        {
            return "25m Grovpistol";
        }

        if (ovelse.Id == 7 || ovelse.Name.Contains("Hurtig Fin", StringComparison.OrdinalIgnoreCase))
        {
            return "25m Hurtigpistol Fin";
        }

        if (ovelse.Id == 6 || ovelse.Name.Contains("Hurtig Grov", StringComparison.OrdinalIgnoreCase))
        {
            return "25m Hurtigpistol Grov";
        }

        throw new InvalidOperationException(
            $"No SSC ExerciseName mapping is configured for OvelseDef.Id={ovelse.Id} ({ovelse.Name}).");
    }
}
