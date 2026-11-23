using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ElectricityTimeSeries.Infrastructure.Persistence;

/// <summary>Only for <c>dotnet ef</c> migrations. The connection string is never opened, so no database is needed.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TimeSeriesDbContext>
{
    public TimeSeriesDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<TimeSeriesDbContext>()
            .UseSqlServer("Server=design-time-only;Database=ElectricityTimeSeries")
            .Options);
}
