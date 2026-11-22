using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ElectricityTimeSeries.Api.Authorization;
using ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

namespace ElectricityTimeSeries.Api.IntegrationTests.TimeSeries;

/// <summary>
/// Calls the time series endpoints over HTTP, as a client would. Requests are written as raw JSON,
/// so the tests also check the public JSON format (field names, enum names, "PT15M" durations).
/// All tests share one API instance and its in-memory store, so each test uses its own data and
/// only asserts on series it created.
/// </summary>
public class TimeSeriesEndpointsTests : IClassFixture<ApiFactory>
{
    private const string BaseUrl = "api/time-series";
    private static readonly DateTimeOffset Midnight = new(2025, 11, 22, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public TimeSeriesEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
        _client = CreateClient(Scopes.TimeSeriesRead, Scopes.TimeSeriesWrite);
    }

    [Fact]
    public async Task Create_returns_201_with_location_and_the_stored_series()
    {
        var response = await _client.PostAsJsonAsync(BaseUrl, Series(zone: "no1", points: 4));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadSeries(response);
        Assert.Equal($"/{BaseUrl}/{created.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal("Consumption", created.Type);
        Assert.Equal("NO1", created.BiddingZone);
        Assert.Equal("PT15M", created.Resolution);
        Assert.Equal(Midnight, created.Start);
        Assert.Equal(Midnight.AddHours(1), created.End);
        Assert.Equal(4, created.Points.Length);
    }

    [Fact]
    public async Task Get_by_id_returns_the_created_series()
    {
        var created = await Create(Series(zone: "NO2", points: 2));

        var response = await _client.GetAsync($"{BaseUrl}/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await ReadSeries(response);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(created.Points, fetched.Points);
    }

    [Fact]
    public async Task Update_replaces_the_series_contents()
    {
        var created = await Create(Series(zone: "NO3", points: 4));

        var response = await _client.PutAsJsonAsync(
            $"{BaseUrl}/{created.Id}",
            Series(type: "Production", zone: "NO3", resolution: "PT1H", points: 2, step: TimeSpan.FromHours(1)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await ReadSeries(await _client.GetAsync($"{BaseUrl}/{created.Id}"));
        Assert.Equal("Production", fetched.Type);
        Assert.Equal("PT1H", fetched.Resolution);
        Assert.Equal(2, fetched.Points.Length);
    }

    [Fact]
    public async Task Delete_returns_204_and_the_series_is_gone()
    {
        var created = await Create(Series(zone: "NO4", points: 1));

        var response = await _client.DeleteAsync($"{BaseUrl}/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{BaseUrl}/{created.Id}")).StatusCode);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Unknown_id_returns_404_problem_details(string method)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), $"{BaseUrl}/{Guid.NewGuid()}");
        if (method == "PUT")
        {
            request.Content = JsonContent.Create(Series());
        }

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Search_by_bidding_zone_includes_flows_in_both_directions()
    {
        var consumption = await Create(Series(zone: "FI", points: 1));
        var flowOut = await Create(Series(type: "CrossBorderFlow", zone: "FI", toZone: "SE1", points: 1));
        var flowIn = await Create(Series(type: "CrossBorderFlow", zone: "EE", toZone: "FI", points: 1));
        var elsewhere = await Create(Series(zone: "SE1", points: 1));

        var results = await Search("biddingZone=fi");

        var ids = results.Select(s => s.Id).ToList();
        Assert.Contains(consumption.Id, ids);
        Assert.Contains(flowOut.Id, ids);
        Assert.Contains(flowIn.Id, ids);
        Assert.DoesNotContain(elsewhere.Id, ids);
        Assert.All(results, s => Assert.True(s.BiddingZone == "FI" || s.ToBiddingZone == "FI"));
    }

    [Fact]
    public async Task Search_by_type_returns_only_that_type()
    {
        var production = await Create(Series(type: "Production", zone: "DK2", points: 1));
        var consumption = await Create(Series(type: "Consumption", zone: "DK2", points: 1));

        var results = await Search("biddingZone=DK2&type=Production");

        Assert.Contains(results, s => s.Id == production.Id);
        Assert.DoesNotContain(results, s => s.Id == consumption.Id);
    }

    [Fact]
    public async Task Search_by_time_interval_returns_overlapping_series_with_only_the_points_inside()
    {
        // 00:00-01:00 and 02:00-03:00, both in 15-minute steps.
        var early = await Create(Series(zone: "LT", start: Midnight, points: 4));
        var late = await Create(Series(zone: "LT", start: Midnight.AddHours(2), points: 4));

        var results = await Search($"biddingZone=LT&from={Query(Midnight.AddMinutes(30))}&to={Query(Midnight.AddHours(2))}");

        var match = Assert.Single(results);
        Assert.Equal(early.Id, match.Id);
        Assert.Equal([Midnight.AddMinutes(30), Midnight.AddMinutes(45)], match.Points.Select(p => p.Timestamp));
        Assert.DoesNotContain(results, s => s.Id == late.Id);
    }

    [Fact]
    public async Task Search_accepts_times_with_any_offset()
    {
        var created = await Create(Series(zone: "LV", start: Midnight, points: 4));
        var oneAmInOslo = Midnight.ToOffset(TimeSpan.FromHours(1)); // 01:00+01:00 is 00:00 UTC

        var results = await Search($"biddingZone=LV&from={Query(oneAmInOslo)}&to={Query(oneAmInOslo.AddMinutes(15))}");

        var match = Assert.Single(results, s => s.Id == created.Id);
        Assert.Equal(Midnight, Assert.Single(match.Points).Timestamp);
    }

    public static TheoryData<string, object> InvalidRequests => new()
    {
        { "biddingZone", Series(zone: "XX1") },
        { "toBiddingZone", Series(type: "CrossBorderFlow", zone: "NO1") },
        { "toBiddingZone", Series(type: "CrossBorderFlow", zone: "NO1", toZone: "NO1") },
        { "resolution", Series(resolution: "PT30M") },
        { "points", Series(points: 0) },
        { "points[0].value", Series(value: -1m) },
        { "points[0].timestamp", Series(start: Midnight.AddMinutes(7)) },
        { "points[1].timestamp", Series(points: 2, step: TimeSpan.FromMinutes(30)) },
        { "type", Without(Series(), "type") },
        { "biddingZone", Without(Series(), "biddingZone") },
        { "resolution", Without(Series(), "resolution") },
        { "points", Without(Series(), "points") },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Invalid_series_returns_400_validation_problem(string expectedField, object body)
    {
        var response = await _client.PostAsJsonAsync(BaseUrl, body);

        await AssertValidationProblem(response, expectedField);
    }

    [Theory]
    [InlineData("""{ "type": "Nuclear" }""", "$.type")]
    [InlineData("""{ "resolution": "15 minutes" }""", "$.resolution")]
    [InlineData("""{ "points": [{ "timestamp": "yesterday", "value": 1 }] }""", "$.points[0].timestamp")]
    public async Task Malformed_json_returns_400_validation_problem(string json, string expectedField)
    {
        var response = await _client.PostAsync(BaseUrl, new StringContent(json, MediaTypeHeaderValue.Parse("application/json")));

        await AssertValidationProblem(response, expectedField);
    }

    [Fact]
    public async Task Invalid_update_returns_400_and_keeps_the_original()
    {
        var created = await Create(Series(zone: "PL", points: 2));

        var response = await _client.PutAsJsonAsync($"{BaseUrl}/{created.Id}", Series(zone: "XX1"));

        await AssertValidationProblem(response, "biddingZone");
        var fetched = await ReadSeries(await _client.GetAsync($"{BaseUrl}/{created.Id}"));
        Assert.Equal("PL", fetched.BiddingZone);
    }

    [Theory]
    [InlineData("biddingZone=XX1", "biddingZone")]
    [InlineData("type=Nuclear", "type")]
    [InlineData("from=2025-11-22T02:00:00Z&to=2025-11-22T01:00:00Z", "from")]
    [InlineData("from=2025-11-22T01:00:00Z&to=2025-11-22T01:00:00Z", "from")]
    [InlineData("from=not-a-date", "from")]
    public async Task Invalid_search_returns_400_validation_problem(string query, string expectedField)
    {
        var response = await _client.GetAsync($"{BaseUrl}?{query}");

        await AssertValidationProblem(response, expectedField);
    }

    [Theory]
    [InlineData("GET", "", HttpStatusCode.OK)]
    [InlineData("POST", "", HttpStatusCode.Forbidden)]
    [InlineData("PUT", "/{id}", HttpStatusCode.Forbidden)]
    [InlineData("DELETE", "/{id}", HttpStatusCode.Forbidden)]
    public async Task Read_only_token_can_read_but_not_write(string method, string path, HttpStatusCode expected)
    {
        var created = await Create(Series(zone: "NL", points: 1));
        var readOnly = CreateClient(Scopes.TimeSeriesRead);
        var request = new HttpRequestMessage(new HttpMethod(method), BaseUrl + path.Replace("{id}", created.Id.ToString()));
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(Series(zone: "NL"));
        }

        var response = await readOnly.SendAsync(request);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Write_only_token_cannot_read()
    {
        var writeOnly = CreateClient(Scopes.TimeSeriesWrite);

        var response = await writeOnly.GetAsync(BaseUrl);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateClient(params string[] scopes)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.Tokens.CreateToken(scopes));
        return client;
    }

    private async Task<SeriesResult> Create(object body)
    {
        var response = await _client.PostAsJsonAsync(BaseUrl, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadSeries(response);
    }

    private async Task<SeriesResult[]> Search(string query)
    {
        var response = await _client.GetAsync($"{BaseUrl}?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SeriesResult[]>(Json))!;
    }

    private static async Task<SeriesResult> ReadSeries(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<SeriesResult>(Json))!;

    private static async Task AssertValidationProblem(HttpResponseMessage response, string expectedField)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonObject>();
        var fields = problem?["errors"]?.AsObject().Select(e => e.Key).ToList() ?? [];
        Assert.True(
            fields.Contains(expectedField, StringComparer.OrdinalIgnoreCase),
            $"Expected an error for '{expectedField}', got: {string.Join(", ", fields)}.");
    }

    private static string Query(DateTimeOffset time) => Uri.EscapeDataString(time.ToString("O"));

    /// <summary>Builds a request body. Defaults to a valid 4-point consumption series in NO1.</summary>
    private static JsonObject Series(
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

    private static JsonObject Without(JsonObject body, string field)
    {
        body.Remove(field);
        return body;
    }

    private sealed record SeriesResult(
        Guid Id,
        string Type,
        string BiddingZone,
        string? ToBiddingZone,
        string Resolution,
        DateTimeOffset Start,
        DateTimeOffset End,
        PointResult[] Points);

    private sealed record PointResult(DateTimeOffset Timestamp, decimal Value);
}
