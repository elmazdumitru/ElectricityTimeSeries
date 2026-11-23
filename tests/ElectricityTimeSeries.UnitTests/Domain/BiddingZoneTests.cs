using System.Reflection;
using ElectricityTimeSeries.Core.Domain;

namespace ElectricityTimeSeries.UnitTests.Domain;

public class BiddingZoneTests
{
    [Theory]
    [InlineData("NO1")]
    [InlineData("no1")]
    [InlineData("  No1 ")]
    public void From_trims_and_upper_cases(string code)
    {
        Assert.Equal("NO1", BiddingZone.From(code).ToString());
    }

    [Fact]
    public void Zones_created_from_differently_written_codes_are_equal()
    {
        var a = BiddingZone.From("de-lu");
        var b = BiddingZone.From(" DE-LU");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Single(new HashSet<BiddingZone> { a, b });
    }

    [Theory]
    [InlineData("NO1", true)]
    [InlineData("gb", true)]
    [InlineData("XX1", false)]
    [InlineData("", false)]
    public void IsKnown_recognizes_the_supported_zones(string code, bool expected)
    {
        Assert.Equal(expected, BiddingZone.From(code).IsKnown);
    }

    /// <summary>Every zone code constant in BiddingZone.KnownZones, found by reflection, so a new one
    /// can't be added without also being added to IsKnown and the list of known zones.</summary>
    public static TheoryData<string> ZoneConstants()
    {
        var knownZones = typeof(BiddingZone).GetNestedType("KnownZones", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BiddingZone.KnownZones was renamed or moved; update this test.");

        return new(knownZones.GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!));
    }

    [Theory]
    [MemberData(nameof(ZoneConstants))]
    public void Every_zone_constant_is_known(string code)
    {
        Assert.True(BiddingZone.From(code).IsKnown, $"'{code}' is a zone constant but IsKnown doesn't accept it.");
    }

    [Theory]
    [MemberData(nameof(ZoneConstants))]
    public void Every_zone_constant_is_in_the_list_of_known_zones(string code)
    {
        Assert.Contains(code, BiddingZone.ConcatenatedKnownZones.Split(", "));
    }

    [Fact]
    public void Default_zone_is_harmless_unknown_and_empty()
    {
        var zone = default(BiddingZone);

        Assert.False(zone.IsKnown);
        Assert.Equal(string.Empty, zone.ToString());
        Assert.Equal(default, zone);
        Assert.NotEqual(BiddingZone.From(""), zone);
    }

    [Fact]
    public void From_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => BiddingZone.From(null!));
    }
}
