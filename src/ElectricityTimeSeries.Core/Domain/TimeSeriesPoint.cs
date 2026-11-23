namespace ElectricityTimeSeries.Core.Domain;

/// <summary>A value in MW for the period that starts at Timestamp and lasts one resolution step.</summary>
public sealed record TimeSeriesPoint(DateTimeOffset Timestamp, decimal Value);
