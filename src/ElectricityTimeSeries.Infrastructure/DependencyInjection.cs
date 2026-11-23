using ElectricityTimeSeries.Core.Abstractions;
using ElectricityTimeSeries.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ElectricityTimeSeries.Infrastructure;

public static class DependencyInjection
{
    private const string InMemoryDatabaseName = "ElectricityTimeSeries";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var storage = configuration.GetSection(StorageOptions.Section).Get<StorageOptions>() ?? new StorageOptions();

        switch (storage.Provider)
        {
            case StorageProvider.InMemory:
                // Gives each API instance its own store; otherwise test hosts in one process would share one by name.
                services.AddSingleton<InMemoryDatabaseRoot>();
                services.AddSingleton(RacingSaveGuard.ForInMemory());
                services.AddDbContext<TimeSeriesDbContext>((provider, options) =>
                    options.UseInMemoryDatabase(InMemoryDatabaseName, provider.GetRequiredService<InMemoryDatabaseRoot>()));
                break;

            case StorageProvider.SqlServer:
                var connectionString = string.IsNullOrWhiteSpace(storage.ConnectionString)
                    ? throw new InvalidOperationException($"'{StorageOptions.Section}:{nameof(StorageOptions.ConnectionString)}' is required for SQL Server storage.")
                    : storage.ConnectionString;
                services.AddSingleton(RacingSaveGuard.ForSqlServer());
                // Retry dropped connections, and deadlocks between requests changing the same series.
                services.AddDbContext<TimeSeriesDbContext>(options =>
                    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
                break;

            default:
                throw new InvalidOperationException($"Unknown storage provider '{storage.Provider}'.");
        }

        services.AddScoped<ITimeSeriesRepository, TimeSeriesRepository>();

        return services;
    }

    /// <summary>Applies pending migrations on SQL Server; does nothing for in-memory storage.</summary>
    public static async Task InitializeStorageAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TimeSeriesDbContext>().Database;

        if (database.IsRelational())
        {
            await database.MigrateAsync(cancellationToken);
        }
    }
}
