using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ElectricityTimeSeries.Api.Json;
using ElectricityTimeSeries.Core.Domain;

namespace ElectricityTimeSeries.Api.Contracts;

/// <summary>The body of a create or update request.</summary>
/// <remarks>
/// Fields are nullable so a missing field is reported as "required" instead of silently
/// becoming a default value such as 0. Business rules are checked by the Core layer.
/// </remarks>
public sealed class TimeSeriesRequest
{
    [Required]
    public TimeSeriesType? Type { get; init; }

    /// <example>NO1</example>
    [Required]
    public string? BiddingZone { get; init; }

    /// <summary>The receiving zone. Only for cross-border flows.</summary>
    public string? ToBiddingZone { get; init; }

    /// <summary>How long each point lasts, as an ISO 8601 duration: PT15M or PT1H.</summary>
    /// <example>PT15M</example>
    [Required]
    [JsonConverter(typeof(IsoDurationJsonConverter))]
    public TimeSpan? Resolution { get; init; }

    [Required]
    public IReadOnlyList<TimeSeriesPointRequest>? Points { get; init; }

    public TimeSeriesDefinition ToDefinition() => new(
        Type!.Value,
        BiddingZone!,
        ToBiddingZone,
        Resolution!.Value,
        Points!.Select(p => new TimeSeriesPoint(p.Timestamp!.Value, p.Value!.Value)).ToList());
}

public sealed class TimeSeriesPointRequest
{
    /// <summary>When the period starts. Must include a time zone, for example 2025-11-22T10:00:00Z.</summary>
    [Required]
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>The value in MW.</summary>
    [Required]
    public decimal? Value { get; init; }
}
