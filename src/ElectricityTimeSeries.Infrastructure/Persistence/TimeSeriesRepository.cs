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

    public async Task<TimeSeriesPage> FindPointsAsync(TimeSeriesFilter filter, int skip, int take, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("TimeSeriesRepository.FindPoints");
        var series = _context.TimeSeries.AsNoTracking();

        if (filter.BiddingZone is { } zone)
        {
            series = series.Where(s => s.BiddingZone == zone || s.ToBiddingZone == zone);
        }

        if (filter.Type is { } type)
        {
            series = series.Where(s => s.Type == type);
        }

        // Narrows down to the series whose period overlaps the interval, then to the points whose period does.
        if (filter.From is { } from)
        {
            series = series.Where(s => s.End > from);
        }

        if (filter.To is { } to)
        {
            series = series.Where(s => s.Start < to);
        }

        var points = series.SelectMany(s => s.Points, (s, p) => new { Series = s, Point = p });

        if (filter.From is { } pointsFrom)
        {
            // A point matches when it ends after 'from', so when it starts less than one resolution before it.
            // SQL Server can't subtract a column's duration, so the two allowed resolutions are spelled out.
            var hourBefore = Before(pointsFrom, OneHour);
            var quarterBefore = Before(pointsFrom, FifteenMinutes);
            points = points.Where(x => x.Point.Timestamp > hourBefore
                && (x.Series.Resolution == OneHour || x.Point.Timestamp > quarterBefore));
        }

        if (filter.To is { } pointsTo)
        {
            points = points.Where(x => x.Point.Timestamp < pointsTo);
        }

        // Two queries, not one transaction: the total is the count at the moment of the request.
        var total = await points.CountAsync(cancellationToken);
        var rows = await points
            .OrderBy(x => x.Series.Start)
            .ThenBy(x => x.Series.Id)
            .ThenBy(x => x.Point.Timestamp)
            .Skip(skip)
            .Take(take)
            .Select(x => new
            {
                x.Series.Id,
                x.Series.Type,
                x.Series.BiddingZone,
                x.Series.ToBiddingZone,
                x.Series.Resolution,
                x.Series.Start,
                x.Series.End,
                x.Point.Timestamp,
                x.Point.Value,
            })
            .ToListAsync(cancellationToken);

        var slices = rows
            .GroupBy(r => r.Id)
            .Select(g =>
            {
                var first = g.First();
                return new TimeSeriesSlice(
                    first.Id, first.Type, first.BiddingZone, first.ToBiddingZone, first.Resolution, first.Start, first.End,
                    g.Select(r => new TimeSeriesPoint(r.Timestamp, r.Value)).ToList());
            })
            .ToList();

        activity?.SetTag("timeseries.count", slices.Count);
        activity?.SetTag("timeseries.points.total", total);
        return new TimeSeriesPage(slices, total);
    }

    public async Task AddAsync(TimeSeries series, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("TimeSeriesRepository.Add", series.Id);
        _context.TimeSeries.Add(series);
        StampNewVersion(series);
        await SaveWithoutOverlapsAsync(
            series,
            async () =>
            {
                await SaveChangesAsync(cancellationToken);
                return true;
            },
            cancellationToken);
    }

    public async Task<bool> UpdateAsync(TimeSeries series, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("TimeSeriesRepository.Update", series.Id);
        if (_context.Entry(series).State == EntityState.Detached)
        {
            _context.TimeSeries.Update(series);
        }

        StampNewVersion(series);
        return await SaveWithoutOverlapsAsync(series, () => SaveLoadedAsync(series, cancellationToken), cancellationToken);
    }

    public async Task<bool> DeleteAsync(TimeSeries series, CancellationToken cancellationToken)
    {
        using var activity = StartActivity("TimeSeriesRepository.Delete", series.Id);
        if (_context.Database.IsInMemory())
        {
            _context.TimeSeries.Remove(series);
            return await _racingSaves.RunAsync(() => SaveLoadedAsync(series, cancellationToken), cancellationToken);
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

    private static readonly TimeSpan FifteenMinutes = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);

    // Stops at the earliest time there is, for a 'from' in the first hour of year 1.
    private static DateTimeOffset Before(DateTimeOffset time, TimeSpan span) =>
        time.UtcTicks - span.Ticks < DateTimeOffset.MinValue.UtcTicks ? DateTimeOffset.MinValue : time - span;

    private void StampNewVersion(TimeSeries series) =>
        _context.Entry(series).Property(TimeSeriesConfiguration.VersionProperty).CurrentValue = Guid.NewGuid();

    private Guid LoadedVersion(TimeSeries series) =>
        (Guid)_context.Entry(series).Property(TimeSeriesConfiguration.VersionProperty).OriginalValue!;

    /// <summary>Saves a series loaded with GetAsync. Callers run it in the racing-save guard or a transaction.</summary>
    private async Task<bool> SaveLoadedAsync(TimeSeries series, CancellationToken cancellationToken)
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
    }

    /// <summary>Checks for overlaps and saves as one step, so two overlapping saves can't both get through.</summary>
    private async Task<bool> SaveWithoutOverlapsAsync(TimeSeries series, Func<Task<bool>> save, CancellationToken cancellationToken)
    {
        if (_context.Database.IsInMemory())
        {
            return await _racingSaves.RunAsync(
                async () =>
                {
                    await ThrowIfOverlapsAsync(series, cancellationToken);
                    return await save();
                },
                cancellationToken);
        }

        return await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // One writer at a time per type and zones, until the transaction ends.
            var lockName = $"time-series:{series.Type}:{series.BiddingZone}:{series.ToBiddingZone}";
            await _context.Database.ExecuteSqlAsync(
                $"EXEC sp_getapplock @Resource = {lockName}, @LockMode = 'Exclusive', @LockOwner = 'Transaction'",
                cancellationToken);

            await ThrowIfOverlapsAsync(series, cancellationToken);
            var saved = await save();

            // A save that lost a race (deleted meanwhile) may have had its transaction rolled back by SQL Server.
            if (saved)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return saved;
        });
    }

    private async Task ThrowIfOverlapsAsync(TimeSeries series, CancellationToken cancellationToken)
    {
        using var activity = StorageTelemetry.Source.StartActivity("FindOverlaps");
        var (id, type, zone, toZone, start, end) = (series.Id, series.Type, series.BiddingZone, series.ToBiddingZone, series.Start, series.End);

        var overlapping = await _context.TimeSeries.AsNoTracking()
            .Where(s => s.Id != id && s.Type == type && s.BiddingZone == zone && s.ToBiddingZone == toZone)
            .Where(s => s.Start < end && s.End > start)
            .OrderBy(s => s.Start)
            .Select(s => s.Id)
            .Take(10)
            .ToListAsync(cancellationToken);

        if (overlapping.Count > 0)
        {
            throw new OverlappingSeriesException(overlapping);
        }
    }

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
