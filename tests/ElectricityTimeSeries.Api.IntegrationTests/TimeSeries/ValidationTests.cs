using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

namespace ElectricityTimeSeries.Api.IntegrationTests.TimeSeries;

public class ValidationTests : TimeSeriesApiTest
{
    public ValidationTests(TestStorage storage)
        : base(storage)
    {
    }

    public static TheoryData<string, JsonObject> InvalidRequests => new()
    {
        // Business rules, checked by the Core layer.
        { "biddingZone", Series(zone: "XX1") },
        { "biddingZone", Series(zone: "   ") },
        { "toBiddingZone", Series(type: "CrossBorderFlow", zone: "NO1", toZone: "NO1") },
        { "toBiddingZone", Series(type: "CrossBorderFlow", zone: "NO1", toZone: "") },
        { "resolution", Series(resolution: "PT30M") },
        { "points", Series(points: 0) },
        { "points[0].value", Series(value: -1m) },
        { "points[0].value", Series(type: "Production", value: -1m) },
        { "points[0].value", Series(type: "CrossBorderFlow", zone: "NO1", toZone: "SE3", value: -1m) },
        { "points[0].value", Series(value: 0.0000001m) },                // 7 decimal places
        { "points[0].value", Series(value: 10_000_000_000_000m) },       // too large
        { "points[0].timestamp", Series(start: Midnight.AddMinutes(7)) },
        { "points[0].timestamp", Series(resolution: "PT1H", start: Midnight.AddMinutes(15), points: 1) },
        { "points[1].timestamp", Series(points: 2, step: TimeSpan.FromMinutes(30)) },   // gap
        { "points[1].timestamp", Series(points: 2, step: TimeSpan.Zero) },              // duplicate
        { "points[1].timestamp", Series(points: 2, step: TimeSpan.FromMinutes(-15)) },  // out of order
        // Only flows have a receiving zone.
        { "toBiddingZone", Series(type: "Consumption", zone: "NO1", toZone: "SE3") },
        { "toBiddingZone", Series(type: "Production", zone: "NO1", toZone: "SE3") },
        // Types are names only.
        { "$.type", With(Series(), "type", 42) },
        { "$.type", With(Series(), "type", "42") },
        { "$.type", With(Series(), "type", "Nuclear") },
        // Missing or null fields are reported by name.
        { "type", Without(Series(), "type") },
        { "type", WithNull(Series(), "type") },
        { "biddingZone", Without(Series(), "biddingZone") },
        { "resolution", Without(Series(), "resolution") },
        { "points", Without(Series(), "points") },
        { "toBiddingZone", Series(type: "CrossBorderFlow", zone: "NO1") },
        { "biddingZone", WithNull(Series(), "biddingZone") },
        { "resolution", WithNull(Series(), "resolution") },
        { "points", WithNull(Series(), "points") },
        { "points[0]", With(Series(), "points", new JsonArray((JsonNode?)null)) },
        { "points[1]", WithNullPoint(Series(points: 3), 1) },
    };

    private static JsonObject WithNullPoint(JsonObject body, int index)
    {
        body["points"]![index] = null;
        return body;
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Invalid_series_returns_400_and_stores_nothing(string expectedField, JsonObject body)
    {
        var response = await Post(body);

        await AssertValidationProblem(response, expectedField);
        Assert.Empty(await Search());
    }

    [Fact]
    public async Task Two_months_of_15_minute_values_are_accepted_but_one_more_is_not()
    {
        var accepted = await Post(Series(points: MaxPoints));
        var rejected = await Post(Series(start: Midnight.AddYears(2), points: MaxPoints + 1));

        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        await AssertValidationProblem(rejected, "points");
    }

    [Fact]
    public async Task Business_rule_errors_are_reported_together()
    {
        var body = Series(type: "Consumption", zone: "XX1", resolution: "PT15M", points: 2, step: TimeSpan.FromMinutes(30), value: -5m);

        var fields = await ErrorFields(await Post(body));

        Assert.Equal(["biddingZone", "points[0].value", "points[1].timestamp", "points[1].value"], fields.Order());
    }

    [Fact]
    public async Task Every_missing_field_of_the_chosen_kind_is_reported_at_once()
    {
        var fields = await ErrorFields(await Post(new JsonObject { ["type"] = "CrossBorderFlow" }));

        Assert.Equal(["biddingZone", "points", "resolution", "toBiddingZone"], fields.Order());
    }

    [Fact]
    public async Task An_empty_object_reports_every_missing_field()
    {
        var fields = await ErrorFields(await Post(new JsonObject()));

        Assert.Equal(["biddingZone", "points", "resolution", "type"], fields.Order());
    }

    [Theory]
    [InlineData("""{ "type": "Nuclear" }""", "$.type")]
    [InlineData("""{ "type": "Consumption", "biddingZone": 1 }""", "$.biddingZone")]
    [InlineData("""{ "type": "Consumption", "resolution": "15 minutes" }""", "$.resolution")]
    [InlineData("""{ "type": "Consumption", "points": [{ "timestamp": "yesterday", "value": 1 }] }""", "$.points[0].timestamp")]
    [InlineData("""{ "type": "Consumption", "biddingZone": "NO1", "resolution": "PT15M", "points": [{ "timestamp": "2025-11-22T00:00:00Z" }] }""", "points[0].value")]
    [InlineData("", "")]
    [InlineData("{ not json", "$")]
    public async Task Unreadable_body_reports_exactly_one_error_for_the_real_problem(string json, string expectedField)
    {
        var fields = await ErrorFields(await PostRaw(json));

        Assert.Equal([expectedField], fields);
    }

    [Fact]
    public async Task Body_that_is_not_json_returns_415()
    {
        var response = await Client.PostAsync(BaseUrl, new StringContent("type=Consumption"), Ct);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Error_responses_say_what_went_wrong()
    {
        var response = await Post(Series(zone: "XX1"));

        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);
        Assert.Equal(400, problem!["status"]!.GetValue<int>());
        Assert.StartsWith("Unknown bidding zone.", problem["errors"]!["biddingZone"]![0]!.GetValue<string>());
    }
}
