namespace ElectricityTimeSeries.Core.Domain;

/// <summary>
/// One value of a time series. It covers the period from <see cref="Timestamp"/> to
/// <see cref="Timestamp"/> plus the series' resolution, and is expressed in MW.
/// </summary>
public sealed record TimeSeriesPoint(DateTimeOffset Timestamp, decimal Value);
