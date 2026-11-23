using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

/// <summary>Issues real tokens, signed with the private key from <see cref="TestSigningKeys"/>.</summary>
public sealed class TestTokenFactory
{
    public const string Audience = "electricity-timeseries-api";
    public const string DefaultClientId = "integration-tests";

    private readonly TestSigningKeys _keys;

    public TestTokenFactory(TestSigningKeys keys)
    {
        _keys = keys;
    }

    public string CreateToken(
        IEnumerable<string>? scopes = null,
        string issuer = FakeIdentityProvider.Issuer,
        string audience = Audience,
        DateTime? expires = null,
        SigningCredentials? signingCredentials = null,
        string clientId = DefaultClientId,
        string? tenantId = null,
        DateTime? issuedAt = null)
    {
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(5);
        var issuedAtValue = issuedAt ?? expiresAt.AddMinutes(-10);
        var claims = new Dictionary<string, object>
        {
            ["sub"] = "integration-test-client",
            ["client_id"] = clientId,
        };

        if (tenantId is not null)
        {
            claims["tid"] = tenantId;
        }

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
            IssuedAt = issuedAtValue,
            NotBefore = issuedAtValue,
            Expires = expiresAt,
            Claims = claims,
            SigningCredentials = signingCredentials ?? _keys.SigningCredentials,
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
