using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

namespace ElectricityTimeSeries.Api.IntegrationTests.TimeSeries;

public class OverlapTests : TimeSeriesApiTest
{
    public OverlapTests(TestStorage storage)
        : base(storage)
    {
    }

    [Fact]
    public async Task An_overlapping_series_is_refused_with_409_naming_the_one_in_the_way()
    {
        var existing = await Create(Series(start: Midnight, points: 4));

        var response = await Post(Series(start: Midnight.AddMinutes(30), points: 4));

        Assert.Equal([existing.Id], await OverlappingIds(response));
        Assert.Single(await Search());
    }

    public static TheoryData<string, JsonObject> SeriesThatDontOverlap => new()
    {
        { "touching", Series(start: Midnight.AddHours(1)) },
        { "other zone", Series(zone: "SE3") },
        { "other type", Series(type: "Production") },
    };

    [Theory]
    [MemberData(nameof(SeriesThatDontOverlap))]
    public async Task Series_that_only_touch_or_differ_are_accepted(string reason, JsonObject body)
    {
        await Create(Series(start: Midnight, points: 4));

        var response = await Post(body);

        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected 201 for a {reason} series, got {(int)response.StatusCode}.");
    }

    [Fact]
    public async Task Flows_in_opposite_directions_are_separate()
    {
        await Create(Series(type: "CrossBorderFlow", zone: "NO2", toZone: "DK1"));

        var otherWay = await Post(Series(type: "CrossBorderFlow", zone: "DK1", toZone: "NO2"));
        var sameWay = await Post(Series(type: "CrossBorderFlow", zone: "NO2", toZone: "DK1"));

        Assert.Equal(HttpStatusCode.Created, otherWay.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, sameWay.StatusCode);
    }

    [Fact]
    public async Task An_update_into_another_series_is_refused()
    {
        var first = await Create(Series(start: Midnight, points: 4));
        var second = await Create(Series(start: Midnight.AddHours(1), points: 4));

        var response = await Put(second.Id, Series(start: Midnight.AddMinutes(45), points: 4));

        Assert.Equal([first.Id], await OverlappingIds(response));
        Assert.Equal(Midnight.AddHours(1), (await Get(second.Id)).Start);
    }

    [Fact]
    public async Task An_update_may_cover_its_own_period()
    {
        var series = await Create(Series(start: Midnight, points: 4));

        var response = await Put(series.Id, Series(start: Midnight.AddMinutes(15), points: 8));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Of_simultaneous_overlapping_creates_only_one_is_saved()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Post(Series(zone: "FI"))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }));
        Assert.Single(await Search("biddingZone=FI"));
    }

    private static async Task<Guid[]> OverlappingIds(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);
        return problem!["overlappingIds"]!.AsArray().Select(id => Guid.Parse((string)id!)).ToArray();
    }
}
