namespace ElectricityTimeSeries.Infrastructure;

public sealed class StorageOptions
{
    public const string Section = "Storage";

    public StorageProvider Provider { get; init; } = StorageProvider.InMemory;

    /// <summary>Required when <see cref="Provider"/> is <see cref="StorageProvider.SqlServer"/>.</summary>
    public string? ConnectionString { get; init; }
}

public enum StorageProvider
{
    /// <summary>Lost when the API stops. Needs nothing installed.</summary>
    InMemory,

    /// <summary>Created and updated from the EF Core migrations.</summary>
    SqlServer,
}
