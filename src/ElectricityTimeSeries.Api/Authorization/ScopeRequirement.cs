using Microsoft.AspNetCore.Authorization;

namespace ElectricityTimeSeries.Api.Authorization;

public sealed class ScopeRequirement(string scope) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;
}
