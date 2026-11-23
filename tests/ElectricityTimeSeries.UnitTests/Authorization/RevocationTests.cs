using ElectricityTimeSeries.Api.Authorization.Enforcement;

namespace ElectricityTimeSeries.UnitTests.Authorization;

public class RevocationTests
{
    private static readonly DateTimeOffset CutOff = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly AccessTokenAttributes Token = new(
        Subject: "alice", ClientId: "billing-app", TenantId: "contoso", IssuedAt: CutOff.AddHours(-1));

    [Theory]
    [InlineData("billing-app", null, null)]
    [InlineData(null, "contoso", null)]
    [InlineData(null, null, "alice")]
    [InlineData("billing-app", "contoso", "alice")]
    public void Matches_when_every_field_it_sets_matches(string? clientId, string? tenantId, string? subject)
    {
        Assert.True(new Revocation(clientId, tenantId, subject).Matches(Token));
    }

    [Theory]
    [InlineData("monitoring-app", null, null)]
    [InlineData(null, "fabrikam", null)]
    [InlineData("billing-app", "fabrikam", null)]
    public void Does_not_match_when_any_field_it_sets_differs(string? clientId, string? tenantId, string? subject)
    {
        Assert.False(new Revocation(clientId, tenantId, subject).Matches(Token));
    }

    [Fact]
    public void An_entry_that_sets_nothing_matches_nothing()
    {
        Assert.False(new Revocation(Reason: "Forgot the criteria.").Matches(Token));
    }

    [Fact]
    public void Cut_off_matches_only_tokens_issued_before_it()
    {
        var revocation = new Revocation(TenantId: "contoso", IssuedBefore: CutOff);

        Assert.True(revocation.Matches(Token));
        Assert.False(revocation.Matches(Token with { IssuedAt = CutOff }));
        Assert.False(revocation.Matches(Token with { IssuedAt = CutOff.AddMinutes(1) }));
    }

    [Fact]
    public void Cut_off_matches_tokens_without_an_issue_time()
    {
        Assert.True(new Revocation(IssuedBefore: CutOff).Matches(Token with { IssuedAt = null }));
    }
}
