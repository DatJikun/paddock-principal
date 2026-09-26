using System.Text.Json.Serialization;

namespace Paddock.Data.Authored;

public sealed record TimelinePeriod(
    [property: JsonPropertyName("dimension")] string Dimension,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("from")] int From,
    [property: JsonPropertyName("to")] int? To,
    [property: JsonPropertyName("confidence")] string Confidence,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("notes")] string Notes);
