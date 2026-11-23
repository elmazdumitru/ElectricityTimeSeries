using ElectricityTimeSeries.Core.Domain;

namespace ElectricityTimeSeries.UnitTests.Domain;

public class TimeSeriesTests
{
    private static readonly DateTimeOffset Midnight = new(2025, 11, 22, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan FifteenMinutes = TimeSpan.FromMinutes(15);

    [Fact]
    public void Create_sets_start_end_and_normalizes_zone_and_times()
    {
        var localMidnight = Midnight.ToOffset(TimeSpan.FromHours(1));
        var definition = Definition(zone: " no1 ", points: Points(localMidnight, 4));

        var series = TimeSeries.Create(definition);

        Assert.NotEqual(Guid.Empty, series.Id);
        Assert.Equal("NO1", series.BiddingZone.ToString());
        Assert.Equal(Midnight, series.Start);
        Assert.Equal(TimeSpan.Zero, series.Start.Offset);
        Assert.Equal(Midnight.AddHours(1), series.End);
        Assert.All(series.Points, p => Assert.Equal(TimeSpan.Zero, p.Timestamp.Offset));
    }

    public static TheoryData<string, TimeSeriesDefinition> InvalidDefinitions => new()
    {
        // A flow without a receiving zone, or consumption with one, can't be built at all, so isn't listed.
        { "biddingZone", Definition(zone: "XX1") },
        { "toBiddingZone", Definition(TimeSeriesType.CrossBorderFlow, "NO1", toZone: "no1") },
        { "toBiddingZone", Definition(TimeSeriesType.CrossBorderFlow, "NO1", toZone: "XX1") },
        { "resolution", Definition(resolution: TimeSpan.FromMinutes(30)) },
        { "points[0].value", Definition(TimeSeriesType.Production, points: [new TimeSeriesPoint(Midnight, -1m)]) },
        { "points[0].value", Definition(points: [new TimeSeriesPoint(Midnight, 1.0000001m)]) },
        { "points[0].value", Definition(TimeSeriesType.CrossBorderFlow, toZone: "SE3", points: [new TimeSeriesPoint(Midnight, 10_000_000_000_000m)]) },
        { "points[0].value", Definition(TimeSeriesType.CrossBorderFlow, toZone: "SE3", points: [new TimeSeriesPoint(Midnight, -1m)]) },
        { "points", Definition(points: []) },
        { "points[0].value", Definition(points: [new TimeSeriesPoint(Midnight, -1m)]) },
        { "points[0].timestamp", Definition(points: [new TimeSeriesPoint(Midnight.AddMinutes(7), 1m)]) },
        { "points[1].timestamp", Definition(points: [new(Midnight, 1m), new(Midnight.AddMinutes(30), 1m)]) },
        { "points[1].timestamp", Definition(points: [new(Midnight, 1m), new(Midnight, 1m)]) },
        { "points[1].timestamp", Definition(points: [new(Midnight.AddMinutes(15), 1m), new(Midnight, 1m)]) },
    };

    [Theory]
    [MemberData(nameof(InvalidDefinitions))]
    public void Create_rejects_definitions_that_break_a_rule(string expectedField, TimeSeriesDefinition definition)
    {
        var exception = Assert.Throws<DomainValidationException>(() => TimeSeries.Create(definition));

        Assert.Contains(expectedField, exception.Errors.Keys);
    }

    [Theory]
    [InlineData(TimeSeriesType.Production, null)]
    [InlineData(TimeSeriesType.Consumption, null)]
    [InlineData(TimeSeriesType.CrossBorderFlow, "SE3")]
    public void Create_takes_type_and_receiving_zone_from_the_kind_of_definition(TimeSeriesType type, string? expectedToZone)
    {
        var series = TimeSeries.Create(Definition(type, "NO1", toZone: expectedToZone));

        Assert.Equal(type, series.Type);
        Assert.Equal(expectedToZone, series.ToBiddingZone?.ToString());
    }

    [Fact]
    public void Create_rejects_an_empty_definition_as_a_programming_mistake()
    {
        Assert.Throws<ArgumentException>(() => TimeSeries.Create(default));
    }

    [Fact]
    public void Create_reports_at_most_fifty_point_errors()
    {
        var points = Enumerable.Range(0, 100).Select(i => new TimeSeriesPoint(Midnight.AddMinutes(15 * i), -1m)).ToList();

        var exception = Assert.Throws<DomainValidationException>(() => TimeSeries.Create(Definition(points: points)));

        Assert.Equal(50, exception.Errors.Count);
    }

    [Fact]
    public void Update_replaces_contents_and_keeps_id()
    {
        var series = TimeSeries.Create(Definition());
        var id = series.Id;

        series.Update(Definition(TimeSeriesType.Production, "SE3", resolution: TimeSpan.FromHours(1),
            points: Points(Midnight, 2, TimeSpan.FromHours(1))));

        Assert.Equal(id, series.Id);
        Assert.Equal(TimeSeriesType.Production, series.Type);
        Assert.Equal("SE3", series.BiddingZone.ToString());
        Assert.Equal(2, series.Points.Count);
        Assert.Equal(Midnight.AddHours(2), series.End);
    }

    [Fact]
    public void Update_with_invalid_definition_leaves_series_unchanged()
    {
        var series = TimeSeries.Create(Definition());

        Assert.Throws<DomainValidationException>(() => series.Update(Definition(zone: "XX1")));

        Assert.Equal("NO1", series.BiddingZone.ToString());
        Assert.Equal(4, series.Points.Count);
    }

    [Theory]
    [InlineData(null, null, 4)]
    [InlineData(0, 60, 4)]
    [InlineData(15, 45, 2)]
    [InlineData(20, 40, 2)] // Points that only partly overlap the interval are included.
    [InlineData(60, 120, 0)]
    [InlineData(null, 30, 2)]
    [InlineData(30, null, 2)]
    public void PointsWithin_returns_points_overlapping_the_interval(int? fromMinutes, int? toMinutes, int expectedCount)
    {
        var series = TimeSeries.Create(Definition());
        DateTimeOffset? from = fromMinutes is null ? null : Midnight.AddMinutes(fromMinutes.Value);
        DateTimeOffset? to = toMinutes is null ? null : Midnight.AddMinutes(toMinutes.Value);

        var points = series.PointsWithin(from, to);

        Assert.Equal(expectedCount, points.Count);
    }

    /// <summary>Defaults to a valid 4-point consumption series in NO1.</summary>
    private static TimeSeriesDefinition Definition(
        TimeSeriesType type = TimeSeriesType.Consumption,
        string zone = "NO1",
        string? toZone = null,
        TimeSpan? resolution = null,
        IReadOnlyList<TimeSeriesPoint>? points = null)
    {
        var biddingZone = BiddingZone.From(zone);
        var step = resolution ?? FifteenMinutes;
        var values = points ?? Points(Midnight, 4);

        return type switch
        {
            TimeSeriesType.Production =>
                new ProductionDefinition { BiddingZone = biddingZone, Resolution = step, Points = values },
            TimeSeriesType.Consumption =>
                new ConsumptionDefinition { BiddingZone = biddingZone, Resolution = step, Points = values },
            TimeSeriesType.CrossBorderFlow => new CrossBorderFlowDefinition
            {
                BiddingZone = biddingZone,
                ToBiddingZone = BiddingZone.From(toZone ?? "SE3"),
                Resolution = step,
                Points = values,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    private static List<TimeSeriesPoint> Points(DateTimeOffset start, int count, TimeSpan? step = null) =>
        Enumerable.Range(0, count)
            .Select(i => new TimeSeriesPoint(start + (step ?? FifteenMinutes) * i, 1000m + i))
            .ToList();
}
