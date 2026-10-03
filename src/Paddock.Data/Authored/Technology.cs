using System.Text.Json.Serialization;

namespace Paddock.Data.Authored;

public sealed record Technology(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("area")] string Area,
    [property: JsonPropertyName("first_used")] TechnologyFirstUse FirstUsed,
    [property: JsonPropertyName("widespread_by")] int? WidespreadBy,
    [property: JsonPropertyName("banned")] IReadOnlyList<TechnologyBan> Banned,
    [property: JsonPropertyName("prerequisites")] IReadOnlyList<string> Prerequisites,
    [property: JsonPropertyName("earliest_plausible")] TechnologyEarliest EarliestPlausible,
    [property: JsonPropertyName("effect_summary")] string EffectSummary,
    [property: JsonPropertyName("confidence")] string Confidence,
    [property: JsonPropertyName("notes")] string? Notes = null);

public sealed record TechnologyFirstUse(
    [property: JsonPropertyName("season")] int? Season,
    [property: JsonPropertyName("team")] string? Team,
    [property: JsonPropertyName("car")] string? Car,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("note")] string Note);

public sealed record TechnologyBan(
    [property: JsonPropertyName("from")] int From,
    [property: JsonPropertyName("note")] string Note,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("to")] int? To = null);

public sealed record TechnologyEarliest(
    [property: JsonPropertyName("season")] int? Season,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("source")] string Source);
