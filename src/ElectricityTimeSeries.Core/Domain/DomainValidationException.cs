namespace ElectricityTimeSeries.Core.Domain;

/// <summary><see cref="Errors"/> maps each bad field, such as "points[3].timestamp", to its messages.</summary>
public sealed class DomainValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more business rules were broken.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
