using ElectricityTimeSeries.Core.Abstractions;
using ElectricityTimeSeries.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace ElectricityTimeSeries.Infrastructure;

public static class DependencyInjection
{
    private const string DatabaseName = "ElectricityTimeSeries";

    /// <summary>
    /// Registers data-access implementations (repositories, storage) of the Infrastructure layer.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryDatabaseRoot>();

        // Storage is kept in memory for now. Moving to SQL Server means replacing this call with
        // UseSqlServer(connectionString) and adding migrations; nothing outside this project changes.
        services.AddDbContext<TimeSeriesDbContext>((provider, options) =>
            options.UseInMemoryDatabase(DatabaseName, provider.GetRequiredService<InMemoryDatabaseRoot>()));

        services.AddScoped<ITimeSeriesRepository, TimeSeriesRepository>();

        return services;
    }
}
