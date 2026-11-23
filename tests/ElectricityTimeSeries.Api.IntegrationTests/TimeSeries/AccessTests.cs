using System.Net;
using System.Net.Http.Json;
using ElectricityTimeSeries.Api.Authorization;
using ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

namespace ElectricityTimeSeries.Api.IntegrationTests.TimeSeries;

public class AccessTests : TimeSeriesApiTest
{
    public AccessTests(TestStorage storage)
        : base(storage)
    {
    }

    public static TheoryData<string, string> Endpoints => new()
    {
        { "GET", "" },
        { "GET", "/{id}" },
        { "POST", "" },
        { "PUT", "/{id}" },
        { "DELETE", "/{id}" },
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Every_endpoint_requires_a_token(string method, string path)
    {
        var created = await Create(Series(points: 1));

        var response = await Api.CreateClient().SendAsync(Request(method, path, created.Id), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "", HttpStatusCode.OK)]
    [InlineData("GET", "/{id}", HttpStatusCode.OK)]
    [InlineData("POST", "", HttpStatusCode.Forbidden)]
    [InlineData("PUT", "/{id}", HttpStatusCode.Forbidden)]
    [InlineData("DELETE", "/{id}", HttpStatusCode.Forbidden)]
    public async Task Read_only_token_can_read_but_not_write(string method, string path, HttpStatusCode expected)
    {
        var created = await Create(Series(points: 1));

        var response = await CreateClient(Scopes.TimeSeriesRead).SendAsync(Request(method, path, created.Id), Ct);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "", HttpStatusCode.Forbidden)]
    [InlineData("GET", "/{id}", HttpStatusCode.Forbidden)]
    [InlineData("POST", "", HttpStatusCode.Created)]
    [InlineData("PUT", "/{id}", HttpStatusCode.OK)]
    [InlineData("DELETE", "/{id}", HttpStatusCode.NoContent)]
    public async Task Write_only_token_can_write_but_not_read(string method, string path, HttpStatusCode expected)
    {
        var created = await Create(Series(points: 1));

        var response = await CreateClient(Scopes.TimeSeriesWrite).SendAsync(Request(method, path, created.Id), Ct);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Refused_writes_change_nothing()
    {
        var created = await Create(Series(zone: "NO1", points: 1));
        var readOnly = CreateClient(Scopes.TimeSeriesRead);

        await readOnly.PostAsJsonAsync(BaseUrl, Series(zone: "SE1"), Ct);
        await readOnly.PutAsJsonAsync($"{BaseUrl}/{created.Id}", Series(zone: "SE1"), Ct);
        await readOnly.DeleteAsync($"{BaseUrl}/{created.Id}", Ct);

        var stored = Assert.Single(await Search());
        AssertSameSeries(created, stored);
    }

    private static HttpRequestMessage Request(string method, string path, Guid id)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), BaseUrl + path.Replace("{id}", id.ToString()));
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(Series(start: Midnight.AddDays(1), points: 1));
        }

        return request;
    }
}
