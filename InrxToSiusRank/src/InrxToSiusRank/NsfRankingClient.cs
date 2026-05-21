using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InrxToSiusRank;

public interface ISeedStartLagRankingProvider
{
    Task<IReadOnlyList<RankingEntry>> GetRankingAsync(
        string disciplineId,
        string periodStart,
        string periodEnd,
        CancellationToken cancellationToken = default);
}

public sealed class NsfRankingClient : ISeedStartLagRankingProvider, IDisposable
{
    private const string RankingEndpoint = "https://nsfapi.azurewebsites.net/ranking";
    private readonly HttpClient _httpClient;
    private readonly bool _disposeHttpClient;

    public NsfRankingClient()
        : this(new HttpClient(), disposeHttpClient: true)
    {
    }

    public NsfRankingClient(HttpClient httpClient, bool disposeHttpClient = false)
    {
        _httpClient = httpClient;
        _disposeHttpClient = disposeHttpClient;
    }

    public async Task<IReadOnlyList<RankingEntry>> GetRankingAsync(
        string disciplineId,
        string periodStart,
        string periodEnd,
        CancellationToken cancellationToken = default)
    {
        var rankings = new List<RankingEntry>();
        for (var pageIndex = 0;; pageIndex++)
        {
            var url = BuildUrl(disciplineId, periodStart, periodEnd, pageIndex);
            await using var stream = await _httpClient.GetStreamAsync(url, cancellationToken);
            var page = await JsonSerializer.DeserializeAsync<RankingPage>(
                stream,
                cancellationToken: cancellationToken);
            if (page is null)
            {
                throw new InvalidOperationException("NSF ranking API returned an empty response.");
            }

            var items = page.Items ?? Array.Empty<RankingItem>();
            rankings.AddRange(items.Select(item => new RankingEntry(
                item.PersonId ?? string.Empty,
                item.FullName ?? string.Empty,
                item.Position,
                item.TotalScore)));

            if (page.Paging?.HasNextPage != true)
            {
                return rankings;
            }
        }
    }

    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static string BuildUrl(string disciplineId, string periodStart, string periodEnd, int pageIndex)
    {
        var query = new Dictionary<string, string>
        {
            ["pageIndex"] = pageIndex.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = "200",
            ["orderBy"] = "totalScore:desc",
            ["disciplineId"] = disciplineId,
            ["numberOfResults"] = "1",
            ["periodStart"] = periodStart,
            ["periodEnd"] = periodEnd
        };

        return RankingEndpoint + "?" + string.Join(
            "&",
            query.Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));
    }

    private sealed record RankingPage(
        [property: JsonPropertyName("paging")] RankingPaging? Paging,
        [property: JsonPropertyName("items")] IReadOnlyList<RankingItem> Items);

    private sealed record RankingPaging(
        [property: JsonPropertyName("hasNextPage")] bool HasNextPage);

    private sealed record RankingItem(
        [property: JsonPropertyName("position")] int Position,
        [property: JsonPropertyName("personId")] string? PersonId,
        [property: JsonPropertyName("fullName")] string? FullName,
        [property: JsonPropertyName("totalScore")] decimal TotalScore);
}
