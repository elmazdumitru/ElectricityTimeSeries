using ElectricityTimeSeries.Core.Domain;

namespace ElectricityTimeSeries.Core.Abstractions;

public interface ITimeSeriesRepository
{
    Task<TimeSeries?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Results are ordered by start time.</summary>
    Task<IReadOnlyList<TimeSeries>> FindAsync(TimeSeriesFilter filter, CancellationToken cancellationToken);

    Task AddAsync(TimeSeries series, CancellationToken cancellationToken);

    /// <summary>Saves a series loaded with <see cref="GetAsync"/>. Returns false if it was deleted meanwhile,
    /// and throws <see cref="ConcurrentChangeException"/> if it was changed meanwhile.</summary>
    Task<bool> UpdateAsync(TimeSeries series, CancellationToken cancellationToken);

    /// <summary>Deletes a series loaded with <see cref="GetAsync"/>. Returns false if it was deleted meanwhile,
    /// and throws <see cref="ConcurrentChangeException"/> if it was changed meanwhile.</summary>
    Task<bool> DeleteAsync(TimeSeries series, CancellationToken cancellationToken);
}
