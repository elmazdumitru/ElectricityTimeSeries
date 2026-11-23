using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ElectricityTimeSeries.Api.Telemetry;

public static class TelemetryExtensions
{
    public const string ServiceName = "electricity-timeseries-api";

    /// <summary>Logs, traces and metrics through OpenTelemetry. They're only sent out when
    /// OTEL_EXPORTER_OTLP_ENDPOINT is set, for example to the Aspire dashboard.</summary>
    public static WebApplicationBuilder AddApiTelemetry(this WebApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing => tracing
                // Record every request, even when the caller's trace says not to (Visual Studio's HTTP client does).
                .SetSampler(new AlwaysOnSampler())
                // The service and storage spans from Core and Infrastructure.
                .AddSource("ElectricityTimeSeries.*")
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation());

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.UseOtlpExporter();
        }

        return builder;
    }
}
