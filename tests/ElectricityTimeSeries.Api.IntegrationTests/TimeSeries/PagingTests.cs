using ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

namespace ElectricityTimeSeries.Api.IntegrationTests.TimeSeries;

/// <summary>Skip and take count points, not series, so a series can be split across pages.</summary>
public class PagingTests : TimeSeriesApiTest
{
    public PagingTests(TestStorage storage)
        : base(storage)
    {
    }

    [Fact]
    public async Task Pages_follow_on_from_each_other_and_split_series()
    {
        var first = await Create(Series(start: Midnight, points: 4));
        var second = await Create(Series(start: Midnight.AddHours(1), points: 4));

        var page1 = await SearchPage("take=3");
        var page2 = await SearchPage("skip=3&take=3");
        var page3 = await SearchPage("skip=6&take=3");
        var beyond = await SearchPage("skip=8&take=3");

        Assert.Equal([(first.Id, 3)], Shape(page1.Results));
        Assert.Equal([(first.Id, 1), (second.Id, 2)], Shape(page2.Results));
        Assert.Equal([(second.Id, 2)], Shape(page3.Results));
        Assert.Empty(beyond.Results);
        Assert.All([page1, page2, page3, beyond], page => Assert.Equal(8, page.Total));
    }

    [Fact]
    public async Task A_split_series_keeps_its_whole_start_and_end()
    {
        var created = await Create(Series(start: Midnight, points: 4));

        var (results, _) = await SearchPage("skip=1&take=2");

        var slice = Assert.Single(results);
        Assert.Equal((created.Start, created.End), (slice.Start, slice.End));
        Assert.Equal([Midnight.AddMinutes(15), Midnight.AddMinutes(30)], slice.Points.Select(p => p.Timestamp));
    }

    [Fact]
    public async Task Without_take_a_page_holds_1000_points()
    {
        await Create(Series(start: Midnight, points: 1500));

        var (results, total) = await SearchPage();

        Assert.Equal(1000, Assert.Single(results).Points.Length);
        Assert.Equal(1500, total);
    }

    [Fact]
    public async Task The_total_counts_only_points_matching_the_filters()
    {
        await Create(Series(zone: "SE3", start: Midnight, points: 8));
        await Create(Series(zone: "SE4", start: Midnight, points: 8));

        var (_, total) = await SearchPage($"biddingZone=SE3&from={Query(Midnight)}&to={Query(Midnight.AddHours(1))}&take=1");

        Assert.Equal(4, total);
    }

    private static (Guid Id, int Points)[] Shape(SeriesResult[] results) =>
        results.Select(s => (s.Id, s.Points.Length)).ToArray();
}
