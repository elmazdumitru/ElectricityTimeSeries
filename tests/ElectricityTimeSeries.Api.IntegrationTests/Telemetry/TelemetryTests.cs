using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using ElectricityTimeSeries.Api.Authorization;
using ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace ElectricityTimeSeries.Api.IntegrationTests.Telemetry;

/// <summary>Checks the API's tracing setup by catching its spans in memory; nothing is sent anywhere.</summary>
public class TelemetryTests : IClassFixture<SharedApiFixture>
{
    private readonly ApiFactory _api;

    public TelemetryTests(SharedApiFixture fixture)
    {
        _api = fixture.Api;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Requests_are_traced_with_their_route_and_status()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spans = new IncomingRequestSpans(traceId);
        await using var api = _api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddProcessor(spans))));

        using var request = new HttpRequestMessage(HttpMethod.Get, "test/authorization/read");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _api.Tokens.CreateToken([Scopes.TimeSeriesRead]));
        // Our own trace id, so requests from other tests running in this process can't be mistaken for this one.
        request.Headers.Add("traceparent", $"00-{traceId}-{ActivitySpanId.CreateRandom()}-01");
        using var response = await api.CreateClient().SendAsync(request, Ct);

        var span = await spans.First.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("test/authorization/read", span.GetTagItem("http.route"));
        Assert.Equal(200, span.GetTagItem("http.response.status_code"));
    }

    private sealed class IncomingRequestSpans : BaseProcessor<Activity>
    {
        private readonly ActivityTraceId _traceId;
        private readonly TaskCompletionSource<Activity> _first = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IncomingRequestSpans(ActivityTraceId traceId)
        {
            _traceId = traceId;
        }

        public Task<Activity> First => _first.Task;

        public override void OnEnd(Activity activity)
        {
            if (activity.Kind == ActivityKind.Server && activity.TraceId == _traceId)
            {
                _first.TrySetResult(activity);
            }
        }
    }
}
