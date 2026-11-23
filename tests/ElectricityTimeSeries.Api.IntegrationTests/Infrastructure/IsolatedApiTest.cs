namespace ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

/// <summary>Gives each test its own API and empty storage, so results are exact and order doesn't matter.
/// To share one API across tests instead, use <see cref="SharedApiFixture"/>.</summary>
public abstract class IsolatedApiTest : IAsyncLifetime
{
    private readonly TestStorage _storage;
    private TestDatabase? _database;
    private ApiFactory? _api;

    protected IsolatedApiTest(TestStorage storage)
    {
        _storage = storage;
    }

    protected ApiFactory Api => _api ?? throw new InvalidOperationException("The API is created in InitializeAsync.");

    /// <summary>Cancelled when the test run is stopped, so long calls end quickly.</summary>
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _database = _storage.CreateDatabase();
        _api = new ApiFactory(_database);
        await _api.PrepareAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_api is not null)
        {
            await _api.DisposeAsync();
        }

        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }
}
