using ElectricityTimeSeries.Api.Authorization.Enforcement;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ElectricityTimeSeries.UnitTests.Authorization;

public sealed class FileRevocationLedgerTests : IDisposable
{
    private static readonly AccessTokenAttributes BillingToken = new(null, "billing-app", null, null);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "electricity-timeseries-tests", Guid.NewGuid().ToString("N"));
    private readonly ErrorSignal _logger = new();

    private string LedgerPath => Path.Combine(_directory, "access-ledger.json");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Permits_everything_when_there_is_no_ledger_file()
    {
        using var ledger = CreateLedger();

        Assert.True((await ledger.EvaluateAsync(BillingToken, Ct)).IsPermitted);
    }

    [Fact]
    public async Task Keeps_the_previous_ledger_when_a_new_one_cant_be_read()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(LedgerPath, """{ "revocations": [ { "clientId": "billing-app" } ] }""", Ct);
        using var ledger = CreateLedger();

        await File.WriteAllTextAsync(LedgerPath, """{ "revocations": [ { "clientId": """, Ct);
        await _logger.FirstError.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        Assert.False((await ledger.EvaluateAsync(BillingToken, Ct)).IsPermitted);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private FileRevocationLedger CreateLedger() => new(
        Options.Create(new AccessPolicyOptions { LedgerPath = LedgerPath }),
        new HostingEnvironment { ContentRootPath = _directory },
        _logger);

    private sealed class ErrorSignal : ILogger<FileRevocationLedger>
    {
        private readonly TaskCompletionSource _firstError = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task FirstError => _firstError.Task;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                _firstError.TrySetResult();
            }
        }
    }
}
