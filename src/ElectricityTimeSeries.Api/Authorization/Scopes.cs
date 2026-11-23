namespace ElectricityTimeSeries.Api.Authorization;

/// <summary>Scope names exactly as the identity provider puts them in access tokens.</summary>
public static class Scopes
{
    public const string TimeSeriesRead = "TimeSeries.Read";
    public const string TimeSeriesWrite = "TimeSeries.Write";
}
