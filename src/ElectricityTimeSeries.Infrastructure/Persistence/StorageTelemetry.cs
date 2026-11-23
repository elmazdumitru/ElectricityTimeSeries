using System.Diagnostics;

namespace ElectricityTimeSeries.Infrastructure.Persistence;

/// <summary>Spans for the storage work. The API decides whether they're recorded and where they go.</summary>
internal static class StorageTelemetry
{
    public static readonly ActivitySource Source = new("ElectricityTimeSeries.Infrastructure");
}
