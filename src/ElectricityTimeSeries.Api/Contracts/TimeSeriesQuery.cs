using ElectricityTimeSeries.Core.Abstractions;
using ElectricityTimeSeries.Core.Domain;
using ElectricityTimeSeries.Core.Services;

namespace ElectricityTimeSeries.Api.Contracts;

public sealed class TimeSeriesQuery
{
    /// <summary>Series in this zone, including flows into or out of it.</summary>
    public BiddingZone? BiddingZone { get; init; }

    public TimeSeriesType? Type { get; init; }

    /// <summary>Start of the time interval (inclusive). Give a time zone; without one it's read as the server's local time.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>End of the time interval (exclusive). Give a time zone; without one it's read as the server's local time.</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>How many matching points to skip. Counts points, not series.</summary>
    public int Skip { get; init; }

    /// <summary>How many points to return, at most 1000 (the default). Fewer means the last page.</summary>
    public int Take { get; init; } = TimeSeriesService.MaxPageSize;

    public TimeSeriesFilter ToFilter() => new(BiddingZone, Type, From, To);
}
