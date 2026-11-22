namespace ElectricityTimeSeries.Api.Authorization;

/// <summary>
/// Names of the API's authorization policies, referenced by controllers via
/// <c>[Authorize(Policy = ...)]</c>. Their rules are defined in <see cref="AuthorizationExtensions"/>.
/// </summary>
public static class Policies
{
    public const string CanReadTimeSeries = nameof(CanReadTimeSeries);
    public const string CanWriteTimeSeries = nameof(CanWriteTimeSeries);
}
