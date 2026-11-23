using System.Text.Json.Serialization;
using ElectricityTimeSeries.Api.Authorization;
using ElectricityTimeSeries.Api.ErrorHandling;
using ElectricityTimeSeries.Api.Json;
using ElectricityTimeSeries.Api.OpenApi;
using ElectricityTimeSeries.Api.Telemetry;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using ElectricityTimeSeries.Core;
using ElectricityTimeSeries.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddApiTelemetry();

builder.Services
    .AddCore()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddApiAuthentication();
builder.Services
    .AddControllers(options =>
    {
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        options.JsonSerializerOptions.Converters.Add(new BiddingZoneJsonConverter());

        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainValidationExceptionHandler>();
builder.Services.AddExceptionHandler<ConcurrentChangeExceptionHandler>();
builder.Services.AddHealthChecks();

builder.Services.AddOpenApi(options =>
{
    options.AddSchemaTransformer<BiddingZoneSchemaTransformer>();
    options.AddSchemaTransformer<DurationSchemaTransformer>();
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

app.Run();

// Exposed for integration tests (WebApplicationFactory<Program>).
public partial class Program;
