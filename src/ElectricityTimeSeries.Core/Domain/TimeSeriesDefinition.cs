namespace ElectricityTimeSeries.Core.Domain;

/// <summary>Everything needed to create a time series or replace its contents.</summary>
/// <param name="ToBiddingZone">The receiving zone. Only used for <see cref="TimeSeriesType.CrossBorderFlow"/>.</param>
public sealed record TimeSeriesDefinition(
    TimeSeriesType Type,
    string BiddingZone,
    string? ToBiddingZone,
    TimeSpan Resolution,
    IReadOnlyList<TimeSeriesPoint> Points);
