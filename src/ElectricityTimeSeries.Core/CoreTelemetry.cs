using System.Diagnostics;

namespace ElectricityTimeSeries.Core;

/// <summary>Spans for the business operations. The API decides whether they're recorded and where they go.</summary>
internal static class CoreTelemetry
{
    public static readonly ActivitySource Source = new("ElectricityTimeSeries.Core");

    public static Activity? StartActivity(string name, Guid? seriesId = null)
    {
        var activity = Source.StartActivity(name);
        if (seriesId is not null)
        {
            activity?.SetTag("timeseries.id", seriesId);
        }

        return activity;
    }
}
