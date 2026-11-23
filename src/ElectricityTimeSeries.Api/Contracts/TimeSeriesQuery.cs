using ElectricityTimeSeries.Core.Abstractions;
using ElectricityTimeSeries.Core.Domain;

namespace ElectricityTimeSeries.Api.Contracts;

public sealed class TimeSeriesQuery
{
    /// <summary>Series in this zone, including flows into or out of it.</summary>
    public BiddingZone? BiddingZone { get; init; }

    public TimeSeriesType? Type { get; init; }

    /// <summary>Start of the time interval (inclusive). Must include a time zone.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>End of the time interval (exclusive). Must include a time zone.</summary>
    public DateTimeOffset? To { get; init; }

    public TimeSeriesFilter ToFilter() => new(BiddingZone, Type, From, To);
}
