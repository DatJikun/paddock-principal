using System.Text.Json.Serialization;

namespace Paddock.Data.Authored;

public sealed record FoundersFile(
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("organizations")] IReadOnlyList<FounderOrganization> Organizations);

public sealed record FounderOrganization(
    [property: JsonPropertyName("organization_id")] string OrganizationId,
    [property: JsonPropertyName("season_count")] int SeasonCount,
    [property: JsonPropertyName("from")] int From,
    [property: JsonPropertyName("to")] int To,
    [property: JsonPropertyName("constructor_entries")] IReadOnlyList<FounderConstructorEntry> ConstructorEntries,
    [property: JsonPropertyName("founded")] int? Founded,
    [property: JsonPropertyName("founders")] IReadOnlyList<string> Founders,
    [property: JsonPropertyName("country")] string? Country,
    [property: JsonPropertyName("base_city")] string? BaseCity,
    [property: JsonPropertyName("confidence")] string Confidence,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("notes")] string Notes);

public sealed record FounderConstructorEntry(
    [property: JsonPropertyName("constructorId")] string ConstructorId,
    [property: JsonPropertyName("from")] int From,
    [property: JsonPropertyName("to")] int To);
