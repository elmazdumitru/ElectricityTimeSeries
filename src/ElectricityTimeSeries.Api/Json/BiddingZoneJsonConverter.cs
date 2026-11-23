using System.Text.Json;
using System.Text.Json.Serialization;
using ElectricityTimeSeries.Core.Domain;

namespace ElectricityTimeSeries.Api.Json;

/// <summary>Accepts any text, known zone or not, so an unknown zone is reported with all other errors
/// instead of stopping the JSON reading. JSON null never reaches this converter.</summary>
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
        writer.WriteStringValue(value.ToString());
}
