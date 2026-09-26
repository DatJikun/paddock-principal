using System.Globalization;
using System.Text;
using System.Text.Json;
using Paddock.Domain.Random;

namespace Paddock.Persistence;

/// <summary>
/// Canonical text for every <see cref="RngStreamName"/> state.
/// Words are decimal strings so the full <see cref="ulong"/> range round-trips.
/// Object key order on write follows <see cref="RngStreamName.All"/>.
/// </summary>
internal static class RngStateCodec
{
    public static void RequireEveryStream(IReadOnlyDictionary<string, RngState> states)
    {
        if (states.Count != RngStreamName.All.Count || RngStreamName.All.Any(name => !states.ContainsKey(name)))
        {
            throw new ArgumentException(
                "RNG states must contain every RngStreamName exactly once.",
                nameof(states));
        }
    }

    public static string Serialize(IReadOnlyDictionary<string, RngState> states)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var name in RngStreamName.All)
            {
                var state = states[name];
                writer.WriteStartObject(name);
                WriteWord(writer, "s0", state.S0);
                WriteWord(writer, "s1", state.S1);
                WriteWord(writer, "s2", state.S2);
                WriteWord(writer, "s3", state.S3);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static IReadOnlyDictionary<string, RngState> Deserialize(string payload)
    {
        try
        {
            return Parse(payload);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("RNG states payload is malformed.", exception);
        }
    }

    private static Dictionary<string, RngState> Parse(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("RNG states payload is not an object.");
        }

        var result = new Dictionary<string, RngState>(RngStreamName.All.Count, StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!result.TryAdd(property.Name, ReadState(property.Name, property.Value)))
            {
                throw new InvalidDataException($"Duplicate RNG stream '{property.Name}'.");
            }
        }

        if (result.Count != RngStreamName.All.Count || RngStreamName.All.Any(name => !result.ContainsKey(name)))
        {
            throw new InvalidDataException("RNG states payload must contain every RngStreamName exactly once.");
        }

        return result;
    }

    private static RngState ReadState(string streamName, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"RNG stream '{streamName}' is not an object.");
        }

        ulong? s0 = null;
        ulong? s1 = null;
        ulong? s2 = null;
        ulong? s3 = null;
        foreach (var property in element.EnumerateObject())
        {
            var word = ReadWord(streamName, property.Name, property.Value);
            switch (property.Name)
            {
                case "s0" when s0 is null:
                    s0 = word;
                    break;
                case "s1" when s1 is null:
                    s1 = word;
                    break;
                case "s2" when s2 is null:
                    s2 = word;
                    break;
                case "s3" when s3 is null:
                    s3 = word;
                    break;
                default:
                    throw new InvalidDataException(
                        $"RNG stream '{streamName}' has an unexpected property '{property.Name}'.");
            }
        }

        if (s0 is null || s1 is null || s2 is null || s3 is null)
        {
            throw new InvalidDataException($"RNG stream '{streamName}' must contain s0, s1, s2, and s3.");
        }

        return new RngState(s0.Value, s1.Value, s2.Value, s3.Value);
    }

    private static ulong ReadWord(string streamName, string property, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(
                $"RNG stream '{streamName}' property '{property}' must be a decimal string.");
        }

        var text = value.GetString();
        if (text is null || !ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var word))
        {
            throw new InvalidDataException(
                $"RNG stream '{streamName}' property '{property}' is not an unsigned 64-bit integer.");
        }

        return word;
    }

    private static void WriteWord(Utf8JsonWriter writer, string name, ulong value)
    {
        writer.WriteString(name, value.ToString(CultureInfo.InvariantCulture));
    }
}
