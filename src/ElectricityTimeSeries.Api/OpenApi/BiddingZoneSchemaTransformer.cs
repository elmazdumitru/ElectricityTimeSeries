using System.Text.Json.Nodes;
using ElectricityTimeSeries.Core.Domain;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ElectricityTimeSeries.Api.OpenApi;

/// <summary>Shows <see cref="BiddingZone"/> in OpenAPI as plain text ("NO1"), as in JSON, not as an object.</summary>
public sealed class BiddingZoneSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if ((Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type) == typeof(BiddingZone))
        {
            schema.Type = JsonSchemaType.String;
            schema.Properties = null;
            schema.Description = $"A bidding zone code. Known zones: {BiddingZone.ConcatenatedKnownZones}.";
            schema.Examples = [JsonValue.Create("NO1")];
        }

        return Task.CompletedTask;
    }
}
