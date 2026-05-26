namespace InrxToSiusRank;

public static class SscUsersValidator
{
    public static IReadOnlyList<SscValidationMessage> ValidateUsers(IReadOnlyList<SscUser> users)
    {
        var messages = new List<SscValidationMessage>();

        foreach (var user in users)
        {
            if (string.IsNullOrWhiteSpace(user.UserId))
            {
                messages.Add(Error($"User '{user.DisplayName}' is missing UserId."));
            }

            if (string.IsNullOrWhiteSpace(user.OrganizationName))
            {
                messages.Add(Error($"UserId={FormatUserId(user)} has empty OrganizationName."));
            }

            if (string.IsNullOrWhiteSpace(user.OrganizationId))
            {
                messages.Add(Error($"UserId={FormatUserId(user)} has empty OrganizationId."));
            }
            else if (!Guid.TryParse(user.OrganizationId, out _))
            {
                messages.Add(Error($"UserId={FormatUserId(user)} has invalid OrganizationId '{user.OrganizationId}'."));
            }

            if (string.IsNullOrWhiteSpace(user.Name))
            {
                messages.Add(Error($"UserId={FormatUserId(user)} has empty Name."));
            }

            if (string.IsNullOrWhiteSpace(user.FirstName))
            {
                messages.Add(Error($"UserId={FormatUserId(user)} has empty FirstName."));
            }

            if (string.IsNullOrWhiteSpace(user.DisplayName))
            {
                messages.Add(Error($"UserId={FormatUserId(user)} has empty DisplayName."));
            }

            if (!string.IsNullOrWhiteSpace(user.Gender) &&
                !user.Gender.Equals("M", StringComparison.OrdinalIgnoreCase) &&
                !user.Gender.Equals("F", StringComparison.OrdinalIgnoreCase))
            {
                messages.Add(Warning($"UserId={FormatUserId(user)} has unexpected Gender value '{user.Gender}'. Expected M, F, or empty."));
            }
        }

        foreach (var duplicate in users
                     .Where(user => !string.IsNullOrWhiteSpace(user.UserId))
                     .GroupBy(user => user.UserId.Trim(), StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            messages.Add(Error(
                $"Duplicate UserId {duplicate.Key}: " +
                string.Join(", ", duplicate.Select(user => user.DisplayName))));
        }

        return messages;
    }

    public static IReadOnlyList<SscValidationMessage> ValidateOrganization(
        string organizationName,
        string organizationId)
    {
        var messages = new List<SscValidationMessage>();
        if (string.IsNullOrWhiteSpace(organizationName))
        {
            messages.Add(Error("OrganizationName is required."));
        }

        if (string.IsNullOrWhiteSpace(organizationId))
        {
            messages.Add(Error("OrganizationId is required."));
        }
        else if (!Guid.TryParse(organizationId, out _))
        {
            messages.Add(Error($"OrganizationId must be a GUID: {organizationId}"));
        }

        return messages;
    }

    private static string FormatUserId(SscUser user) =>
        string.IsNullOrWhiteSpace(user.UserId) ? "<empty>" : user.UserId.Trim();

    private static SscValidationMessage Error(string message) =>
        new(SscValidationSeverity.Error, message);

    private static SscValidationMessage Warning(string message) =>
        new(SscValidationSeverity.Warning, message);
}

public static class SscSetupValidator
{
    public static IReadOnlyList<SscValidationMessage> ValidateSetup(
        IReadOnlyList<SscEventExport> events,
        IReadOnlyDictionary<int, string> startNumbersByDeltakerId,
        IReadOnlyList<SscUser> users)
    {
        var messages = new List<SscValidationMessage>();
        messages.AddRange(SscUsersValidator.ValidateUsers(users));

        var userIds = users
            .Where(user => !string.IsNullOrWhiteSpace(user.UserId))
            .Select(user => user.UserId.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var eventExport in events)
        {
            try
            {
                SscExerciseMapper.Resolve(eventExport.Ovelse);
            }
            catch (InvalidOperationException ex)
            {
                messages.Add(new SscValidationMessage(SscValidationSeverity.Error, ex.Message));
            }

            foreach (var starter in eventExport.Starters)
            {
                if (!startNumbersByDeltakerId.TryGetValue(starter.DeltakerId, out var userId) ||
                    string.IsNullOrWhiteSpace(userId))
                {
                    messages.Add(new SscValidationMessage(
                        SscValidationSeverity.Error,
                        $"Resultat.Id={starter.ResultatId} ({starter.FirstName} {starter.LastName}) has no UserId/bib-map assignment."));
                    continue;
                }

                if (!userIds.Contains(userId))
                {
                    messages.Add(new SscValidationMessage(
                        SscValidationSeverity.Error,
                        $"Missing SSC user for Deltaker.Id={starter.DeltakerId}, UserId={userId}, {starter.FirstName} {starter.LastName}."));
                }

                var targetNumber = SscLanePayloadBuilder.ResolveLaneNumber(starter);
                if (targetNumber is not null && (targetNumber < 1 || targetNumber > 40))
                {
                    messages.Add(new SscValidationMessage(
                        SscValidationSeverity.Error,
                        $"Resultat.Id={starter.ResultatId} has target/lane {targetNumber}, outside SSC validation range 1-40."));
                }
            }
        }

        return messages;
    }
}

public static class SscValidationMessageFormatter
{
    public static string Prefix(SscValidationMessage message) =>
        message.Severity == SscValidationSeverity.Error ? "ERROR" : "WARNING";

    public static IReadOnlyList<SscValidationMessage> Distinct(IReadOnlyList<SscValidationMessage> messages) =>
        messages
            .GroupBy(message => $"{message.Severity}\u001f{message.Message}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
}
