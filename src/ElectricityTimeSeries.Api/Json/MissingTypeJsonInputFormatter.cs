using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Options;

namespace ElectricityTimeSeries.Api.Json;

/// <summary>Turns a body without its "type" field into a 400 listing the allowed values, instead of a 500.
/// System.Text.Json throws <see cref="NotSupportedException"/> there, which ASP.NET Core doesn't turn into a 400.</summary>
public sealed class MissingTypeJsonInputFormatter : IInputFormatter, IApiRequestFormatMetadataProvider
{
    private readonly SystemTextJsonInputFormatter _inner;

    public MissingTypeJsonInputFormatter(SystemTextJsonInputFormatter inner)
    {
        _inner = inner;
    }

    public bool CanRead(InputFormatterContext context) => _inner.CanRead(context);

    public async Task<InputFormatterResult> ReadAsync(InputFormatterContext context)
    {
        try
        {
            return await _inner.ReadAsync(context);
        }
        catch (NotSupportedException) when (Polymorphism(context.ModelType) is { } polymorphism)
        {
            var allowed = string.Join(", ", polymorphism.DerivedTypes.Select(d => d.TypeDiscriminator));
            context.ModelState.TryAddModelError("$", $"The '{polymorphism.TypeDiscriminatorPropertyName}' field is required: {allowed}.");
            return InputFormatterResult.Failure();
        }
    }

    public IReadOnlyList<string>? GetSupportedContentTypes(string? contentType, Type objectType) =>
        _inner.GetSupportedContentTypes(contentType, objectType);

    private JsonPolymorphismOptions? Polymorphism(Type modelType) =>
        _inner.SerializerOptions.GetTypeInfo(modelType).PolymorphismOptions;
}

public sealed class MissingTypeJsonInputFormatterSetup : IConfigureOptions<MvcOptions>
{
    public void Configure(MvcOptions options)
    {
        var inner = options.InputFormatters.OfType<SystemTextJsonInputFormatter>().First();
        options.InputFormatters[options.InputFormatters.IndexOf(inner)] = new MissingTypeJsonInputFormatter(inner);
    }
}
