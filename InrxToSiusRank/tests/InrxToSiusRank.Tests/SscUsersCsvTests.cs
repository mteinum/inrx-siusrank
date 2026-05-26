namespace InrxToSiusRank.Tests;

public sealed class SscUsersCsvTests
{
    [Fact]
    public void Header_matches_shooting_sports_cloud_export_order()
    {
        const string expected =
            "OrganizationName,OrganizationId,UserId,Name,FirstName,DisplayName,NationName,DisplayNationName,ISOCode,IOCCode,UserClassName,UserClassId,UserGroupName,UserGroupId,ShootingSportsCloudUserId,DateOfBirth,Gender,UserPictureId,UserPreferredLanguage";

        Assert.Equal(expected, SscUsersCsv.HeaderLine);
    }

    [Fact]
    public void Csv_preserves_norwegian_characters_and_quotes_fields()
    {
        var user = CreateUser(
            name: "Bjørnsen, Øst",
            firstName: "Pål \"Åge\"",
            displayName: "BJØRNSEN, ØST Pål \"Åge\" æøå");

        var csv = SscUsersCsv.ToCsv([user]);

        Assert.StartsWith(SscUsersCsv.HeaderLine + "\r\n", csv);
        Assert.Contains("\"Bjørnsen, Øst\"", csv);
        Assert.Contains("\"Pål \"\"Åge\"\"\"", csv);
        Assert.Contains("\"BJØRNSEN, ØST Pål \"\"Åge\"\" æøå\"", csv);
        Assert.EndsWith("\r\n", csv);
    }

    [Fact]
    public void Mapper_sets_gender_and_date_of_birth_when_inrx_values_are_safe()
    {
        var starter = CreateStarter(birthDay: "23.06.1973", gender: "K");

        var user = SscUserMapper.Map(starter, "26001", "Legacy", "f95a2bc3-79bd-4c24-98b6-4e17f99bbfaf");

        Assert.Equal("1973-06-23", user.DateOfBirth);
        Assert.Equal("F", user.Gender);
        Assert.Equal("Norway", user.NationName);
        Assert.Equal("NOR", user.ISOCode);
        Assert.Equal("NOR", user.IOCCode);
    }

    [Fact]
    public void Validator_reports_duplicate_user_id()
    {
        var messages = SscUsersValidator.ValidateUsers(
        [
            CreateUser(userId: "26001", displayName: "A"),
            CreateUser(userId: "26001", displayName: "B")
        ]);

        Assert.Contains(messages, message =>
            message.Severity == SscValidationSeverity.Error &&
            message.Message.Contains("Duplicate UserId 26001", StringComparison.Ordinal));
    }

    [Fact]
    public void Validator_reports_missing_organization()
    {
        var messages = SscUsersValidator.ValidateUsers([CreateUser(organizationName: "", organizationId: "")]);

        Assert.Contains(messages, message =>
            message.Severity == SscValidationSeverity.Error &&
            message.Message.Contains("empty OrganizationName", StringComparison.Ordinal));
        Assert.Contains(messages, message =>
            message.Severity == SscValidationSeverity.Error &&
            message.Message.Contains("empty OrganizationId", StringComparison.Ordinal));
    }

    private static SscUser CreateUser(
        string organizationName = "Legacy",
        string organizationId = "f95a2bc3-79bd-4c24-98b6-4e17f99bbfaf",
        string userId = "26001",
        string name = "Teinum",
        string firstName = "Morten",
        string displayName = "TEINUM Morten") =>
        new(
            OrganizationName: organizationName,
            OrganizationId: organizationId,
            UserId: userId,
            Name: name,
            FirstName: firstName,
            DisplayName: displayName,
            NationName: "Norway",
            DisplayNationName: "Norway",
            ISOCode: "NOR",
            IOCCode: "NOR",
            UserClassName: string.Empty,
            UserClassId: string.Empty,
            UserGroupName: string.Empty,
            UserGroupId: string.Empty,
            ShootingSportsCloudUserId: string.Empty,
            DateOfBirth: "1973-06-23",
            Gender: "M",
            UserPictureId: string.Empty,
            UserPreferredLanguage: string.Empty);

    private static InrxStarter CreateStarter(string birthDay = "1973-06-23", string gender = "M") =>
        new(
            ResultatId: 1,
            DeltakerId: 100,
            Standplass: 5,
            SkivenrFra: string.Empty,
            SkivenrTil: string.Empty,
            Relay: 1,
            RelayDate: "2026-07-06 09:00:00",
            NsfId: "905380",
            AccreditationNumber: string.Empty,
            FirstName: "Morten",
            LastName: "Teinum",
            BirthDay: birthDay,
            Gender: gender,
            Land: "Norge",
            ClubName: "Kristiansand Pistolskyttere",
            ClubShortName: "KPS",
            InrxClass: "-",
            KmNmClass: "Å",
            DmClass: "-",
            OvelseName: "Fripistol",
            StevneName: "NM Fripistol");
}
