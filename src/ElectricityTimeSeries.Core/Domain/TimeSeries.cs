namespace ElectricityTimeSeries.Core.Domain;

/// <summary>
/// A block of evenly spaced values for one bidding zone (or, for flows, a pair of zones),
/// for example "consumption in NO1 on 22 November, every 15 minutes".
/// A time series is always valid: it can only be created or changed through
/// <see cref="Create"/> and <see cref="Update"/>, which check the business rules.
/// </summary>
public sealed class TimeSeries
{
    private readonly List<TimeSeriesPoint> _points = [];

    // Used by the storage layer when loading saved data.
    private TimeSeries()
    {
    }

    public Guid Id { get; private set; }

    public TimeSeriesType Type { get; private set; }

    public BiddingZone BiddingZone { get; private set; } = null!;

    /// <summary>The receiving zone of a cross-border flow; empty for production and consumption.</summary>
    public BiddingZone? ToBiddingZone { get; private set; }

    /// <summary>How long each point lasts.</summary>
    public TimeSpan Resolution { get; private set; }

    /// <summary>When the first point starts (UTC).</summary>
    public DateTimeOffset Start { get; private set; }

    /// <summary>When the last point ends (UTC).</summary>
    public DateTimeOffset End { get; private set; }

    public IReadOnlyList<TimeSeriesPoint> Points => _points.OrderBy(p => p.Timestamp).ToList();

    /// <exception cref="DomainValidationException">The definition breaks a business rule.</exception>
    public static TimeSeries Create(TimeSeriesDefinition definition)
    {
        var series = new TimeSeries { Id = Guid.CreateVersion7() };
        series.Apply(definition);
        return series;
    }

    /// <summary>Replaces the whole contents of the series, keeping its id.</summary>
    /// <exception cref="DomainValidationException">The definition breaks a business rule.</exception>
    public void Update(TimeSeriesDefinition definition) => Apply(definition);

    /// <summary>
    /// Returns the points whose period overlaps the interval from <paramref name="from"/> (inclusive)
    /// to <paramref name="to"/> (exclusive). A missing bound means "no limit".
    /// </summary>
    public IReadOnlyList<TimeSeriesPoint> PointsWithin(DateTimeOffset? from, DateTimeOffset? to) =>
        Points
            .Where(p => (from is null || p.Timestamp + Resolution > from) && (to is null || p.Timestamp < to))
            .ToList();

    private void Apply(TimeSeriesDefinition definition)
    {
        TimeSeriesRules.Validate(definition);

        Type = definition.Type;
        BiddingZone = definition.BiddingZone;
        ToBiddingZone = definition.ToBiddingZone;
        Resolution = definition.Resolution;

        _points.Clear();
        _points.AddRange(definition.Points.Select(p => p with { Timestamp = p.Timestamp.ToUniversalTime() }));

        Start = _points[0].Timestamp;
        End = _points[^1].Timestamp + Resolution;
    }
}
