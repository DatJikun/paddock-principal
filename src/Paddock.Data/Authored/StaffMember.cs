using System.Text.Json.Serialization;

namespace Paddock.Data.Authored;

public sealed record StaffMember(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("born")] string? Born,
    [property: JsonPropertyName("nationality")] string? Nationality,
    [property: JsonPropertyName("career")] IReadOnlyList<StaffCareerStint> Career,
    [property: JsonPropertyName("notable")] IReadOnlyList<string> Notable,
    [property: JsonPropertyName("confidence")] string Confidence);

public sealed record StaffCareerStint(
    [property: JsonPropertyName("org")] string Org,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("from")] int From,
    [property: JsonPropertyName("to")] int To,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("series")] string? Series = null);
