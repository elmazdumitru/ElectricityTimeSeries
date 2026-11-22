using System.Text.Json.Serialization;
using ElectricityTimeSeries.Api.Authorization;
using ElectricityTimeSeries.Api.ErrorHandling;
using ElectricityTimeSeries.Api.OpenApi;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using ElectricityTimeSeries.Core;
using ElectricityTimeSeries.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddCore()
    .AddInfrastructure();

builder.Services.AddApiAuthentication();
builder.Services
    .AddControllers(options =>
        // Report errors under the JSON field names clients send ("biddingZone"), not the C# names.
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()))
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainValidationExceptionHandler>();
builder.Services.AddHealthChecks();

builder.Services.AddOpenApi(options => options.AddSchemaTransformer<BiddingZoneSchemaTransformer>());

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
