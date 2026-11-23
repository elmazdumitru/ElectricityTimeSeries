using ElectricityTimeSeries.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ElectricityTimeSeries.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddScoped<TimeSeriesService>();
        return services;
    }
}
