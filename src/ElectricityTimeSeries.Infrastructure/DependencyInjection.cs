using Microsoft.Extensions.DependencyInjection;

namespace ElectricityTimeSeries.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers data-access implementations (repositories, storage) of the Infrastructure layer.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        return services;
    }
}
