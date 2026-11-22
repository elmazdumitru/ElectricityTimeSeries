using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace ElectricityTimeSeries.Api.Json;

/// <summary>
/// Reads and writes durations as ISO 8601 text ("PT15M", "PT1H"), the format used for
/// resolutions in electricity market data.
/// </summary>
public sealed class IsoDurationJsonConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();

        try
        {
            return XmlConvert.ToTimeSpan(text ?? string.Empty);
        }
        catch (FormatException)
        {
            throw new JsonException("Expected an ISO 8601 duration, for example PT15M or PT1H.");
        }
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) =>
        writer.WriteStringValue(XmlConvert.ToString(value));
}
