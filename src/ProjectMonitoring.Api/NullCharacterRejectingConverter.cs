using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectMonitoring.Api;

/// <summary>
/// PostgreSQL cannot store U+0000 in text, so requests containing it would otherwise fail
/// as a server error. Rejecting them while reading JSON covers every request body.
/// </summary>
public sealed class NullCharacterRejectingConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value?.Contains('\0') == true)
        {
            throw new JsonException("Text must not contain null characters.");
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
