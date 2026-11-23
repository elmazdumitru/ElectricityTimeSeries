namespace ElectricityTimeSeries.Core.Domain;

/// <summary>Evenly spaced values for one bidding zone, or a pair of zones for a flow.
/// Always valid: only <see cref="Create"/> and <see cref="Update"/> can set it, and both check the rules.</summary>
public sealed class TimeSeries
{
    private readonly List<TimeSeriesPoint> _points = [];

    // Used by the storage layer when loading saved data.
    private TimeSeries()
    {
    }

    public Guid Id { get; private set; }

    public TimeSeriesType Type { get; private set; }

    public BiddingZone BiddingZone { get; private set; }

    /// <summary>Only set for cross-border flows.</summary>
    public BiddingZone? ToBiddingZone { get; private set; }

    /// <summary>How long each point lasts.</summary>
    public TimeSpan Resolution { get; private set; }

    /// <summary>When the first point starts (UTC).</summary>
    public DateTimeOffset Start { get; private set; }

    /// <summary>When the last point ends (UTC).</summary>
    public DateTimeOffset End { get; private set; }

    public IReadOnlyList<TimeSeriesPoint> Points => _points.OrderBy(p => p.Timestamp).ToList();

    public static TimeSeries Create(TimeSeriesDefinition definition)
    {
        var series = new TimeSeries { Id = Guid.CreateVersion7() };
        series.Apply(definition);
        return series;
    }

    /// <summary>Replaces the whole contents of the series, keeping its id.</summary>
    public void Update(TimeSeriesDefinition definition) => Apply(definition);

    private void Apply(TimeSeriesDefinition definition)
    {
        TimeSeriesRules.Validate(definition);

        var content = definition.Content;

        Type = definition.Type;
        BiddingZone = content.BiddingZone;
        ToBiddingZone = definition switch
        {
            CrossBorderFlowDefinition flow => flow.ToBiddingZone,
            ProductionDefinition or ConsumptionDefinition => null,
        };
        Resolution = content.Resolution;

        _points.Clear();
        _points.AddRange(content.Points.Select(p => p with { Timestamp = p.Timestamp.ToUniversalTime() }));

        Start = _points[0].Timestamp;
        End = _points[^1].Timestamp + Resolution;
    }
}
