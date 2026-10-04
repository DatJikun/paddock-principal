using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Paddock.Simulation.Codec;

/// <summary>
/// The text form of a codec body: one flat JSON object of strings and integers.
/// Keys are written in the order given, so the same value always gives the same text. Reading is strict:
/// the keys must be exactly the expected ones, each once, and every value must have the expected kind.
/// Integers are written as decimal strings so the full <see cref="ulong"/> range survives any JSON reader.
/// </summary>
public static class FlatJson
{
    public static string Write(params (string Key, object Value)[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in fields)
            {
                switch (value)
                {
                    case string text:
                        writer.WriteString(key, text);
                        break;
                    case int number:
                        writer.WriteString(key, number.ToString(CultureInfo.InvariantCulture));
                        break;
                    case long number:
                        writer.WriteString(key, number.ToString(CultureInfo.InvariantCulture));
                        break;
                    case ulong number:
                        writer.WriteString(key, number.ToString(CultureInfo.InvariantCulture));
                        break;
                    default:
                        throw new ArgumentException("Unsupported field type " + value.GetType().Name + " for '" + key + "'.", nameof(fields));
                }
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Parses <paramref name="text"/> and requires exactly <paramref name="keys"/>.</summary>
    public static FlatObject Read(string text, params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(keys);
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("A codec body must be a JSON object.");
            }

            var values = new Dictionary<string, string>(keys.Length, StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (Array.IndexOf(keys, property.Name) < 0)
                {
                    throw new InvalidDataException("A codec body has the unexpected field '" + property.Name + "'.");
                }

                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException("Field '" + property.Name + "' of a codec body must be a string.");
                }

                if (!values.TryAdd(property.Name, property.Value.GetString()!))
                {
                    throw new InvalidDataException("A codec body repeats the field '" + property.Name + "'.");
                }
            }

            foreach (var key in keys)
            {
                if (!values.ContainsKey(key))
                {
                    throw new InvalidDataException("A codec body is missing the field '" + key + "'.");
                }
            }

            return new FlatObject(values);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("A codec body is not valid JSON.", exception);
        }
    }
}

/// <summary>The fields of one parsed <see cref="FlatJson"/> body.</summary>
public sealed class FlatObject
{
    private readonly Dictionary<string, string> _values;

    internal FlatObject(Dictionary<string, string> values) => _values = values;

    public string String(string key) => _values[key];

    public int Int32(string key) =>
        int.TryParse(_values[key], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException("Field '" + key + "' is not a 32-bit integer.");

    public long Int64(string key) =>
        long.TryParse(_values[key], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException("Field '" + key + "' is not a 64-bit integer.");
}
