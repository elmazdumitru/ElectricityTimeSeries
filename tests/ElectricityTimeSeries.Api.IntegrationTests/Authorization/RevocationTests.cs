using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using ElectricityTimeSeries.Api.Authorization;
using ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

namespace ElectricityTimeSeries.Api.IntegrationTests.Authorization;

public class RevocationTests : IsolatedApiTest
{
    private const string ReadUrl = "test/authorization/read";

    public RevocationTests(TestStorage storage)
        : base(storage)
    {
    }

    [Fact]
    public async Task Revoking_a_client_shuts_out_its_tokens_while_other_clients_carry_on()
    {
        var billing = Api.Tokens.CreateToken([Scopes.TimeSeriesRead], clientId: "billing-app");
        var monitoring = Api.Tokens.CreateToken([Scopes.TimeSeriesRead], clientId: "monitoring-app");
        Assert.Equal(HttpStatusCode.OK, await ReadAsync(billing));
        Assert.Equal(HttpStatusCode.OK, await ReadAsync(monitoring));

        await Api.WriteAccessLedgerAsync("""
            { "revocations": [ { "clientId": "billing-app", "reason": "Client secret leaked." } ] }
            """, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, await EventuallyAsync(billing, HttpStatusCode.Unauthorized));
        Assert.Equal(HttpStatusCode.OK, await ReadAsync(monitoring));
    }

    [Fact]
    public async Task Tenant_cut_off_revokes_only_that_tenants_older_tokens()
    {
        var now = DateTime.UtcNow;
        var oldContoso = Api.Tokens.CreateToken([Scopes.TimeSeriesRead], tenantId: "contoso", issuedAt: now.AddMinutes(-30));
        var newContoso = Api.Tokens.CreateToken([Scopes.TimeSeriesRead], tenantId: "contoso", issuedAt: now.AddMinutes(-1));
        var oldFabrikam = Api.Tokens.CreateToken([Scopes.TimeSeriesRead], tenantId: "fabrikam", issuedAt: now.AddMinutes(-30));

        await Api.WriteAccessLedgerAsync($$"""
            { "revocations": [ { "tenantId": "contoso", "issuedBefore": "{{now.AddMinutes(-10):O}}" } ] }
            """, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, await EventuallyAsync(oldContoso, HttpStatusCode.Unauthorized));
        Assert.Equal(HttpStatusCode.OK, await ReadAsync(newContoso));
        Assert.Equal(HttpStatusCode.OK, await ReadAsync(oldFabrikam));
    }

    private async Task<HttpStatusCode> ReadAsync(string token)
    {
        using var client = Api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.GetAsync(ReadUrl, Ct);
        return response.StatusCode;
    }

    /// <summary>The API picks up the new ledger a moment after it's written.</summary>
    private async Task<HttpStatusCode> EventuallyAsync(string token, HttpStatusCode expected)
    {
        var timer = Stopwatch.StartNew();
        HttpStatusCode status;
        while ((status = await ReadAsync(token)) != expected && timer.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(100, Ct);
        }

        return status;
    }
}
