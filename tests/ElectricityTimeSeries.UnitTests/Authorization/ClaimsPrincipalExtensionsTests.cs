using System.Security.Claims;
using ElectricityTimeSeries.Api.Authorization;

namespace ElectricityTimeSeries.UnitTests.Authorization;

public class ClaimsPrincipalExtensionsTests
{
    [Theory]
    [InlineData("scope")]
    [InlineData("scp")]
    [InlineData("http://schemas.microsoft.com/identity/claims/scope")]
    public void HasScope_returns_true_for_supported_scope_claim_types(string claimType)
    {
        var user = CreateUser(new Claim(claimType, Scopes.TimeSeriesRead));

        Assert.True(user.HasScope(Scopes.TimeSeriesRead));
    }

    [Fact]
    public void HasScope_returns_true_when_scope_is_one_of_several_space_separated_values()
    {
        var user = CreateUser(new Claim("scope", $"openid {Scopes.TimeSeriesRead} {Scopes.TimeSeriesWrite}"));

        Assert.True(user.HasScope(Scopes.TimeSeriesWrite));
    }

    [Fact]
    public void HasScope_returns_true_when_each_scope_is_a_separate_claim()
    {
        var user = CreateUser(
            new Claim("scope", Scopes.TimeSeriesRead),
            new Claim("scope", Scopes.TimeSeriesWrite));

        Assert.True(user.HasScope(Scopes.TimeSeriesWrite));
    }

    [Fact]
    public void HasScope_returns_false_when_scope_is_missing()
    {
        var user = CreateUser(new Claim("scope", Scopes.TimeSeriesRead));

        Assert.False(user.HasScope(Scopes.TimeSeriesWrite));
    }

    [Fact]
    public void HasScope_is_case_sensitive()
    {
        var user = CreateUser(new Claim("scope", Scopes.TimeSeriesRead.ToLowerInvariant()));

        Assert.False(user.HasScope(Scopes.TimeSeriesRead));
    }

    [Fact]
    public void HasScope_does_not_match_a_partial_scope_value()
    {
        var user = CreateUser(new Claim("scope", "TimeSeries.ReadAll"));

        Assert.False(user.HasScope(Scopes.TimeSeriesRead));
    }

    [Fact]
    public void HasScope_ignores_scope_values_in_other_claim_types()
    {
        var user = CreateUser(new Claim(ClaimTypes.Role, Scopes.TimeSeriesRead));

        Assert.False(user.HasScope(Scopes.TimeSeriesRead));
    }

    private static ClaimsPrincipal CreateUser(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));
}
