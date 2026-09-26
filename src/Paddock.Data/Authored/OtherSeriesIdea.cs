using System.Text.Json.Serialization;

namespace Paddock.Data.Authored;

public sealed record OtherSeriesIdea(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("where")] string Where,
    [property: JsonPropertyName("years")] string Years,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("gameplay_value")] string GameplayValue);
