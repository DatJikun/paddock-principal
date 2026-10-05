using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Paddock.Application.Access;
using Paddock.Application.Commands;
using Paddock.Domain.Time;

namespace Paddock.Desktop.Bridge;

/// <summary>
/// Turns a view into JSON the page can read. Dates are ISO strings, enums are names, money stays a whole number,
/// and <see cref="AccessContext"/> is left out so a view cannot carry who-sees-truth into the page.
/// </summary>
public static class BridgeValues
{
    private static readonly JsonNamingPolicy Names = JsonNamingPolicy.CamelCase;

    public static JsonNode? ToNode(object? value) => ToNode(value, new HashSet<object>(ReferenceEqualityComparer.Instance));

    public static string Response(string id, JsonNode? data) =>
        new JsonObject
        {
            ["id"] = id,
            ["ok"] = true,
            ["data"] = data,
        }.ToJsonString();

    public static string Failure(string id, string key, IReadOnlyDictionary<string, string>? parameters = null)
    {
        var error = new JsonObject { ["key"] = key };
        if (parameters is { Count: > 0 })
        {
            var args = new JsonObject();
            foreach (var pair in parameters)
            {
                args[pair.Key] = pair.Value;
            }

            error["parameters"] = args;
        }

        return new JsonObject
        {
            ["id"] = id,
            ["ok"] = false,
            ["error"] = error,
        }.ToJsonString();
    }

    public static string Event(string type, JsonNode? data) =>
        new JsonObject
        {
            ["type"] = type,
            ["data"] = data,
        }.ToJsonString();

    private static JsonNode? ToNode(object? value, HashSet<object> seen)
    {
        if (value is null)
        {
            return null;
        }

        var type = value.GetType();
        if (type == typeof(string))
        {
            return JsonValue.Create((string)value);
        }

        if (type == typeof(bool))
        {
            return JsonValue.Create((bool)value);
        }

        if (type == typeof(DateOnly))
        {
            return JsonValue.Create(((DateOnly)value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (type == typeof(GameDate))
        {
            return JsonValue.Create(value.ToString());
        }

        if (type.IsEnum)
        {
            return JsonValue.Create(value.ToString());
        }

        if (value is TranslationMessage message)
        {
            return new JsonObject
            {
                ["key"] = message.Key,
                ["parameters"] = (JsonNode?)ToNode(message.Parameters, seen),
            };
        }

        if (IsWholeNumber(type))
        {
            return JsonValue.Create(Convert.ToInt64(value, CultureInfo.InvariantCulture));
        }

        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
        {
            return JsonValue.Create(Convert.ToDouble(value, CultureInfo.InvariantCulture));
        }

        if (IsId(type))
        {
            return IdNode(value);
        }

        if (value is IDictionary dictionary)
        {
            var obj = new JsonObject();
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is not string key)
                {
                    throw new InvalidOperationException("Bridge maps only string-keyed dictionaries.");
                }

                obj[key] = ToNode(entry.Value, seen);
            }

            return obj;
        }

        if (value is IEnumerable sequence and not string)
        {
            var array = new JsonArray();
            foreach (var item in sequence)
            {
                array.Add(ToNode(item, seen));
            }

            return array;
        }

        if (!type.IsValueType && !seen.Add(value))
        {
            throw new InvalidOperationException("Bridge value graph has a cycle at " + type.Name + ".");
        }

        var objNode = new JsonObject();
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetMethod is null || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
            {
                continue;
            }

            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (propertyType == typeof(AccessContext))
            {
                continue;
            }

            objNode[Names.ConvertName(property.Name)] = ToNode(property.GetValue(value), seen);
        }

        return objNode;
    }

    private static bool IsWholeNumber(Type type) =>
        type == typeof(byte) || type == typeof(short) || type == typeof(int) || type == typeof(long)
        || type == typeof(sbyte) || type == typeof(ushort) || type == typeof(uint);

    private static bool IsId(Type type)
    {
        if (type == typeof(string))
        {
            return false;
        }

        var value = type.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
        var assigned = type.GetProperty("IsAssigned", BindingFlags.Instance | BindingFlags.Public);
        return type.Name.EndsWith("Id", StringComparison.Ordinal)
            && value?.PropertyType == typeof(string)
            && assigned?.PropertyType == typeof(bool);
    }

    private static JsonNode? IdNode(object value)
    {
        var assigned = (bool)value.GetType().GetProperty("IsAssigned")!.GetValue(value)!;
        if (!assigned)
        {
            return null;
        }

        return JsonValue.Create((string)value.GetType().GetProperty("Value")!.GetValue(value)!);
    }
}
