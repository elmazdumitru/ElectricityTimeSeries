using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ElectricityTimeSeries.Api.Authorization;
using ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace ElectricityTimeSeries.Api.IntegrationTests.Telemetry;

/// <summary>Checks the API's tracing setup by catching its spans in memory; nothing is sent anywhere.</summary>
public class TelemetryTests : IClassFixture<SharedApiFixture>
{
    private const string NewSeries = """
        { "type": "Consumption", "biddingZone": "NO1", "resolution": "PT15M",
          "points": [ { "timestamp": "2025-11-22T00:00:00Z", "value": 4210.5 } ] }
        """;

    private readonly ApiFactory _api;

    public TelemetryTests(SharedApiFixture fixture)
    {
        _api = fixture.Api;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Requests_are_traced_even_when_the_caller_says_not_to(bool callerRecords)
    {
        var (status, spans) = await SendTracedAsync(HttpMethod.Get, "test/authorization/read", body: null, callerRecords);

        var request = Assert.Single(spans, span => span.Kind == ActivityKind.Server);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("test/authorization/read", request.GetTagItem("http.route"));
        Assert.Equal(200, request.GetTagItem("http.response.status_code"));
    }

    [Fact]
    public async Task Saving_a_series_is_traced_down_to_the_storage()
    {
        var (status, spans) = await SendTracedAsync(HttpMethod.Post, "api/time-series", NewSeries, callerRecords: true);

        Assert.Equal(HttpStatusCode.Created, status);
        var request = Assert.Single(spans, span => span.Kind == ActivityKind.Server);
        var service = Assert.Single(spans, span => span.DisplayName == "TimeSeriesService.Create");
        var repository = Assert.Single(spans, span => span.DisplayName == "TimeSeriesRepository.Add");
        var save = Assert.Single(spans, span => span.DisplayName == "SaveChanges");
        Assert.Equal(request.SpanId, service.ParentSpanId);
        Assert.Equal(service.SpanId, repository.ParentSpanId);
        Assert.Equal(repository.SpanId, save.ParentSpanId);
        Assert.NotNull(repository.GetTagItem("storage.provider"));
    }

    /// <summary>Sends the request with its own trace id, so spans from other tests running in this process
    /// can't get mixed in, and returns the spans of that trace once the request is done.</summary>
    private async Task<(HttpStatusCode Status, IReadOnlyList<Activity> Spans)> SendTracedAsync(
        HttpMethod method, string url, string? body, bool callerRecords)
    {
        var traceId = ActivityTraceId.CreateRandom();
        var trace = new TraceCollector(traceId);
        await using var api = _api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddProcessor(trace))));

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", _api.Tokens.CreateToken([Scopes.TimeSeriesRead, Scopes.TimeSeriesWrite]));
        request.Headers.Add("traceparent", $"00-{traceId}-{ActivitySpanId.CreateRandom()}-{(callerRecords ? "01" : "00")}");
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        using var response = await api.CreateClient().SendAsync(request, Ct);
        return (response.StatusCode, await trace.WaitForRequestAsync());
    }

    private sealed class TraceCollector : BaseProcessor<Activity>
    {
        private readonly ActivityTraceId _traceId;
        private readonly ConcurrentQueue<Activity> _spans = new();
        private readonly TaskCompletionSource _requestEnded = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TraceCollector(ActivityTraceId traceId)
        {
            _traceId = traceId;
        }

        /// <summary>The request's span ends last, after everything that happened inside it.</summary>
        public async Task<IReadOnlyList<Activity>> WaitForRequestAsync()
        {
            await _requestEnded.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            return [.. _spans];
        }

        public override void OnEnd(Activity activity)
        {
            if (activity.TraceId != _traceId)
            {
                return;
            }

            _spans.Enqueue(activity);
            if (activity.Kind == ActivityKind.Server)
            {
                _requestEnded.TrySetResult();
            }
        }
    }
}
