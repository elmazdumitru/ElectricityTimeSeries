using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ElectricityTimeSeries.Api.OpenApi;

/// <summary>Shows <see cref="TimeSpan"/> values in OpenAPI as ISO 8601 durations ("PT15M"), the way the API writes them.</summary>
public sealed class DurationSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (IsDuration(context.JsonTypeInfo.Type))
        {
            Describe(schema);
        }

        return Task.CompletedTask;
    }

    private static bool IsDuration(Type type) => (Nullable.GetUnderlyingType(type) ?? type) == typeof(TimeSpan);

    private static void Describe(OpenApiSchema schema)
    {
        schema.Type = JsonSchemaType.String;
        schema.Format = "duration";
        schema.Pattern = null; // The default "hh:mm:ss" pattern doesn't match "PT15M".
        schema.Description ??= "An ISO 8601 duration: PT15M or PT1H.";
        schema.Examples = [JsonValue.Create("PT15M")];
    }
}
