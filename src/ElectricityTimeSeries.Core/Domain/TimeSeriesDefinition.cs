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

public abstract record TimeSeriesContent
{
    public required BiddingZone BiddingZone { get; init; }

    public required TimeSpan Resolution { get; init; }

    public required IReadOnlyList<TimeSeriesPoint> Points { get; init; }
}

public sealed record ProductionDefinition : TimeSeriesContent;

public sealed record ConsumptionDefinition : TimeSeriesContent;

/// <summary>Electricity flowing from <see cref="TimeSeriesContent.BiddingZone"/> to <see cref="ToBiddingZone"/>.</summary>
public sealed record CrossBorderFlowDefinition : TimeSeriesContent
{
    public required BiddingZone ToBiddingZone { get; init; }
}
