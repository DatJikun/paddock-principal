using System.Text.Json.Serialization;

namespace Paddock.Data.Authored;

public sealed record EnginesFile(
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("supply_types")] IReadOnlyList<EngineSupplyType> SupplyTypes,
    [property: JsonPropertyName("entries")] IReadOnlyList<EngineEntry> Entries);

public sealed record EngineSupplyType(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("meaning")] string Meaning);

public sealed record EngineEntry(
    [property: JsonPropertyName("constructorId")] string ConstructorId,
    [property: JsonPropertyName("year")] int Year,
    [property: JsonPropertyName("supplier")] string Supplier,
    [property: JsonPropertyName("engine_name")] string EngineName,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("confidence")] string Confidence,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("notes")] string? Notes = null,
    [property: JsonPropertyName("badged_as")] string? BadgedAs = null);
