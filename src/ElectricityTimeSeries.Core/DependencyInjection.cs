using ElectricityTimeSeries.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ElectricityTimeSeries.Core;

public static class DependencyInjection
{
    /// <summary>
    /// Registers business-logic services (domain services, validators) of the Core layer.
    /// </summary>
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddScoped<TimeSeriesService>();
        return services;
    }
}
