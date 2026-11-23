using Microsoft.AspNetCore.Authorization;

namespace ElectricityTimeSeries.Api.Authorization;

public sealed partial class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
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
            LogMissingRequiredScope(_logger, requirement.Scope);
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Access token is missing required scope {Scope}.")]
    private static partial void LogMissingRequiredScope(ILogger logger, string scope);
}
