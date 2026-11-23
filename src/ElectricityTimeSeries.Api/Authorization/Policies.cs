namespace ElectricityTimeSeries.Api.Authorization;

/// <summary>Policy names for [Authorize]; their rules are in <see cref="AuthorizationExtensions"/>.</summary>
public static class Policies
{
    public const string CanReadTimeSeries = nameof(CanReadTimeSeries);
    public const string CanWriteTimeSeries = nameof(CanWriteTimeSeries);
}
