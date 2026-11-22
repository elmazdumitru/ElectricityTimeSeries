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
        Assert.Equal("NO1", BiddingZone.From(code).Value);
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

    [Fact]
    public void From_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => BiddingZone.From(null!));
    }
}
