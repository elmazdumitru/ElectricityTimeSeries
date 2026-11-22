using System.Text.Json;
using System.Text.Json.Serialization;
using ElectricityTimeSeries.Core.Domain;

namespace ElectricityTimeSeries.Api.Json;

/// <summary>
/// Reads and writes a <see cref="BiddingZone"/> as its plain text code, for example "NO1".
/// Any text is accepted, known or not, so an unknown zone is reported by the business rules
/// together with every other error, instead of stopping the JSON reading.
/// </summary>
/// <remarks>JSON <c>null</c> never reaches this converter; the property is simply left <c>null</c>.</remarks>
public sealed class BiddingZoneJsonConverter : JsonConverter<BiddingZone>
{
    public override BiddingZone Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException("Expected a bidding zone code as text, for example \"NO1\".");
        }

        return BiddingZone.From(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, BiddingZone value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
