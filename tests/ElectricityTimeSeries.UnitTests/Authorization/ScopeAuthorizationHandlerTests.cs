using System.Security.Claims;
using ElectricityTimeSeries.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElectricityTimeSeries.UnitTests.Authorization;

public class ScopeAuthorizationHandlerTests
{
    private readonly ScopeAuthorizationHandler _handler = new(NullLogger<ScopeAuthorizationHandler>.Instance);

    [Fact]
    public async Task Succeeds_when_user_has_the_required_scope()
    {
        var context = CreateContext(Scopes.TimeSeriesRead, new Claim("scope", Scopes.TimeSeriesRead));

        await _handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Does_not_succeed_when_user_lacks_the_required_scope()
    {
        var context = CreateContext(Scopes.TimeSeriesWrite, new Claim("scope", Scopes.TimeSeriesRead));

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Does_not_explicitly_fail_so_other_handlers_can_still_succeed()
    {
        var context = CreateContext(Scopes.TimeSeriesWrite);

        await _handler.HandleAsync(context);

        Assert.False(context.HasFailed);
    }

    private static AuthorizationHandlerContext CreateContext(string requiredScope, params Claim[] claims)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
        return new AuthorizationHandlerContext([new ScopeRequirement(requiredScope)], user, resource: null);
    }
}
