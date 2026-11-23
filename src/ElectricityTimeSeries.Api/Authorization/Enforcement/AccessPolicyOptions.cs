namespace ElectricityTimeSeries.Api.Authorization.Enforcement;

public sealed class AccessPolicyOptions
{
    public const string Section = "AccessPolicy";

    /// <summary>Relative paths are resolved against the content root.</summary>
    public string LedgerPath { get; init; } = "access-ledger.json";
}
