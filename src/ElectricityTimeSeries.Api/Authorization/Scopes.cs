namespace ElectricityTimeSeries.Api.Authorization;

/// <summary>
/// Scopes issued by the identity provider, exactly as they appear in access tokens.
/// These are a contract with the identity provider; policies (see <see cref="Policies"/>)
/// decide which scopes an operation requires.
/// </summary>
public static class Scopes
{
    public const string TimeSeriesRead = "TimeSeries.Read";
    public const string TimeSeriesWrite = "TimeSeries.Write";
}
