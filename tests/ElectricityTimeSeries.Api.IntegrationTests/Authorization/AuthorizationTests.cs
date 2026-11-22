using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ElectricityTimeSeries.Api.Authorization;
using ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;
using Microsoft.IdentityModel.Tokens;

namespace ElectricityTimeSeries.Api.IntegrationTests.Authorization;

public class AuthorizationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string ReadUrl = "test/authorization/read";
    private const string WriteUrl = "test/authorization/write";
    private const string NoPolicyUrl = "test/authorization/no-policy";

    private readonly HttpClient _client = factory.CreateClient();
    private readonly TestTokenFactory _tokens = factory.Tokens;

    [Fact]
    public async Task Health_endpoint_allows_anonymous_access()
    {
        var response = await _client.GetAsync("health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Fallback_policy_rejects_anonymous_requests_to_endpoints_without_a_policy()
    {
        var response = await _client.GetAsync(NoPolicyUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Fallback_policy_allows_any_authenticated_caller()
    {
        var response = await SendAsync(HttpMethod.Get, NoPolicyUrl, _tokens.CreateToken());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Missing_token_returns_401_with_bearer_challenge()
    {
        var response = await _client.GetAsync(ReadUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
    }

    [Theory]
    [InlineData(new string[0], HttpStatusCode.Forbidden)]
    [InlineData(new[] { Scopes.TimeSeriesRead }, HttpStatusCode.OK)]
    [InlineData(new[] { Scopes.TimeSeriesWrite }, HttpStatusCode.Forbidden)]
    [InlineData(new[] { Scopes.TimeSeriesRead, Scopes.TimeSeriesWrite }, HttpStatusCode.OK)]
    public async Task Read_policy_requires_read_scope(string[] scopes, HttpStatusCode expected)
    {
        var response = await SendAsync(HttpMethod.Get, ReadUrl, _tokens.CreateToken(scopes));

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(new string[0], HttpStatusCode.Forbidden)]
    [InlineData(new[] { Scopes.TimeSeriesRead }, HttpStatusCode.Forbidden)]
    [InlineData(new[] { Scopes.TimeSeriesWrite }, HttpStatusCode.OK)]
    [InlineData(new[] { Scopes.TimeSeriesRead, Scopes.TimeSeriesWrite }, HttpStatusCode.OK)]
    public async Task Write_policy_requires_write_scope(string[] scopes, HttpStatusCode expected)
    {
        var response = await SendAsync(HttpMethod.Post, WriteUrl, _tokens.CreateToken(scopes));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Forbidden_response_is_problem_details()
    {
        var response = await SendAsync(HttpMethod.Post, WriteUrl, _tokens.CreateToken([Scopes.TimeSeriesRead]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Api_downloads_the_public_keys_from_the_identity_provider()
    {
        var response = await SendAsync(HttpMethod.Get, ReadUrl, _tokens.CreateToken([Scopes.TimeSeriesRead]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(FakeIdentityProvider.KeysUrl, factory.IdentityProvider.Requests);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("wrong audience")]
    [InlineData("wrong issuer")]
    [InlineData("signed by another key pair")]
    [InlineData("signed with the public key as a shared secret")]
    [InlineData("tampered payload")]
    [InlineData("malformed")]
    public async Task Invalid_token_returns_401(string reason)
    {
        var token = CreateInvalidToken(reason);

        var response = await SendAsync(HttpMethod.Get, ReadUrl, token);

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 401 for a {reason} token, got {(int)response.StatusCode}.");
    }

    private string CreateInvalidToken(string reason)
    {
        string[] scopes = [Scopes.TimeSeriesRead];

        switch (reason)
        {
            case "expired":
                return _tokens.CreateToken(scopes, expires: DateTime.UtcNow.AddMinutes(-10));
            case "wrong audience":
                return _tokens.CreateToken(scopes, audience: "some-other-api");
            case "wrong issuer":
                return _tokens.CreateToken(scopes, issuer: "https://untrusted-issuer.test");
            case "signed by another key pair":
                using (var otherKeys = new TestSigningKeys())
                {
                    return _tokens.CreateToken(scopes, signingCredentials: otherKeys.SigningCredentials);
                }
            case "signed with the public key as a shared secret":
                // A known trick: sign with the publicly available key as if it were a password.
                var publicKeyBytes = factory.SigningKeys.PublicKey.Parameters.Modulus;
                var sharedSecret = new SymmetricSecurityKey(publicKeyBytes) { KeyId = factory.SigningKeys.KeyId };
                return _tokens.CreateToken(scopes, signingCredentials: new SigningCredentials(sharedSecret, SecurityAlgorithms.HmacSha256));
            case "tampered payload":
                return WithEscalatedScope(_tokens.CreateToken(scopes));
            case "malformed":
                return "not-a-jwt";
            default:
                throw new ArgumentOutOfRangeException(nameof(reason), reason, null);
        }
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    /// <summary>
    /// Rewrites the token's payload to also grant the write scope while keeping the original
    /// signature, simulating a client that tries to escalate its own privileges.
    /// </summary>
    private static string WithEscalatedScope(string token)
    {
        var parts = token.Split('.');
        var payload = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]))
            .Replace(Scopes.TimeSeriesRead, $"{Scopes.TimeSeriesRead} {Scopes.TimeSeriesWrite}");
        parts[1] = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return string.Join('.', parts);
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
    }
}
