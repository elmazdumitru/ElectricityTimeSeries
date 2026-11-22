namespace ElectricityTimeSeries.Core.Domain;

/// <summary>The business rules a time series must follow.</summary>
internal static class TimeSeriesRules
{
    private const double SupportedResolution15Minutes = 15;
    private const double SupportedResolution60Minutes = 60;

    private static bool IsSupportedResolution(this TimeSpan timeSpan) => timeSpan.TotalMinutes is
        SupportedResolution15Minutes or
        SupportedResolution60Minutes;

    // A leap year's worth of 15 minute intervals.
    private const int MaxPoints = 366 * 24 * (60 / 15);

    // Stops reporting point errors after this many, so a bad upload can't produce a huge response.
    private const int MaxPointErrors = 50;

    public static void Validate(TimeSeriesDefinition definition)
    {
        var errors = new ValidationErrors();

        if (!Enum.IsDefined(definition.Type))
        {
            errors.Add("type", "Unknown time series type.");
        }

        if (!definition.BiddingZone.IsKnown)
        {
            errors.Add("biddingZone", $"Unknown bidding zone. Known zones: {BiddingZone.ConcatenatedKnownZones}.");
        }

        if (definition.Type is TimeSeriesType.CrossBorderFlow)
        {
            if (definition.ToBiddingZone is not { IsKnown: true })
            {
                errors.Add("toBiddingZone", $"A cross-border flow needs a known receiving bidding zone. Known zones: {BiddingZone.ConcatenatedKnownZones}.");
            }
            else if (definition.BiddingZone == definition.ToBiddingZone)
            {
                errors.Add("toBiddingZone", "A cross-border flow must go to a different bidding zone.");
            }
        }
        else if (definition.ToBiddingZone is not null)
        {
            errors.Add("toBiddingZone", "Only cross-border flows have a receiving bidding zone.");
        }

        var resolutionIsValid = definition.Resolution.IsSupportedResolution();
        if (!resolutionIsValid)
        {
            errors.Add("resolution", "Supported resolutions are 15 minutes (PT15M) and 1 hour (PT1H).");
        }

        ValidatePoints(definition, resolutionIsValid, errors);

        errors.ThrowIfAny();
    }

    private static void ValidatePoints(TimeSeriesDefinition definition, bool checkTimeSpans, ValidationErrors errors)
    {
        var points = definition.Points;

        if (points is not { Count: > 0 })
        {
            errors.Add("points", "At least one point is required.");
            return;
        }

        if (points.Count > MaxPoints)
        {
            errors.Add("points", $"At most {MaxPoints} points are allowed in one time series.");
            return;
        }

        int pointErrors = 0;

        for (int i = 0; i < points.Count; i++)
        {
            var point = points[i];

            if (definition.Type is not TimeSeriesType.CrossBorderFlow && point.Value < 0)
            {
                if (AddPointError(i, "value", "Production and consumption can't be negative."))
                {
                    return;
                }
            }

            if (checkTimeSpans)
            {
                if (point.Timestamp.UtcTicks % definition.Resolution.Ticks is not 0)
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
    }
}
