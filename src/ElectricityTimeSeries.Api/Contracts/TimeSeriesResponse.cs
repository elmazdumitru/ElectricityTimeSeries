using ElectricityTimeSeries.Core.Domain;

namespace ElectricityTimeSeries.Api.Contracts;

/// <summary>Times are in UTC.</summary>
public sealed record TimeSeriesResponse(
    Guid Id,
    TimeSeriesType Type,
    BiddingZone BiddingZone,
    BiddingZone? ToBiddingZone,
    TimeSpan Resolution,
    DateTimeOffset Start,
    DateTimeOffset End,
    IReadOnlyList<TimeSeriesPointResponse> Points)
{
    public static TimeSeriesResponse From(TimeSeries series) => From(series, series.Points);

    public static TimeSeriesResponse From(TimeSeries series, IReadOnlyList<TimeSeriesPoint> points) => new(
        series.Id,
        series.Type,
        series.BiddingZone,
        series.ToBiddingZone,
        series.Resolution,
        series.Start,
        series.End,
        points.Select(p => new TimeSeriesPointResponse(p.Timestamp, p.Value)).ToList());
}

public sealed record TimeSeriesPointResponse(DateTimeOffset Timestamp, decimal Value);
