using Microsoft.AspNetCore.Authorization;

namespace ElectricityTimeSeries.Api.Authorization;

public static class AuthorizationExtensions
{
    /// <summary>
    /// Adds JWT bearer authentication and the API's authorization policies.
    /// Bearer options (Authority, ValidAudiences, ValidIssuer, signing keys) are bound
    /// from the "Authentication:Schemes:Bearer" configuration section.
    /// </summary>
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication().AddJwtBearer();

        services.AddSingleton<IAuthorizationHandler, ScopeAuthorizationHandler>();

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.CanReadTimeSeries, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new ScopeRequirement(Scopes.TimeSeriesRead)))
            .AddPolicy(Policies.CanWriteTimeSeries, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new ScopeRequirement(Scopes.TimeSeriesWrite)))
            // Every endpoint requires an authenticated caller unless it explicitly opts out.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        return services;
    }
}
