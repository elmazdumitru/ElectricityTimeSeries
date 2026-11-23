using ElectricityTimeSeries.Core.Domain;

namespace ElectricityTimeSeries.Core.Abstractions;

/// <summary>A zone also matches flows into or out of it. From is inclusive, To is exclusive.</summary>
public sealed record TimeSeriesFilter(
    BiddingZone? BiddingZone = null,
    TimeSeriesType? Type = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);
