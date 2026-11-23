using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

namespace ElectricityTimeSeries.Api.IntegrationTests.TimeSeries;

public class CrudTests : TimeSeriesApiTest
{
    public CrudTests(TestStorage storage)
        : base(storage)
    {
    }

    [Fact]
    public async Task Create_returns_201_with_location_and_the_stored_series()
    {
        var response = await Post(Series(zone: "no1", points: 4));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var created = await ReadSeries(response);
        Assert.Equal($"/{BaseUrl}/{created.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal("Consumption", created.Type);
        Assert.Equal("NO1", created.BiddingZone);
        Assert.Equal("PT15M", created.Resolution);
        Assert.Equal(Midnight, created.Start);
        Assert.Equal(Midnight.AddHours(1), created.End);
        Assert.Equal(4, created.Points.Length);
    }

    [Theory]
    [InlineData("Production", "SE3", null, "PT1H")]
    [InlineData("Consumption", "FI", null, "PT15M")]
    [InlineData("CrossBorderFlow", "NO2", "DK1", "PT15M")]
    public async Task Each_kind_reads_back_exactly_as_it_was_created(string type, string zone, string? toZone, string resolution)
    {
        var step = resolution == "PT1H" ? TimeSpan.FromHours(1) : TimeSpan.FromMinutes(15);
        var created = await Create(Series(type: type, zone: zone, toZone: toZone, resolution: resolution, points: 3, step: step));

        var fetched = await Get(created.Id);

        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(type, fetched.Type);
        Assert.Equal(zone, fetched.BiddingZone);
        Assert.Equal(toZone, fetched.ToBiddingZone);
        Assert.Equal(resolution, fetched.Resolution);
        Assert.Equal(Midnight, fetched.Start);
        Assert.Equal(Midnight + step * 3, fetched.End);
        Assert.Equal(
            [new(Midnight, 1000m), new(Midnight + step, 1001m), new(Midnight + step * 2, 1002m)],
            fetched.Points);
    }

    [Theory]
    [InlineData("Consumption", null, false)]
    [InlineData("Production", null, false)]
    [InlineData("CrossBorderFlow", "SE3", true)]
    public async Task Response_has_a_receiving_zone_only_for_flows(string type, string? toZone, bool expected)
    {
        var response = await Post(Series(type: type, zone: "SE4", toZone: toZone, points: 1));

        var body = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);
        Assert.Equal(expected, body!.ContainsKey("toBiddingZone"));
    }

    [Fact]
    public async Task Times_sent_with_an_offset_are_stored_and_returned_in_utc()
    {
        var oneAmInOslo = Midnight.ToOffset(TimeSpan.FromHours(1)); // 01:00+01:00 is 00:00 UTC

        var created = await Create(Series(start: oneAmInOslo, points: 2));
        var fetched = await Get(created.Id);

        Assert.Equal(TimeSpan.Zero, fetched.Start.Offset);
        Assert.Equal(Midnight, fetched.Start);
        Assert.All(fetched.Points, p => Assert.Equal(TimeSpan.Zero, p.Timestamp.Offset));
        Assert.Equal([Midnight, Midnight.AddMinutes(15)], fetched.Points.Select(p => p.Timestamp));
    }

    [Fact]
    public async Task Values_are_stored_exactly()
    {
        var body = Series(type: "CrossBorderFlow", zone: "NO2", toZone: "DK1", points: 4);
        // The full range the rules allow: 6 decimal places, 13 digits before the point.
        decimal[] values = [1234.567891m, 0.000001m, 0m, 9_999_999_999_999.999999m];
        for (var i = 0; i < values.Length; i++)
        {
            body["points"]![i]!["value"] = values[i];
        }

        var created = await Create(body);
        var fetched = await Get(created.Id);

        Assert.Equal(values, fetched.Points.Select(p => p.Value));
    }

    [Fact]
    public async Task Update_replaces_the_series_contents_and_keeps_its_id()
    {
        var created = await Create(Series(zone: "NO3", points: 4));

        var response = await Put(created.Id, Series(type: "Production", zone: "NO3", resolution: "PT1H", start: Midnight.AddHours(5), points: 2, step: TimeSpan.FromHours(1)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadSeries(response);
        var fetched = await Get(created.Id);
        AssertSameSeries(updated, fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Production", fetched.Type);
        Assert.Equal("PT1H", fetched.Resolution);
        Assert.Equal(Midnight.AddHours(5), fetched.Start);
        Assert.Equal(Midnight.AddHours(7), fetched.End);
        Assert.Equal(2, fetched.Points.Length);
    }

    [Fact]
    public async Task Update_can_change_the_kind_of_series()
    {
        var created = await Create(Series(type: "Consumption", zone: "SE2", points: 1));

        var asFlow = await ReadSeries(await Put(created.Id, Series(type: "CrossBorderFlow", zone: "SE2", toZone: "FI", points: 1)));
        Assert.Equal("CrossBorderFlow", asFlow.Type);
        Assert.Equal("FI", asFlow.ToBiddingZone);

        var asProduction = await ReadSeries(await Put(created.Id, Series(type: "Production", zone: "SE2", points: 1)));
        Assert.Equal("Production", asProduction.Type);
        Assert.Null((await Get(created.Id)).ToBiddingZone);
    }

    [Fact]
    public async Task Invalid_update_returns_400_and_keeps_the_original()
    {
        var created = await Create(Series(zone: "PL", points: 2));

        var response = await Put(created.Id, Series(zone: "XX1"));

        await AssertValidationProblem(response, "biddingZone");
        AssertSameSeries(created, await Get(created.Id));
    }

    [Fact]
    public async Task Delete_returns_204_and_the_series_is_gone()
    {
        var created = await Create(Series(zone: "NO4", points: 1));

        var response = await Delete(created.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await GetById(created.Id)).StatusCode);
        Assert.Empty(await Search());
    }

    [Fact]
    public async Task Deleting_twice_returns_404_the_second_time()
    {
        var created = await Create(Series(points: 1));

        Assert.Equal(HttpStatusCode.NoContent, (await Delete(created.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Delete(created.Id)).StatusCode);
    }

    [Fact]
    public async Task Delete_removes_only_that_series()
    {
        var kept = await Create(Series(zone: "EE", points: 1));
        var removed = await Create(Series(zone: "EE", start: Midnight.AddDays(1), points: 1));

        await Delete(removed.Id);

        Assert.Equal([kept.Id], (await Search()).Select(s => s.Id));
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

        var response = await Client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Update_of_an_unknown_id_is_404_even_when_it_breaks_business_rules()
    {
        var response = await Put(Guid.NewGuid(), Series(zone: "XX1"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    public async Task Id_that_is_not_a_guid_returns_404(string method)
    {
        var response = await Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), $"{BaseUrl}/not-a-guid"), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
