using ElectricityTimeSeries.Api.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ElectricityTimeSeries.Api.OpenApi;

/// <summary>Shows times read by <see cref="ZonedTimeJsonConverter"/> as text again: a custom converter hides the type.</summary>
public sealed class ZonedTimeSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        if (type == typeof(DateTimeOffset) || type == typeof(DateTimeOffset?))
        {
            // In the URL an optional time is just left out, so only JSON properties can be null.
            var canBeNull = type == typeof(DateTimeOffset?) && context.ParameterDescription is null;
            schema.Type = canBeNull ? JsonSchemaType.String | JsonSchemaType.Null : JsonSchemaType.String;
            schema.Format = "date-time";
        }

        return Task.CompletedTask;
    }
}
