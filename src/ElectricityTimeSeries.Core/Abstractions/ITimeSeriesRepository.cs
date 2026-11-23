using ElectricityTimeSeries.Core.Domain;

namespace ElectricityTimeSeries.Core.Abstractions;

public interface ITimeSeriesRepository
{
    Task<TimeSeries?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The matching points, ordered by series start, series id and time; <paramref name="skip"/> and
    /// <paramref name="take"/> count points. A point matches when its own period overlaps the interval.</summary>
    Task<TimeSeriesPage> FindPointsAsync(TimeSeriesFilter filter, int skip, int take, CancellationToken cancellationToken);

    /// <summary>Throws <see cref="OverlappingSeriesException"/> if a stored series of the same type and zones overlaps it.</summary>
    Task AddAsync(TimeSeries series, CancellationToken cancellationToken);

    /// <summary>Saves a series loaded with <see cref="GetAsync"/>. Returns false if it was deleted meanwhile,
    /// and throws <see cref="ConcurrentChangeException"/> if it was changed meanwhile, or
    /// <see cref="OverlappingSeriesException"/> if it would overlap another series.</summary>
    Task<bool> UpdateAsync(TimeSeries series, CancellationToken cancellationToken);

    /// <summary>Deletes a series loaded with <see cref="GetAsync"/>. Returns false if it was deleted meanwhile,
    /// and throws <see cref="ConcurrentChangeException"/> if it was changed meanwhile.</summary>
    Task<bool> DeleteAsync(TimeSeries series, CancellationToken cancellationToken);
}
