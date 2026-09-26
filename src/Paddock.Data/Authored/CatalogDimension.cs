using System.Text.Json.Serialization;

namespace Paddock.Data.Authored;

public sealed record CatalogDimension(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("values")] IReadOnlyList<string>? Values,
    [property: JsonPropertyName("unit")] string? Unit,
    [property: JsonPropertyName("gameplay_effect")] string GameplayEffect);
