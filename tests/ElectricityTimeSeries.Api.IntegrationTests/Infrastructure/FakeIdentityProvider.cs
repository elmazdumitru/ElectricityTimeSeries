using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Stands in for the identity provider's web server. The API downloads its settings and
/// public keys from here exactly as it would from Keycloak or Entra ID, but the requests
/// never leave the test process.
/// </summary>
public sealed class FakeIdentityProvider(TestSigningKeys keys) : HttpMessageHandler
{
    public const string Issuer = "https://identity-provider.test/realms/electricity-timeseries";

    private const string MetadataPath = "/realms/electricity-timeseries/.well-known/openid-configuration";
    private const string KeysPath = "/realms/electricity-timeseries/protocol/openid-connect/certs";

    /// <summary>Every URL the API requested, so tests can check the keys were actually downloaded.</summary>
    public ConcurrentQueue<Uri> Requests { get; } = new();

    public static Uri KeysUrl { get; } = new($"https://identity-provider.test{KeysPath}");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!;
        Requests.Enqueue(url);

        var response = url.AbsolutePath switch
        {
            MetadataPath => Json(JsonSerializer.Serialize(new
            {
                issuer = Issuer,
                jwks_uri = KeysUrl.ToString(),
                id_token_signing_alg_values_supported = new[] { "RS256" },
            })),
            KeysPath => Json(keys.ToJwksJson()),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };

        return Task.FromResult(response);
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };
}
