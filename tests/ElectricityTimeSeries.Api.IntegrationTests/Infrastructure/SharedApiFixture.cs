namespace ElectricityTimeSeries.Api.IntegrationTests.Infrastructure;

/// <summary>One API and database shared by a whole test class or collection, for tests that don't need
/// a clean slate. Opt-in only; other tests use <see cref="IsolatedApiTest"/>.</summary>
public sealed class SharedApiFixture : IAsyncLifetime
{
    private readonly TestStorage _storage;
    private TestDatabase? _database;
    private ApiFactory? _api;

    public SharedApiFixture(TestStorage storage)
    {
        _storage = storage;
    }

    public ApiFactory Api => _api ?? throw new InvalidOperationException("The API is created in InitializeAsync.");

    public async ValueTask InitializeAsync()
    {
        _database = _storage.CreateDatabase();
        _api = new ApiFactory(_database);
        await _api.PrepareAsync(TestContext.Current.CancellationToken);
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
