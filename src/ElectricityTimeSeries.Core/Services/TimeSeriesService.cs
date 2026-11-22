using ElectricityTimeSeries.Core.Abstractions;
using ElectricityTimeSeries.Core.Domain;

namespace ElectricityTimeSeries.Core.Services;

/// <summary>A series found by a search, with only the points inside the requested time interval.</summary>
public sealed record TimeSeriesSearchResult(TimeSeries Series, IReadOnlyList<TimeSeriesPoint> Points);

/// <summary>The use cases of the API: create, read, search, update and delete time series.</summary>
public sealed class TimeSeriesService
{
    private readonly ITimeSeriesRepository _repository;

    public TimeSeriesService(ITimeSeriesRepository repository)
    {
        _repository = repository;
    }

    /// <exception cref="DomainValidationException">The definition breaks a business rule.</exception>
    public async Task<TimeSeries> CreateAsync(TimeSeriesDefinition definition, CancellationToken cancellationToken)
    {
        var series = TimeSeries.Create(definition);
        await _repository.AddAsync(series, cancellationToken);
        return series;
    }

    public Task<TimeSeries?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _repository.GetAsync(id, cancellationToken);

    /// <summary>
    /// Finds the series matching the filter. When a time interval is given, each result only
    /// contains the points inside it.
    /// </summary>
    /// <exception cref="DomainValidationException">The filter is invalid.</exception>
    public async Task<IReadOnlyList<TimeSeriesSearchResult>> SearchAsync(TimeSeriesFilter filter, CancellationToken cancellationToken)
    {
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

        errors.ThrowIfAny();

        var normalized = filter with
        {
            From = filter.From?.ToUniversalTime(),
            To = filter.To?.ToUniversalTime(),
        };

        var matches = await _repository.FindAsync(normalized, cancellationToken);

        return matches
            .Select(series => new TimeSeriesSearchResult(series, series.PointsWithin(normalized.From, normalized.To)))
            .ToList();
    }

    /// <summary>Replaces the contents of an existing series.</summary>
    /// <returns>The updated series, or <c>null</c> if it doesn't exist.</returns>
    /// <exception cref="DomainValidationException">The definition breaks a business rule.</exception>
    public async Task<TimeSeries?> UpdateAsync(Guid id, TimeSeriesDefinition definition, CancellationToken cancellationToken)
    {
        var series = await _repository.GetAsync(id, cancellationToken);
        if (series is null)
        {
            return null;
        }

        series.Update(definition);
        await _repository.UpdateAsync(series, cancellationToken);
        return series;
    }

    /// <returns><c>false</c> if the series doesn't exist.</returns>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var series = await _repository.GetAsync(id, cancellationToken);
        if (series is null)
        {
            return false;
        }

        await _repository.DeleteAsync(series, cancellationToken);
        return true;
    }
}
