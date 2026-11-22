using System.Security.Claims;

namespace ElectricityTimeSeries.Api.Authorization;

public static class ClaimsPrincipalExtensions
{
    // "scope" is the OAuth 2.0 claim (dotnet user-jwts, Keycloak, Auth0); "scp" is used by Microsoft Entra ID.
    // The long form is how "scp" appears when the JWT handler maps inbound claim types.
    private static readonly string[] ScopeClaimTypes =
        ["scope", "scp", "http://schemas.microsoft.com/identity/claims/scope"];

    /// <summary>
    /// Returns true when any scope claim contains <paramref name="scope"/>.
    /// Handles both one claim per scope and a single claim with space-separated scopes.
    /// Scope values are compared case-sensitively, as defined by OAuth 2.0 (RFC 6749, section 3.3).
    /// </summary>
    public static bool HasScope(this ClaimsPrincipal user, string scope) =>
        user.Claims
            .Where(claim => ScopeClaimTypes.Contains(claim.Type))
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);
}
