namespace ElectricityTimeSeries.Core.Abstractions;

/// <summary>The series was not saved because others of the same type and zones already cover part of its period.</summary>
public sealed class OverlappingSeriesException : Exception
{
    public OverlappingSeriesException(IReadOnlyList<Guid> overlappingIds)
        : base("The time series overlaps others of the same type and zones.")
    {
        OverlappingIds = overlappingIds;
    }

    public IReadOnlyList<Guid> OverlappingIds { get; }
}
