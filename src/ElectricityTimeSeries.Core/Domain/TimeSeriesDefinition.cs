namespace ElectricityTimeSeries.Core.Domain;

/// <summary>Each kind holds only its own fields, so only a flow can have (and must have) a receiving zone.</summary>
public union TimeSeriesDefinition(ProductionDefinition, ConsumptionDefinition, CrossBorderFlowDefinition)
{
    public TimeSeriesType Type => this switch
    {
        ProductionDefinition => TimeSeriesType.Production,
        ConsumptionDefinition => TimeSeriesType.Consumption,
        CrossBorderFlowDefinition => TimeSeriesType.CrossBorderFlow,
    };

    public TimeSeriesContent Content => this switch
    {
        ProductionDefinition production => production,
        ConsumptionDefinition consumption => consumption,
        CrossBorderFlowDefinition flow => flow,
    };
}

public abstract record TimeSeriesContent(
    BiddingZone BiddingZone,
    TimeSpan Resolution,
    IReadOnlyList<TimeSeriesPoint> Points);

public sealed record ProductionDefinition(
    BiddingZone BiddingZone,
    TimeSpan Resolution,
    IReadOnlyList<TimeSeriesPoint> Points)
    : TimeSeriesContent(BiddingZone, Resolution, Points);

public sealed record ConsumptionDefinition(
    BiddingZone BiddingZone,
    TimeSpan Resolution,
    IReadOnlyList<TimeSeriesPoint> Points)
    : TimeSeriesContent(BiddingZone, Resolution, Points);

/// <summary>Electricity flowing from <paramref name="BiddingZone"/> to <paramref name="ToBiddingZone"/>.</summary>
public sealed record CrossBorderFlowDefinition(
    BiddingZone BiddingZone,
    BiddingZone ToBiddingZone,
    TimeSpan Resolution,
    IReadOnlyList<TimeSeriesPoint> Points)
    : TimeSeriesContent(BiddingZone, Resolution, Points);
