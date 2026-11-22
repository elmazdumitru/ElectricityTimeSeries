using ElectricityTimeSeries.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElectricityTimeSeries.Infrastructure.Persistence;

/// <summary>
/// Describes how a <see cref="TimeSeries"/> is stored, so the domain class itself stays free of
/// storage details. The sizes, precision and indexes are ignored by the in-memory store, but are
/// what a relational database such as SQL Server would use.
/// </summary>
internal sealed class TimeSeriesConfiguration : IEntityTypeConfiguration<TimeSeries>
{
    public void Configure(EntityTypeBuilder<TimeSeries> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Type).HasConversion<string>().HasMaxLength(32);
        // Bidding zones are stored as their text code.
        builder.Property(s => s.BiddingZone)
            .HasConversion(zone => zone.Value, code => BiddingZone.From(code))
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(s => s.ToBiddingZone)
            .HasConversion(zone => zone!.Value, code => BiddingZone.From(code))
            .HasMaxLength(16);

        // The columns used by searches.
        builder.HasIndex(s => new { s.BiddingZone, s.Start, s.End });
        builder.HasIndex(s => new { s.ToBiddingZone, s.Start, s.End });

        builder.OwnsMany(s => s.Points, points =>
        {
            points.WithOwner().HasForeignKey("TimeSeriesId");
            points.HasKey("TimeSeriesId", nameof(TimeSeriesPoint.Timestamp));
            points.Property(p => p.Value).HasPrecision(18, 3);
        });

        // Points are read and written through the private list, not the public read-only copy.
        builder.Navigation(s => s.Points).HasField("_points").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
