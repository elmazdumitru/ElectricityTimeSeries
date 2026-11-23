using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json.Serialization;
using ElectricityTimeSeries.Core.Domain;

namespace ElectricityTimeSeries.Api.Contracts;

// Fields are nullable so [Required] can report a missing one by name, instead of it silently becoming 0.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ProductionRequest), nameof(TimeSeriesType.Production))]
[JsonDerivedType(typeof(ConsumptionRequest), nameof(TimeSeriesType.Consumption))]
[JsonDerivedType(typeof(CrossBorderFlowRequest), nameof(TimeSeriesType.CrossBorderFlow))]
public closed class TimeSeriesRequest
{
    [Required]
    public BiddingZone? BiddingZone { get; init; }

    /// <summary>How long each point lasts, as an ISO 8601 duration: PT15M or PT1H.</summary>
    [Required]
    public TimeSpan? Resolution { get; init; }

    [Required]
    public IReadOnlyList<TimeSeriesPointRequest>? Points { get; init; }

    /// <summary>Call only after validation has passed.</summary>
    public TimeSeriesDefinition ToDefinition() => this switch
    {
        ProductionRequest (var zone, var resolution, var points) => new ProductionDefinition(zone, resolution, points),
        ConsumptionRequest (var zone, var resolution, var points) => new ConsumptionDefinition(zone, resolution, points),
        CrossBorderFlowRequest (var zone, var toZone, var resolution, var points) => new CrossBorderFlowDefinition(zone, toZone, resolution, points),
    };

    internal void Deconstruct(out BiddingZone biddingZone, out TimeSpan resolution, out IReadOnlyList<TimeSeriesPoint> points)
    {
        biddingZone = BiddingZone ?? throw NotValidated(nameof(BiddingZone));
        resolution = Resolution ?? throw NotValidated(nameof(Resolution));
        points = (Points ?? throw NotValidated(nameof(Points)))
            .Select(p => new TimeSeriesPoint(
                p.Timestamp ?? throw NotValidated(nameof(p.Timestamp)),
                p.Value ?? throw NotValidated(nameof(p.Value))))
            .ToList();
    }

    private protected static UnreachableException NotValidated(string field) =>
        new($"'{field}' is missing; the request should have been rejected by validation.");
}

/// <summary>A receiving zone is rejected as an unknown field.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ProductionRequest : TimeSeriesRequest;

/// <summary>A receiving zone is rejected as an unknown field.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ConsumptionRequest : TimeSeriesRequest;

/// <summary>A flow from <see cref="TimeSeriesRequest.BiddingZone"/> to <see cref="ToBiddingZone"/>.</summary>
public sealed class CrossBorderFlowRequest : TimeSeriesRequest
{
    [Required]
    public BiddingZone? ToBiddingZone { get; init; }

    internal void Deconstruct(out BiddingZone biddingZone, out BiddingZone toBiddingZone, out TimeSpan resolution, out IReadOnlyList<TimeSeriesPoint> points)
    {
        Deconstruct(out biddingZone, out resolution, out points);
        toBiddingZone = ToBiddingZone ?? throw NotValidated(nameof(ToBiddingZone));
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
