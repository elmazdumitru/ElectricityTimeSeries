namespace ElectricityTimeSeries.Api.Authorization.Enforcement;

public sealed class RevocationLedger
{
    public static RevocationLedger Empty { get; } = new();

    public IReadOnlyList<Revocation> Revocations { get; init; } = [];
}

/// <summary>Revokes the tokens that match every field it sets. An entry that sets nothing matches nothing.</summary>
public sealed record Revocation(
    string? ClientId = null,
    string? TenantId = null,
    string? Subject = null,
    DateTimeOffset? IssuedBefore = null,
    string? Reason = null)
{
    public bool Matches(AccessTokenAttributes token) =>
        (ClientId, TenantId, Subject, IssuedBefore) != (null, null, null, null) &&
        (ClientId is null || ClientId == token.ClientId) &&
        (TenantId is null || TenantId == token.TenantId) &&
        (Subject is null || Subject == token.Subject) &&
        // A token without an issue time can't prove it's newer than the cut-off.
        (IssuedBefore is null || token.IssuedAt is null || token.IssuedAt < IssuedBefore);
}
