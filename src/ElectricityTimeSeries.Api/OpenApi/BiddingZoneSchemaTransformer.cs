using System.Text.Json.Nodes;
using ElectricityTimeSeries.Core.Domain;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ElectricityTimeSeries.Api.OpenApi;

/// <summary>
/// Describes <see cref="BiddingZone"/> in the OpenAPI document the way it appears in JSON:
/// as plain text, for example "NO1", instead of an object with its C# properties.
/// </summary>
public sealed class BiddingZoneSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type == typeof(BiddingZone))
        {
            schema.Type = JsonSchemaType.String;
            schema.Properties = null;
            schema.Description = $"A bidding zone code. Known zones: {BiddingZone.ConcatenatedKnownZones}.";
            schema.Examples = [JsonValue.Create("NO1")];
        }

        return Task.CompletedTask;
    }
}
