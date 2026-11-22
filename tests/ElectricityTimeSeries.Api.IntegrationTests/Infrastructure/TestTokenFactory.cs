using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Issues real, signed tokens for integration tests, the way the identity provider would:
/// signed with the private key from <see cref="TestSigningKeys"/>.
/// </summary>
public sealed class TestTokenFactory(TestSigningKeys keys)
{
    public const string Audience = "electricity-timeseries-api";

    public string CreateToken(
        IEnumerable<string>? scopes = null,
        string issuer = FakeIdentityProvider.Issuer,
        string audience = Audience,
        DateTime? expires = null,
        SigningCredentials? signingCredentials = null)
    {
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(5);
        var claims = new Dictionary<string, object> { ["sub"] = "integration-test-client" };

        var scopeList = scopes?.ToArray() ?? [];
        if (scopeList.Length > 0)
        {
            // Space-separated in a single claim, the way Keycloak and other OAuth 2.0 providers issue scopes.
            claims["scope"] = string.Join(' ', scopeList);
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = expiresAt.AddMinutes(-10),
            NotBefore = expiresAt.AddMinutes(-10),
            Expires = expiresAt,
            Claims = claims,
            SigningCredentials = signingCredentials ?? keys.SigningCredentials,
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
