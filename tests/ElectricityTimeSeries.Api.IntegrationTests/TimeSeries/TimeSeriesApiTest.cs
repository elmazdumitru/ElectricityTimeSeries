using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ElectricityTimeSeries.Api.Authorization;
using ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

namespace ElectricityTimeSeries.Api.IntegrationTests.TimeSeries;

/// <summary>Helpers for the time series tests. Bodies are raw JSON, so the public JSON format is tested too.</summary>
public abstract class TimeSeriesApiTest : IsolatedApiTest
{
    protected const string BaseUrl = "api/time-series";

    /// <summary>The largest series allowed: a leap year of 15-minute values.</summary>
    protected const int MaxPoints = 366 * 24 * 4;

    protected static readonly DateTimeOffset Midnight = new(2025, 11, 22, 0, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private HttpClient? _client;

    protected TimeSeriesApiTest(TestStorage storage)
        : base(storage)
    {
    }

    protected HttpClient Client => _client ??= CreateClient(Scopes.TimeSeriesRead, Scopes.TimeSeriesWrite);

    protected HttpClient CreateClient(params string[] scopes)
    {
        var client = Api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Api.Tokens.CreateToken(scopes));
        return client;
    }

    protected Task<HttpResponseMessage> Post(object body) => Client.PostAsJsonAsync(BaseUrl, body, Ct);

    protected Task<HttpResponseMessage> PostRaw(string json) =>
        Client.PostAsync(BaseUrl, new StringContent(json, MediaTypeHeaderValue.Parse("application/json")), Ct);

    protected Task<HttpResponseMessage> Put(Guid id, object body) => Client.PutAsJsonAsync($"{BaseUrl}/{id}", body, Ct);

    protected Task<HttpResponseMessage> GetById(Guid id) => Client.GetAsync($"{BaseUrl}/{id}", Ct);

    protected Task<HttpResponseMessage> Delete(Guid id) => Client.DeleteAsync($"{BaseUrl}/{id}", Ct);

    protected async Task<SeriesResult> Create(object body)
    {
        var response = await Post(body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadSeries(response);
    }

    protected async Task<SeriesResult> Get(Guid id)
    {
        var response = await GetById(id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadSeries(response);
    }

    protected async Task<SeriesResult[]> Search(string query = "")
    {
        var response = await Client.GetAsync($"{BaseUrl}?{query}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SeriesResult[]>(Json, Ct))!;
    }

    protected static async Task<SeriesResult> ReadSeries(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<SeriesResult>(Json, Ct))!;

    /// <summary>Asserts a 400 validation problem and returns the invalid field names.</summary>
    protected static async Task<string[]> ErrorFields(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);
        return problem?["errors"]?.AsObject().Select(e => e.Key).ToArray() ?? [];
    }

    protected static async Task AssertValidationProblem(HttpResponseMessage response, string expectedField)
    {
        var fields = await ErrorFields(response);
        Assert.True(
            fields.Contains(expectedField, StringComparer.OrdinalIgnoreCase),
            $"Expected an error for '{expectedField}', got: {string.Join(", ", fields)}.");
    }

    /// <summary>Records compare arrays by reference, so the points are compared separately.</summary>
    protected static void AssertSameSeries(SeriesResult expected, SeriesResult actual)
    {
        Assert.Equal(expected with { Points = [] }, actual with { Points = [] }, SeriesWithoutPoints.Instance);
        Assert.Equal(expected.Points, actual.Points);
    }

    protected static string Query(DateTimeOffset time) => Uri.EscapeDataString(time.ToString("O"));

    /// <summary>Defaults to a valid 4-point consumption series in NO1, starting at midnight UTC.</summary>
    protected static JsonObject Series(
        string type = "Consumption",
        string zone = "NO1",
        string? toZone = null,
        string resolution = "PT15M",
        DateTimeOffset? start = null,
        int points = 4,
        TimeSpan? step = null,
        decimal value = 1000m)
    {
        var body = new JsonObject
        {
            ["type"] = type,
            ["biddingZone"] = zone,
            ["resolution"] = resolution,
            ["points"] = new JsonArray(Enumerable.Range(0, points)
                .Select(i => (JsonNode)new JsonObject
                {
                    ["timestamp"] = ((start ?? Midnight) + (step ?? TimeSpan.FromMinutes(15)) * i).ToString("O"),
                    ["value"] = value + i,
                })
                .ToArray()),
        };

        if (toZone is not null)
        {
            body["toBiddingZone"] = toZone;
        }

        return body;
    }

    protected static JsonObject Without(JsonObject body, string field)
    {
        body.Remove(field);
        return body;
    }

    protected static JsonObject WithNull(JsonObject body, string field) => With(body, field, null);

    protected static JsonObject With(JsonObject body, string field, JsonNode? value)
    {
        body[field] = value;
        return body;
    }

    protected sealed record SeriesResult(
        Guid Id,
        string Type,
        string BiddingZone,
        string? ToBiddingZone,
        string Resolution,
        DateTimeOffset Start,
        DateTimeOffset End,
        PointResult[] Points);

    protected sealed record PointResult(DateTimeOffset Timestamp, decimal Value);

    private sealed class SeriesWithoutPoints : IEqualityComparer<SeriesResult>
    {
        public static readonly SeriesWithoutPoints Instance = new();

        public bool Equals(SeriesResult? x, SeriesResult? y) =>
            x is not null && y is not null &&
            (x.Id, x.Type, x.BiddingZone, x.ToBiddingZone, x.Resolution, x.Start, x.End) ==
            (y.Id, y.Type, y.BiddingZone, y.ToBiddingZone, y.Resolution, y.Start, y.End);

        public int GetHashCode(SeriesResult obj) => obj.Id.GetHashCode();
    }
}
