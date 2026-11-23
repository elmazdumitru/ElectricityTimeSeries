using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ElectricityTimeSeries.Api.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ElectricityTimeSeries.Api.OpenApi;

/// <summary>Shows the properties that use <see cref="IsoDurationJsonConverter"/> as ISO 8601 durations ("PT15M") in OpenAPI.</summary>
public sealed class DurationSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (UsesIsoDurations(context.JsonPropertyInfo))
        {
            Describe(schema);
        }

        return Task.CompletedTask;
    }

    private static bool UsesIsoDurations(JsonPropertyInfo? property) =>
        property?.AttributeProvider?.GetCustomAttributes(typeof(JsonConverterAttribute), inherit: false)
            .Any(attribute => ((JsonConverterAttribute)attribute).ConverterType == typeof(IsoDurationJsonConverter)) is true;

    private static void Describe(OpenApiSchema schema)
    {
        schema.Type = JsonSchemaType.String;
        schema.Format = "duration";
        schema.Pattern = null; // The default "hh:mm:ss" pattern doesn't match "PT15M".
        schema.Description ??= "An ISO 8601 duration: PT15M or PT1H.";
        schema.Examples = [JsonValue.Create("PT15M")];
    }
}
