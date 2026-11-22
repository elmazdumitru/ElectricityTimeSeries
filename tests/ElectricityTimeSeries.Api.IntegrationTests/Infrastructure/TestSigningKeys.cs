using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

/// <summary>
/// A public/private key pair, created fresh for each test run, playing the role of the
/// identity provider's signing keys. Tests sign tokens with the private key; the API only
/// ever receives the public key, the same way it does with a real identity provider.
/// </summary>
public sealed class TestSigningKeys : IDisposable
{
    private readonly RSA _rsa = RSA.Create(2048);

    public string KeyId { get; } = Guid.NewGuid().ToString("N");

    /// <summary>Private key: used by the tests to sign tokens.</summary>
    public SigningCredentials SigningCredentials =>
        new(new RsaSecurityKey(_rsa) { KeyId = KeyId }, SecurityAlgorithms.RsaSha256);

    /// <summary>Public key only: what the API is allowed to see.</summary>
    public RsaSecurityKey PublicKey => new(_rsa.ExportParameters(includePrivateParameters: false)) { KeyId = KeyId };

    /// <summary>The public key in the standard format identity providers publish (a JWKS document).</summary>
    public string ToJwksJson()
    {
        var key = JsonWebKeyConverter.ConvertFromRSASecurityKey(PublicKey);

        return JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new { kty = key.Kty, use = "sig", alg = SecurityAlgorithms.RsaSha256, kid = key.Kid, n = key.N, e = key.E },
            },
        });
    }

    public void Dispose() => _rsa.Dispose();
}
