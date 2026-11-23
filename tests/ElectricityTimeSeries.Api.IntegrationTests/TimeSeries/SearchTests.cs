using ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

namespace ElectricityTimeSeries.Api.IntegrationTests.TimeSeries;

public class SearchTests : TimeSeriesApiTest
{
    public SearchTests(TestStorage storage)
        : base(storage)
    {
    }

    [Fact]
    public async Task Empty_store_returns_an_empty_list()
    {
        Assert.Empty(await Search());
    }

    [Fact]
    public async Task Without_filters_returns_every_series_ordered_by_start()
    {
        var third = await Create(Series(zone: "NO1", start: Midnight.AddHours(2), points: 1));
        var first = await Create(Series(zone: "SE1", start: Midnight, points: 1));
        var second = await Create(Series(type: "CrossBorderFlow", zone: "FI", toZone: "SE1", start: Midnight.AddHours(1), points: 1));

        var results = await Search();

        Assert.Equal([first.Id, second.Id, third.Id], results.Select(s => s.Id));
    }

    [Fact]
    public async Task By_bidding_zone_includes_flows_in_both_directions()
    {
        var consumption = await Create(Series(zone: "FI", points: 1));
        var flowOut = await Create(Series(type: "CrossBorderFlow", zone: "FI", toZone: "SE1", start: Midnight.AddHours(1), points: 1));
        var flowIn = await Create(Series(type: "CrossBorderFlow", zone: "EE", toZone: "FI", start: Midnight.AddHours(2), points: 1));
        await Create(Series(zone: "SE1", points: 1));

        var results = await Search("biddingZone=FI");

        Assert.Equal([consumption.Id, flowOut.Id, flowIn.Id], results.Select(s => s.Id));
    }

    [Theory]
    [InlineData("fi")]
    [InlineData("Fi")]
    [InlineData("%20FI%20")]
    public async Task Bidding_zone_filter_ignores_case_and_spaces(string zone)
    {
        var created = await Create(Series(zone: "FI", points: 1));

        var results = await Search($"biddingZone={zone}");

        Assert.Equal([created.Id], results.Select(s => s.Id));
    }

    [Fact]
    public async Task By_type_returns_only_that_type()
    {
        var production = await Create(Series(type: "Production", zone: "DK2", points: 1));
        await Create(Series(type: "Consumption", zone: "DK2", points: 1));
        await Create(Series(type: "CrossBorderFlow", zone: "DK2", toZone: "DK1", points: 1));

        var results = await Search("type=Production");

        Assert.Equal([production.Id], results.Select(s => s.Id));
    }

    [Fact]
    public async Task By_time_interval_returns_overlapping_series_with_only_the_points_inside()
    {
        // 00:00-01:00 and 02:00-03:00, both in 15-minute steps.
        var early = await Create(Series(zone: "LT", start: Midnight, points: 4));
        await Create(Series(zone: "LT", start: Midnight.AddHours(2), points: 4));

        var results = await Search($"from={Query(Midnight.AddMinutes(30))}&to={Query(Midnight.AddHours(2))}");

        var match = Assert.Single(results);
        Assert.Equal(early.Id, match.Id);
        Assert.Equal([Midnight.AddMinutes(30), Midnight.AddMinutes(45)], match.Points.Select(p => p.Timestamp));
    }

    [Fact]
    public async Task Points_that_only_partly_overlap_the_interval_are_included()
    {
        // Points cover 00:00-00:15, 00:15-00:30, 00:30-00:45 and 00:45-01:00.
        await Create(Series(start: Midnight, points: 4));

        var results = await Search($"from={Query(Midnight.AddMinutes(20))}&to={Query(Midnight.AddMinutes(35))}");

        Assert.Equal([Midnight.AddMinutes(15), Midnight.AddMinutes(30)], Assert.Single(results).Points.Select(p => p.Timestamp));
    }

    [Fact]
    public async Task Interval_end_is_exclusive_and_start_is_inclusive()
    {
        var before = await Create(Series(zone: "NO1", start: Midnight, points: 4));             // 00:00-01:00
        var after = await Create(Series(zone: "NO2", start: Midnight.AddHours(2), points: 4));  // 02:00-03:00

        Assert.Empty(await Search($"from={Query(Midnight.AddHours(1))}&to={Query(Midnight.AddHours(2))}"));
        Assert.Equal([before.Id], (await Search($"to={Query(Midnight.AddMinutes(15))}")).Select(s => s.Id));
        Assert.Equal([after.Id], (await Search($"from={Query(Midnight.AddHours(2).AddMinutes(45))}")).Select(s => s.Id));
    }

