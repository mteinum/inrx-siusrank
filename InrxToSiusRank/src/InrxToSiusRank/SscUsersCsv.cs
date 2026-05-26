using System.Text;

namespace InrxToSiusRank;

public static class SscUsersCsv
{
    public static readonly string[] Header =
    [
        "OrganizationName",
        "OrganizationId",
        "UserId",
        "Name",
        "FirstName",
        "DisplayName",
        "NationName",
        "DisplayNationName",
        "ISOCode",
        "IOCCode",
        "UserClassName",
        "UserClassId",
        "UserGroupName",
        "UserGroupId",
        "ShootingSportsCloudUserId",
        "DateOfBirth",
        "Gender",
        "UserPictureId",
        "UserPreferredLanguage"
    ];

    public static string HeaderLine => string.Join(',', Header);

    public static string ToCsv(IReadOnlyList<SscUser> users)
    {
        var builder = new StringBuilder();
        builder.Append(HeaderLine).Append("\r\n");
        foreach (var user in users)
        {
            builder.AppendJoin(',', user.ToFields().Select(field => DelimitedText.Escape(field, ',')));
            builder.Append("\r\n");
        }

        return builder.ToString();
    }

    public static void Write(string outputPath, IReadOnlyList<SscUser> users, string encodingName)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputPath, ToCsv(users), CsvEncoding.GetEncoding(encodingName));
    }

    public static IReadOnlyList<SscUser> Read(string path)
    {
        var text = ReadText(path);
        var records = DelimitedText.ReadRecords(text, ',');
        if (records.Count == 0)
        {
            return [];
        }

        var header = records[0]
            .Select((name, index) => new { Name = name.Trim().TrimStart('\uFEFF'), Index = index })
            .ToDictionary(item => item.Name, item => item.Index, StringComparer.OrdinalIgnoreCase);

        return records
            .Skip(1)
            .Where(record => record.Any(field => !string.IsNullOrWhiteSpace(field)))
            .Select(record => new SscUser(
                OrganizationName: GetField(record, header, "OrganizationName"),
                OrganizationId: GetField(record, header, "OrganizationId"),
                UserId: GetField(record, header, "UserId"),
                Name: GetField(record, header, "Name"),
                FirstName: GetField(record, header, "FirstName"),
                DisplayName: GetField(record, header, "DisplayName"),
                NationName: GetField(record, header, "NationName"),
                DisplayNationName: GetField(record, header, "DisplayNationName"),
                ISOCode: GetField(record, header, "ISOCode"),
                IOCCode: GetField(record, header, "IOCCode"),
                UserClassName: GetField(record, header, "UserClassName"),
                UserClassId: GetField(record, header, "UserClassId"),
                UserGroupName: GetField(record, header, "UserGroupName"),
                UserGroupId: GetField(record, header, "UserGroupId"),
                ShootingSportsCloudUserId: GetField(record, header, "ShootingSportsCloudUserId"),
                DateOfBirth: GetField(record, header, "DateOfBirth"),
                Gender: GetField(record, header, "Gender"),
                UserPictureId: GetField(record, header, "UserPictureId"),
                UserPreferredLanguage: GetField(record, header, "UserPreferredLanguage")))
            .ToList();
    }

    private static string ReadText(string path)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }

    private static string GetField(
        IReadOnlyList<string> fields,
        IReadOnlyDictionary<string, int> header,
        string name)
    {
        return header.TryGetValue(name, out var index) && index >= 0 && index < fields.Count
            ? fields[index]
            : string.Empty;
    }
}
