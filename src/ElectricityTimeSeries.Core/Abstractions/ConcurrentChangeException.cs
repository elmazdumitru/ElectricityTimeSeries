namespace ElectricityTimeSeries.Core.Abstractions;

/// <summary>The change was not saved because another request changed the series first. Reloading and retrying is safe.</summary>
public sealed class ConcurrentChangeException(Guid id, Exception? innerException = null)
    : Exception($"Time series '{id}' was changed by another request at the same time.", innerException)
{
    public Guid Id { get; } = id;
}
