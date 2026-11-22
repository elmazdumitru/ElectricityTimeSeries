using Microsoft.AspNetCore.Authorization;

namespace ElectricityTimeSeries.Api.Authorization;

/// <summary>
/// Requires the caller's access token to contain the given scope.
/// Evaluated by <see cref="ScopeAuthorizationHandler"/>.
/// </summary>
public sealed class ScopeRequirement(string scope) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;
}
