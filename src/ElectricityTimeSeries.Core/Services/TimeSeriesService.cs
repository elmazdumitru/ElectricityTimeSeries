using ElectricityTimeSeries.Core.Abstractions;
using ElectricityTimeSeries.Core.Domain;

namespace ElectricityTimeSeries.Core.Services;

public sealed class TimeSeriesService
{
    /// <summary>The most points a search returns at once, and the default.</summary>
    public const int MaxPageSize = 1000;

    private readonly ITimeSeriesRepository _repository;

    public TimeSeriesService(ITimeSeriesRepository repository)
    {
        _repository = repository;
    }

    public async Task<TimeSeries> CreateAsync(TimeSeriesDefinition definition, CancellationToken cancellationToken)
    {
        using var activity = CoreTelemetry.StartActivity("TimeSeriesService.Create");
        var series = TimeSeries.Create(definition);
        activity?.SetTag("timeseries.id", series.Id);
        await _repository.AddAsync(series, cancellationToken);
        return series;
    }

    public async Task<TimeSeries?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = CoreTelemetry.StartActivity("TimeSeriesService.Get", id);
        return await _repository.GetAsync(id, cancellationToken);
    }

    /// <summary>One page of matching points; <paramref name="skip"/> and <paramref name="take"/> count points, not series.</summary>
    public async Task<TimeSeriesPage> SearchAsync(TimeSeriesFilter filter, int skip, int take, CancellationToken cancellationToken)
    {
        using var activity = CoreTelemetry.StartActivity("TimeSeriesService.Search");
        var errors = new ValidationErrors();

        if (filter.BiddingZone is { IsKnown: false })
        {
            errors.Add("biddingZone", $"Unknown bidding zone. Known zones: {BiddingZone.ConcatenatedKnownZones}.");
        }

        if (filter.Type is { } type && !Enum.IsDefined(type))
        {
            errors.Add("type", "Unknown time series type.");
        }

        if (filter.From >= filter.To)
        {
            errors.Add("from", "Must be earlier than 'to'.");
        }

        if (skip < 0)
        {
            errors.Add("skip", "Must be 0 or more.");
        }

        if (take is < 1 or > MaxPageSize)
        {
            errors.Add("take", $"Must be between 1 and {MaxPageSize}.");
        }

        errors.ThrowIfAny();

        var normalized = filter with
        {
            From = filter.From?.ToUniversalTime(),
            To = filter.To?.ToUniversalTime(),
        };

        var page = await _repository.FindPointsAsync(normalized, skip, take, cancellationToken);
        activity?.SetTag("timeseries.count", page.Series.Count);
        return page;
    }

    public async Task<TimeSeries?> UpdateAsync(Guid id, TimeSeriesDefinition definition, CancellationToken cancellationToken)
    {
        using var activity = CoreTelemetry.StartActivity("TimeSeriesService.Update", id);
        var series = await _repository.GetAsync(id, cancellationToken);
        if (series is null)
        {
            return null;
        }

        series.Update(definition);

        // Deleted by another request between loading and saving: the same as not found.
        return await _repository.UpdateAsync(series, cancellationToken) ? series : null;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = CoreTelemetry.StartActivity("TimeSeriesService.Delete", id);
        var series = await _repository.GetAsync(id, cancellationToken);
        if (series is null)
        {
            return false;
        }

        return await _repository.DeleteAsync(series, cancellationToken);
    }
}
