using Microsoft.AspNetCore.Authorization;

namespace ElectricityTimeSeries.Api.Authorization;

/// <summary>
/// Succeeds a <see cref="ScopeRequirement"/> when the user's token contains the required scope.
/// </summary>
public sealed class ScopeAuthorizationHandler(ILogger<ScopeAuthorizationHandler> logger)
    : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        if (context.User.HasScope(requirement.Scope))
        {
            context.Succeed(requirement);
        }
        else
        {
            // No context.Fail(): another handler for the same requirement could still satisfy it.
            logger.LogDebug("Access token is missing required scope {Scope}.", requirement.Scope);
        }

        return Task.CompletedTask;
    }
}
