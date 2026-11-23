using ElectricityTimeSeries.Core.Domain;

namespace ElectricityTimeSeries.Core.Abstractions;

/// <summary>Stores and loads time series. Implemented by the Infrastructure layer.</summary>
public interface ITimeSeriesRepository
{
    Task<TimeSeries?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Returns the series that match the filter, ordered by start time.</summary>
    Task<IReadOnlyList<TimeSeries>> FindAsync(TimeSeriesFilter filter, CancellationToken cancellationToken);

    Task AddAsync(TimeSeries series, CancellationToken cancellationToken);

    Task UpdateAsync(TimeSeries series, CancellationToken cancellationToken);

    Task DeleteAsync(TimeSeries series, CancellationToken cancellationToken);
}
