using System.Text.Json.Nodes;
using ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace ElectricityTimeSeries.Api.IntegrationTests.OpenApi;

/// <summary>Checks that the OpenAPI document describes the JSON the API really accepts.</summary>
public class OpenApiDocumentTests : IClassFixture<SharedApiFixture>
{
    private const string TypeReference = "#/components/schemas/TimeSeriesType";

    private readonly ApiFactory _api;

    public OpenApiDocumentTests(SharedApiFixture fixture)
    {
        _api = fixture.Api;
    }

    [Fact]
    public async Task Types_are_shown_as_their_names()
    {
        var document = await Document();

        var type = document["components"]!["schemas"]!["TimeSeriesType"]!;
        Assert.Equal(["Production", "Consumption", "CrossBorderFlow"], type["enum"]!.AsArray().Select(name => (string?)name));
    }

    [Fact]
    public async Task Requests_responses_and_the_search_filter_all_point_to_the_type_schema()
    {
        var document = await Document();

        var schemas = document["components"]!["schemas"]!;
        var filter = document["paths"]!["/api/time-series"]!["get"]!["parameters"]!.AsArray()
            .Single(parameter => string.Equals((string?)parameter!["name"], "type", StringComparison.OrdinalIgnoreCase))!;

        Assert.Contains(TypeReference, schemas["TimeSeriesRequest"]!["properties"]!["type"]!.ToJsonString());
        Assert.Equal(TypeReference, (string?)schemas["TimeSeriesResponse"]!["properties"]!["type"]!["$ref"]);
        Assert.Equal(TypeReference, (string?)filter["schema"]!["$ref"]);
    }

    private async Task<JsonNode> Document()
    {
        var provider = _api.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");
        var document = await provider.GetOpenApiDocumentAsync(TestContext.Current.CancellationToken);

        return JsonNode.Parse(await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_2))!;
    }
}
