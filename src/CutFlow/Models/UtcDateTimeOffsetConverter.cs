using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CutFlow.Models;

internal sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("A project timestamp must be an ISO 8601 string.");
        }

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("A project timestamp cannot be empty.");
        }

        if (!HasExplicitOffset(value))
        {
            value += "Z";
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
        {
            throw new JsonException("A project timestamp must be a valid ISO 8601 value.");
        }

        return timestamp.ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToUniversalTime());

    private static bool HasExplicitOffset(string value)
    {
        if (value.EndsWith('Z') || value.EndsWith('z'))
        {
            return true;
        }

        var timeSeparator = value.IndexOf('T');
        return timeSeparator >= 0 &&
            (value.LastIndexOf('+') > timeSeparator || value.LastIndexOf('-') > timeSeparator);
    }
}
