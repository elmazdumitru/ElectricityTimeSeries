namespace ElectricityTimeSeries.Infrastructure.Persistence;

/// <summary>EF Core's in-memory store has no transactions, so a save that loses a race can be half-applied.
/// For in-memory storage, racing saves run one at a time and check the version first. SQL Server needs nothing extra.</summary>
internal sealed class RacingSaveGuard
{
    private readonly SemaphoreSlim? _oneAtATime;

    private RacingSaveGuard(bool serialize)
    {
        _oneAtATime = serialize ? new SemaphoreSlim(1, 1) : null;
    }

    public static RacingSaveGuard ForSqlServer() => new(serialize: false);

    public static RacingSaveGuard ForInMemory() => new(serialize: true);

    /// <summary>Whether the stored version must be checked before saving (the store can't check it atomically).</summary>
    public bool ChecksVersionFirst => _oneAtATime is not null;

    public async Task<T> RunAsync<T>(Func<Task<T>> save, CancellationToken cancellationToken)
    {
        if (_oneAtATime is null)
        {
            return await save();
        }

        await _oneAtATime.WaitAsync(cancellationToken);
        try
        {
            return await save();
        }
        finally
        {
            _oneAtATime.Release();
        }
    }
}
