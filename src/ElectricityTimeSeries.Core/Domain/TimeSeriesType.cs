namespace ElectricityTimeSeries.Core.Domain;

/// <summary>What a time series measures.</summary>
public enum TimeSeriesType
{
    /// <summary>Electricity produced in a bidding zone.</summary>
    Production,

    /// <summary>Electricity consumed in a bidding zone.</summary>
    Consumption,

    /// <summary>Electricity flowing from one bidding zone to another.</summary>
    CrossBorderFlow,
}
