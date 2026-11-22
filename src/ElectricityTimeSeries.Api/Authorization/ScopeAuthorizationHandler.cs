using Microsoft.AspNetCore.Authorization;

namespace ElectricityTimeSeries.Api.Authorization;

/// <summary>
/// Succeeds a <see cref="ScopeRequirement"/> when the user's token contains the required scope.
/// </summary>
public sealed class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
{
    private readonly ILogger<ScopeAuthorizationHandler> _logger;

    public ScopeAuthorizationHandler(ILogger<ScopeAuthorizationHandler> logger)
    {
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        if (context.User.HasScope(requirement.Scope))
        {
            context.Succeed(requirement);
        }
        else
        {
            // No context.Fail(): another handler for the same requirement could still satisfy it.
            _logger.LogDebug("Access token is missing required scope {Scope}.", requirement.Scope);
        }

        return Task.CompletedTask;
    }
}
