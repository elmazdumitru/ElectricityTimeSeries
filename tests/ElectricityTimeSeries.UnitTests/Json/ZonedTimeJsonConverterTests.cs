using System.Text.Json;
using ElectricityTimeSeries.Api.Json;

namespace ElectricityTimeSeries.UnitTests.Json;

public class ZonedTimeJsonConverterTests
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new ZonedTimeJsonConverter() } };

    [Theory]
    [InlineData("\"2025-11-22T10:00:00Z\"", 10)]
    [InlineData("\"2025-11-22T11:00:00+01:00\"", 10)]
    [InlineData("\"2025-11-22T05:00:00-05:00\"", 10)]
    public void Reads_a_time_with_a_time_zone(string json, int utcHour)
    {
        var time = JsonSerializer.Deserialize<DateTimeOffset>(json, Json);

        Assert.Equal(new DateTimeOffset(2025, 11, 22, utcHour, 0, 0, TimeSpan.Zero), time);
    }

    [Theory]
    [InlineData("\"2025-11-22T10:00:00\"")]
    [InlineData("\"2025-11-22\"")]
    [InlineData("\"2025-11-22T10:00:00+\"")]
    [InlineData("\"not a time\"")]
    [InlineData("1732269600")]
    public void Rejects_a_time_without_a_time_zone(string json)
    {
        var error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DateTimeOffset>(json, Json));

        Assert.Equal("Expected a time with a time zone, for example 2025-11-22T10:00:00Z.", error.Message);
    }

    [Fact]
    public void Null_is_read_as_no_time()
    {
        Assert.Null(JsonSerializer.Deserialize<DateTimeOffset?>("null", Json));
    }
}
