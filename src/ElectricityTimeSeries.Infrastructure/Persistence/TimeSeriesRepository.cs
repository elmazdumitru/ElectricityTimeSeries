using ElectricityTimeSeries.Core.Abstractions;
using ElectricityTimeSeries.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace ElectricityTimeSeries.Infrastructure.Persistence;

internal sealed class TimeSeriesRepository : ITimeSeriesRepository
{
    private readonly TimeSeriesDbContext _context;

    public TimeSeriesRepository(TimeSeriesDbContext context)
    {
        _context = context;
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
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TimeSeries series, CancellationToken cancellationToken)
    {
        if (_context.Entry(series).State == EntityState.Detached)
        {
            _context.TimeSeries.Update(series);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(TimeSeries series, CancellationToken cancellationToken)
    {
        _context.TimeSeries.Remove(series);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
