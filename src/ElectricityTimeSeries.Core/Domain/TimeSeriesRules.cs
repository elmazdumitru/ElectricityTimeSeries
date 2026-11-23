namespace ElectricityTimeSeries.Core.Domain;

internal static class TimeSeriesRules
{
    private const double SupportedResolution15Minutes = 15;
    private const double SupportedResolution60Minutes = 60;

    private static bool IsSupportedResolution(this TimeSpan timeSpan) => timeSpan.TotalMinutes is
        SupportedResolution15Minutes or
        SupportedResolution60Minutes;

    // Two months of 15-minute values (62 days, as in July and August).
    private const int MaxPoints = 62 * 24 * (60 / 15);

    // Stops reporting point errors after this many, so a bad upload can't produce a huge response.
    private const int MaxPointErrors = 50;

    // Values in MW: 6 decimal places is watt-level precision, and 13 digits before the point is far
    // more than any bidding zone. Every storage keeps values this size exactly (SQL Server: decimal(19,6)).
    internal const int MaxDecimalPlaces = 6;
    internal const decimal MaxValue = 9_999_999_999_999.999999m;

    public static void Validate(TimeSeriesDefinition definition)
    {
        if (definition.Value is null)
        {
            // Only possible through default(TimeSeriesDefinition): a programming mistake, not bad input.
            throw new ArgumentException("The definition holds no time series.", nameof(definition));
        }

        var errors = new ValidationErrors();
        var content = definition.Content;

        if (!content.BiddingZone.IsKnown)
        {
            errors.Add("biddingZone", $"Unknown bidding zone. Known zones: {BiddingZone.ConcatenatedKnownZones}.");
        }

        if (definition is CrossBorderFlowDefinition flow)
        {
            if (!flow.ToBiddingZone.IsKnown)
            {
                errors.Add("toBiddingZone", $"Unknown bidding zone. Known zones: {BiddingZone.ConcatenatedKnownZones}.");
            }
            else if (flow.BiddingZone == flow.ToBiddingZone)
            {
                errors.Add("toBiddingZone", "A cross-border flow must go to a different bidding zone.");
            }
        }

        var resolutionIsValid = content.Resolution.IsSupportedResolution();
        if (!resolutionIsValid)
        {
            errors.Add("resolution", "Supported resolutions are 15 minutes (PT15M) and 1 hour (PT1H).");
        }

        ValidatePoints(content, resolutionIsValid, errors);

        errors.ThrowIfAny();
    }

    private static void ValidatePoints(TimeSeriesContent definition, bool checkTimeSpans, ValidationErrors errors)
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

            if (decimal.Round(point.Value, MaxDecimalPlaces) != point.Value)
            {
                if (AddPointError(i, "value", $"At most {MaxDecimalPlaces} decimal places are allowed."))
                {
                    return;
                }
            }
            else if (point.Value < 0 || point.Value > MaxValue)
            {
                if (AddPointError(i, "value", $"Must be between 0 and {MaxValue:N0}."))
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
