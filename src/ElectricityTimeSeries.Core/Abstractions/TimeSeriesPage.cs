using ElectricityTimeSeries.Core.Domain;

namespace ElectricityTimeSeries.Core.Abstractions;

/// <summary>One page of matching points, grouped by series in search order, and how many points match in all.
/// A series can be split across pages.</summary>
public sealed record TimeSeriesPage(IReadOnlyList<TimeSeriesSlice> Series, int TotalPoints);

/// <summary>A series' details with only its points on this page.</summary>
public sealed record TimeSeriesSlice(
    Guid Id,
    TimeSeriesType Type,
    BiddingZone BiddingZone,
    BiddingZone? ToBiddingZone,
    TimeSpan Resolution,
    DateTimeOffset Start,
    DateTimeOffset End,
    IReadOnlyList<TimeSeriesPoint> Points);
