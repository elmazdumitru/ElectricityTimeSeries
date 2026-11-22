using ElectricityTimeSeries.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace ElectricityTimeSeries.Infrastructure.Persistence;

internal sealed class TimeSeriesDbContext : DbContext
{
    public TimeSeriesDbContext(DbContextOptions<TimeSeriesDbContext> options)
        : base(options)
    {
    }

    public DbSet<TimeSeries> TimeSeries => Set<TimeSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TimeSeriesDbContext).Assembly);
}
