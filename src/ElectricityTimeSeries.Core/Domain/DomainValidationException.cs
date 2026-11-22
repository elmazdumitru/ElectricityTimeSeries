namespace ElectricityTimeSeries.Core.Domain;

/// <summary>
/// Thrown when input breaks a business rule. <see cref="Errors"/> maps each invalid field
/// (for example "points[3].timestamp") to what is wrong with it.
/// </summary>
public sealed class DomainValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more business rules were broken.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
