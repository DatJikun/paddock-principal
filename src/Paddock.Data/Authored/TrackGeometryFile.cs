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
/// <param name="WidthM">Optional track width along the lap (v2, PP-073). Absent means the era width.</param>
/// <param name="Edges">Optional kerb, run-off and barrier spans (v2). Outside them, the era edge.</param>
/// <param name="PitLane">Optional physical pit lane (v2).</param>
/// <param name="ElevationM">Optional elevation along the lap (v2). The simulation does not read it yet.</param>
public sealed record TrackGeometryFile(
    [property: JsonPropertyName("layout_id")] string LayoutId,
    [property: JsonPropertyName("control_points")] IReadOnlyList<double[]> ControlPoints,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("corners")] IReadOnlyList<TrackCornerFile>? Corners = null,
    [property: JsonPropertyName("width_m")] IReadOnlyList<TrackWidthPointFile>? WidthM = null,
    [property: JsonPropertyName("edges")] IReadOnlyList<TrackEdgeFile>? Edges = null,
    [property: JsonPropertyName("pit_lane")] TrackPitLaneFile? PitLane = null,
    [property: JsonPropertyName("elevation_m")] IReadOnlyList<TrackElevationPointFile>? ElevationM = null)
{
    /// <summary>File name the loader read this from (not part of the JSON); validator messages name it.</summary>
    [JsonIgnore]
    public string? FileName { get; init; }
}

/// <summary>
/// A named corner, attached to a control point by index so it follows the point when the shape is edited.
/// Names are proper nouns (like circuit names) and are not translated.
/// </summary>
/// <param name="Point">Index into <c>control_points</c>.</param>
/// <param name="Name">Corner name as shown on the map.</param>
public sealed record TrackCornerFile(
    [property: JsonPropertyName("point")] int Point,
    [property: JsonPropertyName("name")] string Name);

/// <summary>Track width at a point of the lap; linear interpolation between points, wrapping across the start line.</summary>
/// <param name="S">Lap fraction in [0, 1).</param>
/// <param name="W">Width in metres.</param>
public sealed record TrackWidthPointFile(
    [property: JsonPropertyName("s")] double S,
    [property: JsonPropertyName("w")] double W);

/// <summary>Elevation at a point of the lap.</summary>
/// <param name="S">Lap fraction in [0, 1).</param>
/// <param name="H">Height in metres (any datum; only differences matter).</param>
public sealed record TrackElevationPointFile(
    [property: JsonPropertyName("s")] double S,
    [property: JsonPropertyName("h")] double H);

/// <summary>
/// Kerb, run-off and barrier on one side over a lap range [from, to); <c>from &gt; to</c> wraps across the start line.
/// Side and run-off are strings so the validator can say what is wrong instead of failing the whole load.
/// </summary>
/// <param name="From">Lap fraction where the span starts, in [0, 1).</param>
/// <param name="To">Lap fraction where the span ends (exclusive), in [0, 1).</param>
/// <param name="Side"><c>left</c> or <c>right</c>, looking in the direction of travel.</param>
/// <param name="KerbM">Kerb width in metres.</param>
/// <param name="Runoff"><c>asphalt</c>, <c>grass</c> or <c>gravel</c>.</param>
/// <param name="RunoffM">Run-off width in metres.</param>
/// <param name="Barrier">True when a barrier closes the run-off.</param>
public sealed record TrackEdgeFile(
    [property: JsonPropertyName("from")] double From,
    [property: JsonPropertyName("to")] double To,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("kerb_m")] double KerbM,
    [property: JsonPropertyName("runoff")] string Runoff,
    [property: JsonPropertyName("runoff_m")] double RunoffM,
    [property: JsonPropertyName("barrier")] bool Barrier);

/// <summary>The physical pit lane as an open polyline in the same metre frame as <c>control_points</c>.</summary>
/// <param name="Points">Polyline from the entry end to the exit end, <c>[x, y]</c> in metres.</param>
/// <param name="EntryS">Lap fraction where the lane leaves the track; the first point lies next to it.</param>
/// <param name="ExitS">Lap fraction where the lane rejoins the track; the last point lies next to it.</param>
/// <param name="LimitFrom">Start of the speed limit, metres along the lane from its first point.</param>
/// <param name="LimitTo">End of the speed limit, metres along the lane from its first point.</param>
/// <param name="LimitKph">Speed limit in km/h.</param>
/// <param name="Boxes">Garage positions, metres along the lane, increasing; slot 0 is nearest the entry.</param>
public sealed record TrackPitLaneFile(
    [property: JsonPropertyName("points")] IReadOnlyList<double[]> Points,
    [property: JsonPropertyName("entry_s")] double EntryS,
    [property: JsonPropertyName("exit_s")] double ExitS,
    [property: JsonPropertyName("limit_from")] double LimitFrom,
    [property: JsonPropertyName("limit_to")] double LimitTo,
    [property: JsonPropertyName("limit_kph")] double LimitKph,
    [property: JsonPropertyName("boxes")] IReadOnlyList<double> Boxes);
