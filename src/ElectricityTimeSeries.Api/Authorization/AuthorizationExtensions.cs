using ElectricityTimeSeries.Api.Authorization.Enforcement;
using Microsoft.AspNetCore.Authorization;

namespace ElectricityTimeSeries.Api.Authorization;

public static class AuthorizationExtensions
{
    /// <summary>Bearer options (authority, audiences, keys) are read from the "Authentication:Schemes:Bearer" section.</summary>
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication().AddJwtBearer(options => options.EventsType = typeof(PolicyEnforcingJwtBearerEvents));
        services.AddTransient<PolicyEnforcingJwtBearerEvents>();
        services.AddOptions<AccessPolicyOptions>().BindConfiguration(AccessPolicyOptions.Section);
        services.AddSingleton<IPolicyEnforcementPoint, FileRevocationLedger>();

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
