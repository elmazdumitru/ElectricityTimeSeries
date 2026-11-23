using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json.Serialization;
using ElectricityTimeSeries.Api.Json;
using ElectricityTimeSeries.Core.Domain;

namespace ElectricityTimeSeries.Api.Contracts;

// Fields are nullable so [Required] can report a missing one by name, instead of it silently becoming 0.
public sealed class TimeSeriesRequest
{
    [Required]
    public TimeSeriesType? Type { get; init; }

    /// <summary>For a flow, the zone the electricity leaves.</summary>
    [Required]
    public BiddingZone? BiddingZone { get; init; }

    /// <summary>For a cross-border flow only: the zone the electricity goes to.</summary>
    [ReceivingZone]
    public BiddingZone? ToBiddingZone { get; init; }

    /// <summary>How long each point lasts, as an ISO 8601 duration: PT15M or PT1H.</summary>
    [Required]
    [JsonConverter(typeof(IsoDurationJsonConverter))]
    public TimeSpan? Resolution { get; init; }

    [Required]
    public IReadOnlyList<TimeSeriesPointRequest>? Points { get; init; }

    /// <summary>Call only after validation has passed.</summary>
    public TimeSeriesDefinition ToDefinition()
    {
        var zone = BiddingZone ?? throw NotValidated(nameof(BiddingZone));
        var resolution = Resolution ?? throw NotValidated(nameof(Resolution));
        var points = (Points ?? throw NotValidated(nameof(Points)))
            .Select(p => new TimeSeriesPoint(
                p.Timestamp ?? throw NotValidated(nameof(p.Timestamp)),
                p.Value ?? throw NotValidated(nameof(p.Value))))
            .ToList();

        return Type switch
        {
            TimeSeriesType.Production =>
                new ProductionDefinition { BiddingZone = zone, Resolution = resolution, Points = points },
            TimeSeriesType.Consumption =>
                new ConsumptionDefinition { BiddingZone = zone, Resolution = resolution, Points = points },
            TimeSeriesType.CrossBorderFlow => new CrossBorderFlowDefinition
            {
                BiddingZone = zone,
                ToBiddingZone = ToBiddingZone ?? throw NotValidated(nameof(ToBiddingZone)),
                Resolution = resolution,
                Points = points,
            },
            _ => throw NotValidated(nameof(Type)),
        };
    }

    private static UnreachableException NotValidated(string field) =>
        new($"'{field}' is missing; the request should have been rejected by validation.");

    /// <summary>A flow needs a receiving zone, and the other types can't have one.</summary>
    private sealed class ReceivingZoneAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext context) =>
            (((TimeSeriesRequest)context.ObjectInstance).Type, value) switch
            {
                (TimeSeriesType.CrossBorderFlow, null) =>
                    new($"The {context.DisplayName} field is required for a {TimeSeriesType.CrossBorderFlow}."),
                (TimeSeriesType.Production or TimeSeriesType.Consumption, not null) =>
                    new($"Only a {TimeSeriesType.CrossBorderFlow} has a {context.DisplayName}."),
                _ => ValidationResult.Success,
            };
    }
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
