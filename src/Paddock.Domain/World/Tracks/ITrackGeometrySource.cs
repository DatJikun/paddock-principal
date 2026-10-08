namespace Paddock.Domain.World.Tracks;

/// <summary>The centre line of a layout, for a reader that should not load files (the race frames, #286).</summary>
public interface ITrackGeometrySource
{
    /// <summary>The layout's geometry at its real length, or null when the layout is unknown.</summary>
    TrackGeometry? GeometryOf(string layoutId);
}