    [Fact]
    public async Task From_alone_returns_series_with_data_after_it()
    {
        await Create(Series(zone: "NO1", start: Midnight, points: 4));
        var later = await Create(Series(zone: "NO2", start: Midnight.AddHours(2), points: 4));

        var results = await Search($"from={Query(Midnight.AddHours(1))}");

        Assert.Equal([later.Id], results.Select(s => s.Id));
        Assert.Equal(4, results[0].Points.Length);
    }

    [Fact]
    public async Task To_alone_returns_series_with_data_before_it()
    {
        var earlier = await Create(Series(zone: "NO1", start: Midnight, points: 4));
        await Create(Series(zone: "NO2", start: Midnight.AddHours(2), points: 4));

        var results = await Search($"to={Query(Midnight.AddHours(1))}");

        Assert.Equal([earlier.Id], results.Select(s => s.Id));
    }

    [Fact]
    public async Task All_filters_combine()
    {
        var match = await Create(Series(type: "Production", zone: "SE3", start: Midnight, points: 4));
        await Create(Series(type: "Consumption", zone: "SE3", start: Midnight, points: 4));            // other type
        await Create(Series(type: "Production", zone: "SE4", start: Midnight, points: 4));             // other zone
        await Create(Series(type: "Production", zone: "SE3", start: Midnight.AddDays(1), points: 4));  // other day

        var results = await Search($"biddingZone=SE3&type=Production&from={Query(Midnight)}&to={Query(Midnight.AddHours(1))}");

        Assert.Equal([match.Id], results.Select(s => s.Id));
    }

    [Fact]
    public async Task Accepts_times_with_any_offset()
    {
        var created = await Create(Series(zone: "LV", start: Midnight, points: 4));
        var oneAmInOslo = Midnight.ToOffset(TimeSpan.FromHours(1)); // 01:00+01:00 is 00:00 UTC

        var results = await Search($"from={Query(oneAmInOslo)}&to={Query(oneAmInOslo.AddMinutes(15))}");

        var match = Assert.Single(results);
        Assert.Equal(created.Id, match.Id);
        Assert.Equal(Midnight, Assert.Single(match.Points).Timestamp);
    }

    [Fact]
    public async Task Get_by_id_always_returns_every_point()
    {
        var created = await Create(Series(start: Midnight, points: 8));

        await Search($"from={Query(Midnight)}&to={Query(Midnight.AddMinutes(15))}");

        Assert.Equal(8, (await Get(created.Id)).Points.Length);
    }

    [Theory]
    [InlineData("biddingZone=XX1", "biddingZone")]
    [InlineData("type=Nuclear", "type")]
    [InlineData("from=2025-11-22T02:00:00Z&to=2025-11-22T01:00:00Z", "from")]
    [InlineData("from=2025-11-22T01:00:00Z&to=2025-11-22T01:00:00Z", "from")]
    [InlineData("from=not-a-date", "from")]
    [InlineData("skip=-1", "skip")]
    [InlineData("take=0", "take")]
    [InlineData("take=1001", "take")]
    [InlineData("take=lots", "take")]
    public async Task Invalid_search_returns_400_validation_problem(string query, string expectedField)
    {
        var response = await Client.GetAsync($"{BaseUrl}?{query}", Ct);

        await AssertValidationProblem(response, expectedField);
    }

    // Points cover 00:00-00:15, 00:15-00:30, 00:30-00:45 and 00:45-01:00; times are seconds after midnight.
    [Theory]
    [InlineData(null, null, 4)]
    [InlineData(0, 3600, 4)]
    [InlineData(900, 2700, 2)]
    [InlineData(899, 2700, 3)]   // a second before 00:15 still falls in the first point
    [InlineData(900, 2701, 3)]   // a second after 00:45 already falls in the last point
    [InlineData(3600, 7200, 0)]
    [InlineData(null, 1800, 2)]
    [InlineData(1800, null, 2)]
    public async Task A_point_matches_when_its_period_overlaps_the_interval(int? fromSeconds, int? toSeconds, int expected)
    {
        await Create(Series(start: Midnight, points: 4));
        var query = string.Join('&', new[]
        {
            fromSeconds is { } from ? $"from={Query(Midnight.AddSeconds(from))}" : null,
            toSeconds is { } to ? $"to={Query(Midnight.AddSeconds(to))}" : null,
        }.OfType<string>());

        var (results, total) = await SearchPage(query);

        Assert.Equal(expected, results.Sum(s => s.Points.Length));
        Assert.Equal(expected, total);
    }
}
