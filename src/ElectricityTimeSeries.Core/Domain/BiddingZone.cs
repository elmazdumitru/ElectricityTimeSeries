using System.Diagnostics.CodeAnalysis;

namespace ElectricityTimeSeries.Core.Domain;

/// <summary>
/// A bidding zone code, always trimmed and in upper case ("no1 " becomes "NO1").
/// It may hold an unknown code, so the business rules can report it as a validation error;
/// check <see cref="IsKnown"/> before relying on it.
/// </summary>
/// <remarks>
/// As a record, equality and hash codes come from <see cref="Value"/>. Every instance is
/// normalized when it is created, so that plain exact comparison is all that's needed.
/// </remarks>
public sealed record BiddingZone : IParsable<BiddingZone>
{
    public const string ConcatenatedKnownZones = KnownZones.Concatenated;

    private BiddingZone(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public bool IsKnown => KnownZones.IsKnown(Value);

    public static BiddingZone From(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return new BiddingZone(code.Trim().ToUpperInvariant());
    }

    /// <summary>Same as <see cref="From"/>. Lets ASP.NET Core read a zone from the URL.</summary>
    public static BiddingZone Parse(string s, IFormatProvider? provider) => From(s);

    /// <summary>Succeeds for any text, known or not; only <c>null</c> fails.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out BiddingZone result)
    {
        result = s is null ? null : From(s);
        return result is not null;
    }

    public override string ToString() => Value;

    private static class KnownZones
    {
        public const string Concatenated =
            $"{Norway1}, {Norway2}, {Norway3}, {Norway4}, {Norway5}, " +
            $"{Sweden1}, {Sweden2}, {Sweden3}, {Sweden4}, " +
            $"{Finland}, {Denmark1}, {Denmark2}, " +
            $"{Estonia}, {Latvia}, {Lithuania}, " +
            $"{Poland}, {GermanyLuxembourg}, {Netherlands}, {GreatBritain}";

        public static bool IsKnown(string value) =>
            value is
                Norway1 or Norway2 or Norway3 or Norway4 or Norway5 or
                Sweden1 or Sweden2 or Sweden3 or Sweden4 or
                Finland or Denmark1 or Denmark2 or
                Estonia or Latvia or Lithuania or
                Poland or GermanyLuxembourg or Netherlands or GreatBritain;

        private const string Norway1 = "NO1";
        private const string Norway2 = "NO2";
        private const string Norway3 = "NO3";
        private const string Norway4 = "NO4";
        private const string Norway5 = "NO5";
        private const string Sweden1 = "SE1";
        private const string Sweden2 = "SE2";
        private const string Sweden3 = "SE3";
        private const string Sweden4 = "SE4";
        private const string Finland = "FI";
        private const string Denmark1 = "DK1";
        private const string Denmark2 = "DK2";
        private const string Estonia = "EE";
        private const string Latvia = "LV";
        private const string Lithuania = "LT";
        private const string Poland = "PL";
        private const string GermanyLuxembourg = "DE-LU";
        private const string Netherlands = "NL";
        private const string GreatBritain = "GB";
    }
}
