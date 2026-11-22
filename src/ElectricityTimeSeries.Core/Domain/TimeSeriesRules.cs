namespace ElectricityTimeSeries.Core.Domain;

/// <summary>The business rules a time series must follow.</summary>
public static class TimeSeriesRules
{
    /// <summary>The supported resolutions: 15 minutes (the Nordic market time unit) and 1 hour.</summary>
    public static IReadOnlyList<TimeSpan> SupportedResolutions { get; } = [TimeSpan.FromMinutes(15), TimeSpan.FromHours(1)];

    /// <summary>One year of 15-minute values (a leap year has 366 × 96 = 35,136).</summary>
    public const int MaxPoints = 35_136;

    /// <summary>Stops reporting point errors after this many, so a bad upload can't produce a huge response.</summary>
    private const int MaxPointErrors = 10;

    internal static void Validate(TimeSeriesDefinition definition)
    {
        var errors = new ValidationErrors();

        if (!Enum.IsDefined(definition.Type))
        {
            errors.Add("type", "Unknown time series type.");
        }

        if (!BiddingZones.IsKnown(definition.BiddingZone))
        {
            errors.Add("biddingZone", $"Unknown bidding zone. Known zones: {string.Join(", ", BiddingZones.All)}.");
        }

        if (definition.Type == TimeSeriesType.CrossBorderFlow)
        {
            if (string.IsNullOrWhiteSpace(definition.ToBiddingZone))
            {
                errors.Add("toBiddingZone", "A cross-border flow needs the receiving bidding zone.");
            }
            else if (!BiddingZones.IsKnown(definition.ToBiddingZone))
            {
                errors.Add("toBiddingZone", "Unknown bidding zone.");
            }
            else if (string.Equals(definition.BiddingZone?.Trim(), definition.ToBiddingZone.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("toBiddingZone", "A cross-border flow must go to a different bidding zone.");
            }
        }
        else if (definition.ToBiddingZone is not null)
        {
            errors.Add("toBiddingZone", "Only cross-border flows have a receiving bidding zone.");
        }

        var resolutionIsValid = SupportedResolutions.Contains(definition.Resolution);
        if (!resolutionIsValid)
        {
            errors.Add("resolution", "Supported resolutions are 15 minutes (PT15M) and 1 hour (PT1H).");
        }

        ValidatePoints(definition, resolutionIsValid, errors);

        errors.ThrowIfAny();
    }


    private static void ValidatePoints(TimeSeriesDefinition definition, bool resolutionIsValid, ValidationErrors errors)
    {
        var points = definition.Points;

        if (points is null || points.Count == 0)
        {
            errors.Add("points", "At least one point is required.");
            return;
        }

        if (points.Count > MaxPoints)
        {
            errors.Add("points", $"At most {MaxPoints} points are allowed in one time series.");
            return;
        }

        var pointErrors = 0;

        bool AddPointError(int index, string field, string message)
        {
            if (pointErrors >= MaxPointErrors)
            {
                return true;
            }

            errors.Add($"points[{index}].{field}", message);
            pointErrors++;

            return pointErrors >= MaxPointErrors;
        }

        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];

            if (definition.Type != TimeSeriesType.CrossBorderFlow && point.Value < 0)
            {
                if (AddPointError(i, "value", "Production and consumption can't be negative."))
                {
                    return;
                }
            }

            if (!resolutionIsValid)
            {
                continue;
            }

            if (point.Timestamp.UtcTicks % definition.Resolution.Ticks != 0)
            {
                if (AddPointError(i, "timestamp", $"Must start on a {definition.Resolution.TotalMinutes:0}-minute boundary."))
                {
                    return;
                }
            }
            else if (i > 0 && point.Timestamp - points[i - 1].Timestamp != definition.Resolution)
            {
                if (AddPointError(i, "timestamp", "Must follow the previous point by exactly one resolution step (no gaps, duplicates or reordering)."))
                {
                    return;
                }
            }
        }
    }
}
