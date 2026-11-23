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

    public Task<TimeSeries?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _context.TimeSeries.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TimeSeries>> FindAsync(TimeSeriesFilter filter, CancellationToken cancellationToken)
    {
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

        return await query
            .OrderBy(s => s.Start)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TimeSeries series, CancellationToken cancellationToken)
    {
        _context.TimeSeries.Add(series);
        StampNewVersion(series);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(TimeSeries series, CancellationToken cancellationToken)
    {
        if (_context.Entry(series).State == EntityState.Detached)
        {
            _context.TimeSeries.Update(series);
        }

        StampNewVersion(series);
        return await SaveRacingChangesAsync(series, cancellationToken);
    }

    public async Task<bool> DeleteAsync(TimeSeries series, CancellationToken cancellationToken)
    {
        _context.TimeSeries.Remove(series);
        return await SaveRacingChangesAsync(series, cancellationToken);
    }

    private void StampNewVersion(TimeSeries series) =>
        _context.Entry(series).Property(TimeSeriesConfiguration.VersionProperty).CurrentValue = Guid.NewGuid();

    private Task<bool> SaveRacingChangesAsync(TimeSeries series, CancellationToken cancellationToken) =>
        _racingSaves.RunAsync(
            async () =>
            {
                if (_racingSaves.ChecksVersionFirst)
                {
                    var loadedVersion = (Guid)_context.Entry(series).Property(TimeSeriesConfiguration.VersionProperty).OriginalValue!;
                    var storedVersion = await _context.TimeSeries.AsNoTracking()
                        .Where(s => s.Id == series.Id)
                        .Select(s => (Guid?)EF.Property<Guid>(s, TimeSeriesConfiguration.VersionProperty))
                        .FirstOrDefaultAsync(cancellationToken);

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
                    await _context.SaveChangesAsync(cancellationToken);
                    return true;
                }
                catch (DbUpdateException exception) when (IsRace(exception))
                {
                    var stillExists = await _context.TimeSeries.AsNoTracking().AnyAsync(s => s.Id == series.Id, cancellationToken);
                    return stillExists ? throw new ConcurrentChangeException(series.Id, exception) : false;
                }
            },
            cancellationToken);

    private static bool IsRace(DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException ||
        exception.InnerException is SqlException
        {
            Number: 2601 or 2627 // duplicate key: another request inserted the same point at the same time
                or 1205          // deadlock: SQL Server picked this request to give way
        };
}
