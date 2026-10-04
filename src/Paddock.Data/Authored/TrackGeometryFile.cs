using System.Text.Json.Serialization;

namespace Paddock.Data.Authored;

/// <summary>
/// One layout's centre line as closed-loop control points in metres (x east, y north), one file per layout in
/// <c>data/authored/tracks/geometry/&lt;layout_id&gt;.json</c>. The same file feeds the simulation
/// (<see cref="TrackGeometryCatalog"/>) and the UI (generated JS data). The track editor reads and writes this format.
/// </summary>
/// <param name="LayoutId">Layout id from <c>circuits.json</c>.</param>
/// <param name="ControlPoints">Closed loop of <c>[x, y]</c> pairs in metres. Point 0 sits on the start line.</param>
/// <param name="Source">Honest provenance, for example "approximate, hand-authored ... ESTIMATE".</param>
/// <param name="Notes">Free text for authors.</param>
/// <param name="Corners">Optional named corners. Absent means the map shows no corner labels.</param>
public sealed record TrackGeometryFile(
    [property: JsonPropertyName("layout_id")] string LayoutId,
    [property: JsonPropertyName("control_points")] IReadOnlyList<double[]> ControlPoints,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("corners")] IReadOnlyList<TrackCornerFile>? Corners = null);

/// <summary>
/// A named corner, attached to a control point by index so it follows the point when the shape is edited.
/// Names are proper nouns (like circuit names) and are not translated.
/// </summary>
/// <param name="Point">Index into <c>control_points</c>.</param>
/// <param name="Name">Corner name as shown on the map.</param>
public sealed record TrackCornerFile(
    [property: JsonPropertyName("point")] int Point,
    [property: JsonPropertyName("name")] string Name);
