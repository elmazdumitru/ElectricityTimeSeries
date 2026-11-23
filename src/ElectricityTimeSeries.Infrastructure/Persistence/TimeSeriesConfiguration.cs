using ElectricityTimeSeries.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ElectricityTimeSeries.Infrastructure.Persistence;

/// <summary>Table names, sizes, precision and indexes only matter to SQL Server; the in-memory store ignores them.</summary>
internal sealed class TimeSeriesConfiguration : IEntityTypeConfiguration<TimeSeries>
{
    // Used for both the required and the optional zone; EF Core leaves empty values empty.
    private static readonly ValueConverter<BiddingZone, string> BiddingZoneConverter =
        new(zone => zone.ToString(), code => BiddingZone.From(code));

    /// <summary>A hidden version stamp, renewed on every save, so two requests changing one series at once can't both succeed.</summary>
    public const string VersionProperty = "Version";

    public void Configure(EntityTypeBuilder<TimeSeries> builder)
    {
        builder.ToTable("TimeSeries");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property<Guid>(VersionProperty).IsConcurrencyToken();

        builder.Property(s => s.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(s => s.BiddingZone)
            .HasConversion(BiddingZoneConverter)
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(s => s.ToBiddingZone)
            .HasConversion(BiddingZoneConverter)
            .HasMaxLength(16);

        builder.HasIndex(s => new { s.BiddingZone, s.Start, s.End });
        builder.HasIndex(s => new { s.ToBiddingZone, s.Start, s.End });

        builder.OwnsMany(s => s.Points, points =>
        {
            points.ToTable("TimeSeriesPoints");
            points.WithOwner().HasForeignKey("TimeSeriesId");
            points.HasKey("TimeSeriesId", nameof(TimeSeriesPoint.Timestamp));
            // 13 digits before the point and 6 after: the range the business rules allow.
            points.Property(p => p.Value).HasPrecision(19, 6);
        });

        // Points are read and written through the private list, not the public read-only copy.
        builder.Navigation(s => s.Points).HasField("_points").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
