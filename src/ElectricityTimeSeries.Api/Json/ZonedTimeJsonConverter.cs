using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElectricityTimeSeries.Api.Json;

/// <summary>Reads a time only if it says its time zone ("Z" or "+01:00"), instead of guessing the server's.</summary>
public sealed class ZonedTimeJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType is JsonTokenType.String && HasTimeZone(reader.GetString()!) && reader.TryGetDateTimeOffset(out var time)
            ? time
            : throw new JsonException("Expected a time with a time zone, for example 2025-11-22T10:00:00Z.");

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);

    // The zone comes after the time: "Z", or an offset starting with "+" or "-".
    private static bool HasTimeZone(string text)
    {
        var timeStart = text.IndexOfAny(['T', 't']);
        return timeStart >= 0 && (text.EndsWith('Z') || text.EndsWith('z') || text.AsSpan(timeStart).IndexOfAny('+', '-') >= 0);
    }
}
