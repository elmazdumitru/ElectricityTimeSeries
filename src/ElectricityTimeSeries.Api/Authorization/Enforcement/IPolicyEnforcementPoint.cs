namespace ElectricityTimeSeries.Api.Authorization.Enforcement;

/// <summary>Decides whether an access token that passed validation may still be used.
/// Swap the implementation for a client of a real policy engine.</summary>
public interface IPolicyEnforcementPoint
{
    ValueTask<EnforcementDecision> EvaluateAsync(AccessTokenAttributes token, CancellationToken cancellationToken);
}

/// <summary>The facts about a token that access rules match on.</summary>
public sealed record AccessTokenAttributes(string? Subject, string? ClientId, string? TenantId, DateTimeOffset? IssuedAt);

public sealed record EnforcementDecision(bool IsPermitted, string? Reason)
{
    public static EnforcementDecision Permit { get; } = new(true, null);

    public static EnforcementDecision Deny(string reason) => new(false, reason);
}
