using System.Text.Json.Serialization;

namespace Paddock.Data.Authored;

public sealed record LineageFile(
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("lineages")] IReadOnlyList<ConstructorLineage> Lineages);

public sealed record ConstructorLineage(
    [property: JsonPropertyName("lineage_id")] string LineageId,
    [property: JsonPropertyName("entries")] IReadOnlyList<LineageEntry> Entries);

public sealed record LineageEntry(
    [property: JsonPropertyName("constructorId")] string ConstructorId,
    [property: JsonPropertyName("from")] int From,
    [property: JsonPropertyName("to")] int? To,
    [property: JsonPropertyName("how_it_ended")] string? HowItEnded,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("notes")] string? Notes = null);
