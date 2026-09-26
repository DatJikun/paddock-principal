using System.Text.Json.Serialization;

namespace Paddock.Data.Authored;

public sealed record CircuitsFile(
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("character_tags")] IReadOnlyList<CharacterTag> CharacterTags,
    [property: JsonPropertyName("circuits")] IReadOnlyList<Circuit> Circuits);

public sealed record CharacterTag(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("meaning")] string Meaning);

public sealed record Circuit(
    [property: JsonPropertyName("circuit_id")] string CircuitId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("country")] string Country,
    [property: JsonPropertyName("layouts")] IReadOnlyList<CircuitLayout> Layouts);

public sealed record CircuitLayout(
    [property: JsonPropertyName("layout_id")] string LayoutId,
    [property: JsonPropertyName("years_used_in_f1")] IReadOnlyList<int> YearsUsedInF1,
    [property: JsonPropertyName("length_km")] double LengthKm,
    [property: JsonPropertyName("corners")] int? Corners,
    [property: JsonPropertyName("character")] IReadOnlyList<string> Character,
    [property: JsonPropertyName("profile_guess")] LayoutProfile ProfileGuess,
    [property: JsonPropertyName("confidence")] string Confidence,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("notes")] string Notes);

public sealed record LayoutProfile(
    [property: JsonPropertyName("straights")] double Straights,
    [property: JsonPropertyName("high_speed")] double HighSpeed,
    [property: JsonPropertyName("low_speed")] double LowSpeed,
    [property: JsonPropertyName("braking")] double Braking);
