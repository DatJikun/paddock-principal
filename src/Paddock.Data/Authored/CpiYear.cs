using System.Text.Json.Serialization;

namespace Paddock.Data.Authored;

public sealed record CpiYear(
    [property: JsonPropertyName("year")] int Year,
    [property: JsonPropertyName("cpi")] decimal Cpi,
    [property: JsonPropertyName("partial")] bool Partial,
    [property: JsonPropertyName("source")] string Source);
