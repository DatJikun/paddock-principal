namespace Paddock.Simulation.Racing.Pace;

/// <summary>
/// Average-car physics by season. Anchors are ESTIMATES per decade in <see cref="SpeedTraceConstants"/>;
/// between anchors the value is a smoothstep, and outside 1950–2020 it is held flat.
/// </summary>
public static class EraCarPhysics
{
    /// <summary>The era-average car for <paramref name="season"/> (rating 50 on every axis).</summary>
    public static CarPhysics For(int season) => new(
        EraCurve.Evaluate(SpeedTraceConstants.MechanicalGripMs2, season),
        EraCurve.Evaluate(SpeedTraceConstants.DownforcePerM, season),
        EraCurve.Evaluate(SpeedTraceConstants.TopSpeedMs, season),
        EraCurve.Evaluate(SpeedTraceConstants.AccelerationMs2, season),
        EraCurve.Evaluate(SpeedTraceConstants.BrakingMs2, season));
}
