using ElectricityTimeSeries.Core.Domain;

namespace ElectricityTimeSeries.Core.Abstractions;

/// <summary>
/// Search criteria. Every criterion is optional.
/// </summary>
/// <param name="BiddingZone">Matches series in this zone, and flows into or out of it.</param>
/// <param name="From">Matches series with data after this time (inclusive).</param>
/// <param name="To">Matches series with data before this time (exclusive).</param>
public sealed record TimeSeriesFilter(
    string? BiddingZone = null,
    TimeSeriesType? Type = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);
