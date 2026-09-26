using System.Text.Json.Serialization;

namespace Paddock.Data.Authored;

public sealed record RaceLayoutEntry(
    [property: JsonPropertyName("season")] int Season,
    [property: JsonPropertyName("round")] int Round,
    [property: JsonPropertyName("layout_id")] string LayoutId);
