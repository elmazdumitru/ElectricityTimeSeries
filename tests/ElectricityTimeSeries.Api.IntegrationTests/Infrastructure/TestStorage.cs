using System.Collections.Concurrent;
using ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;
using ElectricityTimeSeries.Infrastructure;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(TestStorage))]

namespace ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

/// <summary>Storage for the whole test run, picked by <c>TEST_STORAGE</c>: <c>InMemory</c> (default) or
/// <c>SqlServer</c>, which starts one shared SQL Server container (needs Docker).</summary>
public sealed class TestStorage : IAsyncLifetime
{
    public const string EnvironmentVariable = "TEST_STORAGE";

    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly ConcurrentBag<TestDatabase> _ready = [];

    private MsSqlContainer? _sqlServer;

    public StorageProvider Provider { get; } = ReadProvider();

    public async ValueTask InitializeAsync()
    {
        if (Provider is StorageProvider.SqlServer)
        {
            _sqlServer = new MsSqlBuilder(SqlServerImage).Build();
            await _sqlServer.StartAsync(TestContext.Current.CancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_sqlServer is not null)
        {
            await _sqlServer.DisposeAsync();
        }
    }

    /// <summary>Empty storage no other test is using. On SQL Server, a reused database if one is free.</summary>
    public TestDatabase CreateDatabase() => Provider switch
    {
        StorageProvider.InMemory => TestDatabase.InMemory(),
        StorageProvider.SqlServer => _ready.TryTake(out var reused) ? reused : TestDatabase.OnSqlServer(_sqlServer!.GetConnectionString(), this),
        _ => throw new InvalidOperationException($"Unknown storage provider '{Provider}'."),
    };

    /// <summary>Takes back an emptied, migrated database for the next test.</summary>
    internal void GiveBack(TestDatabase database) => _ready.Add(database);

    private static StorageProvider ReadProvider()
    {
        var value = Environment.GetEnvironmentVariable(EnvironmentVariable);

        return string.IsNullOrWhiteSpace(value)
            ? StorageProvider.InMemory
            : Enum.TryParse<StorageProvider>(value, ignoreCase: true, out var provider)
                ? provider
                : throw new InvalidOperationException(
                    $"{EnvironmentVariable}='{value}' is not a storage provider. Use {string.Join(" or ", Enum.GetNames<StorageProvider>())}.");
    }
}
