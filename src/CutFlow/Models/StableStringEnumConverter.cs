using System.Text.Json;
using System.Text.Json.Serialization;

namespace CutFlow.Models;

internal sealed class StableStringEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private static readonly IReadOnlyDictionary<string, TEnum> ValuesByName =
        Enum.GetNames<TEnum>().ToDictionary(
            static name => name,
            static name => Enum.Parse<TEnum>(name),
            StringComparer.Ordinal);

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String ||
            reader.GetString() is not { } name ||
            !ValuesByName.TryGetValue(name, out var value))
        {
            throw new JsonException($"The value is not a valid {typeof(TEnum).Name} string.");
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        var name = Enum.GetName(value);
        if (name is null)
        {
            throw new JsonException($"The value is not a defined {typeof(TEnum).Name} value.");
        }

        writer.WriteStringValue(name);
    }
}
