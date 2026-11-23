using System.Text.Json.Serialization;
using ElectricityTimeSeries.Api.Json;
using ElectricityTimeSeries.Core.Abstractions;
using ElectricityTimeSeries.Core.Domain;

namespace ElectricityTimeSeries.Api.Contracts;

/// <summary>Times are in UTC.</summary>
public sealed record TimeSeriesResponse(
    Guid Id,
    TimeSeriesType Type,
    BiddingZone BiddingZone,
    BiddingZone? ToBiddingZone,
    [property: JsonConverter(typeof(IsoDurationJsonConverter))] TimeSpan Resolution,
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
        ToResponses(points));

    /// <summary>A search result: the series with only its points on this page. Start and End are the whole series'.</summary>
    public static TimeSeriesResponse From(TimeSeriesSlice slice) => new(
        slice.Id,
        slice.Type,
        slice.BiddingZone,
        slice.ToBiddingZone,
        slice.Resolution,
        slice.Start,
        slice.End,
        ToResponses(slice.Points));

    private static List<TimeSeriesPointResponse> ToResponses(IEnumerable<TimeSeriesPoint> points) =>
        points.Select(p => new TimeSeriesPointResponse(p.Timestamp, p.Value)).ToList();
}

public sealed record TimeSeriesPointResponse(DateTimeOffset Timestamp, decimal Value);
