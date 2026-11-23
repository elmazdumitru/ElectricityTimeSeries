namespace ElectricityTimeSeries.Core.Domain;

/// <summary>
/// The bidding zones the API accepts: the Nordic zones and the neighbouring zones they are connected to.
/// </summary>
public static class BiddingZones
{
    private static readonly HashSet<string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        "NO1", "NO2", "NO3", "NO4", "NO5",
        "SE1", "SE2", "SE3", "SE4",
        "FI", "DK1", "DK2",
        "EE", "LV", "LT", "PL", "DE-LU", "NL", "GB",
    };

    public static IReadOnlyCollection<string> All => Known;

    public static bool IsKnown(string? zone) => zone is not null && Known.Contains(zone.Trim());

    /// <summary>Returns the zone code in its standard form, for example "no1" becomes "NO1".</summary>
    public static string Normalize(string zone) => zone.Trim().ToUpperInvariant();
}
