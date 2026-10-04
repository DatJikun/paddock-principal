using System.Text.Json.Serialization;

namespace Paddock.Data.Authored;

public sealed record TrackGeometryFile(
    [property: JsonPropertyName("layout_id")] string LayoutId,
    [property: JsonPropertyName("control_points")] IReadOnlyList<double[]> ControlPoints,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("notes")] string Notes);
