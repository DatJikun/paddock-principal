namespace Paddock.Domain.Racing;

/// <summary>
/// How a <see cref="CarFrame"/> was produced. <see cref="Approximate"/> is interpolated from lap times
/// (the lap engine). A later continuous engine emits <see cref="Exact"/>.
/// </summary>
public enum FrameAccuracy
{
    Approximate,
    Exact,
}

/// <summary>
/// One display sample of a car. It is not a result: classification and points ignore frames.
/// <see cref="DistanceM"/> is cumulative metres along the racing line from the start line
/// (negative when a car is still staged behind the line). While <see cref="InPitLane"/> is set the
/// racing-line distance is held and <see cref="PitDistanceM"/> runs along the pit lane.
/// </summary>
/// <param name="RaceTimeMs">Milliseconds since the start.</param>
/// <param name="CarId">Stable id. The lap engine's tape uses the driver id in this field.</param>
/// <param name="DistanceM">Cumulative racing-line metres.</param>
/// <param name="SpeedMps">Speed along the path the car is on, metres per second.</param>
/// <param name="InPitLane">The sample is on the pit lane, not the racing line.</param>
/// <param name="PitDistanceM">Metres along the pit lane; 0 on the racing line.</param>
public sealed record CarFrame(
    long RaceTimeMs,
    string CarId,
    double DistanceM,
    double SpeedMps,
    bool InPitLane,
    double PitDistanceM);
