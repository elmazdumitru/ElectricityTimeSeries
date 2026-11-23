using System.Diagnostics;
using ElectricityTimeSeries.Core.Abstractions;
using ElectricityTimeSeries.Core.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ElectricityTimeSeries.Infrastructure.Persistence;

internal sealed class TimeSeriesRepository : ITimeSeriesRepository
{
    private readonly TimeSeriesDbContext _context;
    private readonly RacingSaveGuard _racingSaves;

    public TimeSeriesRepository(TimeSeriesDbContext context, RacingSaveGuard racingSaves)
    {
        _context = context;
        _racingSaves = racingSaves;
    }

    public async Task<TimeSeries?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("TimeSeriesRepository.Get", id);
        return await _context.TimeSeries.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<TimeSeries>> FindAsync(TimeSeriesFilter filter, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("TimeSeriesRepository.Find");
        var query = _context.TimeSeries.AsNoTracking();

        if (filter.BiddingZone is { } zone)
        {
            query = query.Where(s => s.BiddingZone == zone || s.ToBiddingZone == zone);
        }

        if (filter.Type is { } type)
        {
            query = query.Where(s => s.Type == type);
        }

        // A series matches when its period overlaps the requested interval.
        if (filter.From is { } from)
        {
            query = query.Where(s => s.End > from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(s => s.Start < to);
        }

        var matches = await query
            .OrderBy(s => s.Start)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);
        activity?.SetTag("timeseries.count", matches.Count);
        return matches;
    }

    public async Task AddAsync(TimeSeries series, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("TimeSeriesRepository.Add", series.Id);
        _context.TimeSeries.Add(series);
        StampNewVersion(series);
        await SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(TimeSeries series, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("TimeSeriesRepository.Update", series.Id);
        if (_context.Entry(series).State == EntityState.Detached)
        {
            _context.TimeSeries.Update(series);
        }

        StampNewVersion(series);
        return await SaveRacingChangesAsync(series, cancellationToken);
    }

    public async Task<bool> DeleteAsync(TimeSeries series, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("TimeSeriesRepository.Delete", series.Id);
        if (_context.Database.IsInMemory())
        {
            _context.TimeSeries.Remove(series);
            return await SaveRacingChangesAsync(series, cancellationToken);
        }

        // One statement: the database deletes the points (ON DELETE CASCADE), not EF one by one.
        var loadedVersion = LoadedVersion(series);
        var deleted = await _context.TimeSeries
            .Where(s => s.Id == series.Id && EF.Property<Guid>(s, TimeSeriesConfiguration.VersionProperty) == loadedVersion)
            .ExecuteDeleteAsync(cancellationToken);
        if (deleted > 0)
        {
            return true;
        }

        var stillExists = await _context.TimeSeries.AsNoTracking().AnyAsync(s => s.Id == series.Id, cancellationToken);
        return stillExists ? throw new ConcurrentChangeException(series.Id) : false;
    }

    private void StampNewVersion(TimeSeries series) =>
        _context.Entry(series).Property(TimeSeriesConfiguration.VersionProperty).CurrentValue = Guid.NewGuid();

    private Guid LoadedVersion(TimeSeries series) =>
        (Guid)_context.Entry(series).Property(TimeSeriesConfiguration.VersionProperty).OriginalValue!;

    private Task<bool> SaveRacingChangesAsync(TimeSeries series, CancellationToken cancellationToken) =>
        _racingSaves.RunAsync(
            async () =>
            {
                if (_racingSaves.ChecksVersionFirst)
                {
                    var loadedVersion = LoadedVersion(series);
                    var storedVersion = await ReadStoredVersionAsync(series.Id, cancellationToken);

                    if (storedVersion is null)
                    {
                        return false;
                    }

                    if (storedVersion != loadedVersion)
                    {
                        throw new ConcurrentChangeException(series.Id);
                    }
                }

                try
                {
                    await SaveChangesAsync(cancellationToken);
                    return true;
                }
                catch (DbUpdateException exception) when (IsRace(exception))
                {
                    var stillExists = await _context.TimeSeries.AsNoTracking().AnyAsync(s => s.Id == series.Id, cancellationToken);
                    return stillExists ? throw new ConcurrentChangeException(series.Id, exception) : false;
                }
            },
            cancellationToken);

    private async Task<Guid?> ReadStoredVersionAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = StorageTelemetry.Source.StartActivity("ReadStoredVersion");
        return await _context.TimeSeries.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => (Guid?)EF.Property<Guid>(s, TimeSeriesConfiguration.VersionProperty))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        using var activity = StorageTelemetry.Source.StartActivity("SaveChanges");
        await _context.SaveChangesAsync(cancellationToken);
    }

    private Activity? StartActivity(string name, Guid? seriesId = null)
    {
        var activity = StorageTelemetry.Source.StartActivity(name);
        activity?.SetTag("storage.provider", _context.Database.IsInMemory() ? "InMemory" : "SqlServer");
        if (seriesId is not null)
        {
            activity?.SetTag("timeseries.id", seriesId);
        }

        return activity;
    }

    private static bool IsRace(DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException ||
        exception.InnerException is SqlException
        {
            Number: 2601 or 2627 // duplicate key: another request inserted the same point at the same time
                or 1205          // deadlock: SQL Server picked this request to give way
        };
}
