namespace Paddock.Simulation.Racing.Physics;

/// <summary>
/// Tunables for the v2 racing line and single-car speed profile (PP-074, TECH §6.5a). EVERY value is an ESTIMATE
/// unless it says otherwise: nothing has been calibrated against timing data yet (the 2010 calibration is #341's
/// later steps). The 2010 era constants live here and only here. Changing a value changes lap times.
/// </summary>
public static class LinePhysicsConstants
{
    // ---- Physics ------------------------------------------------------------------------------------------

    /// <summary>Standard gravity, m/s². A physical constant, not an estimate.</summary>
    public const double GravityMs2 = 9.80665d;

    // ---- 2010 era car -------------------------------------------------------------------------------------

    /// <summary>
    /// The season whose average car <see cref="CarMassKg2010"/> and <see cref="EnginePowerW2010"/> describe. A
    /// <see cref="Pace.CarPhysics"/> is read relative to <see cref="Pace.EraCarPhysics.For"/> of this season.
    /// </summary>
    public const int ReferenceSeason = 2010;

    /// <summary>
    /// 2010 minimum mass of car plus driver without fuel, kg. The rule value (620 kg); the average car is taken to
    /// run at the minimum, which is the ESTIMATE.
    /// </summary>
    public const double CarMassKg2010 = 620d;

    /// <summary>ESTIMATE: power of the average 2010 V8 at the wheels, W (about 750 hp at the crank, less losses).</summary>
    public const double EnginePowerW2010 = 540_000d;

    /// <summary>ESTIMATE: fuel load at the start of a 2010 race (no refuelling that year), kg.</summary>
    public const double RaceStartFuelKg2010 = 160d;

    /// <summary>2010 maximum car width, m (rule value: 1800 mm).</summary>
    public const double CarWidthM2010 = 1.8d;

    // ---- Track --------------------------------------------------------------------------------------------

    /// <summary>
    /// ESTIMATE: one constant track width, m, used until the track file carries <c>width_m</c> (#348). Real
    /// circuits run from about 8 m (old street tracks) to 15+ m; 12 m is a middle value. Monaco in particular is
    /// narrower than this, so its line is too generous.
    /// </summary>
    public const double DefaultTrackWidthM = 12d;

    /// <summary>
    /// ESTIMATE: gap the line keeps from the track edge on top of half the car width, m. The car centre may move at
    /// most <c>width / 2 − car width / 2 − margin</c> from the centreline.
    /// </summary>
    public const double EdgeMarginM = 0.3d;

    // ---- Racing-line solver (numerics, not physics) -------------------------------------------------------

    /// <summary>
    /// Target spacing of the coarse knots the minimum-curvature line is solved on, m. Finer is more exact and slower
    /// (the solve is dense, cubic in the knot count).
    /// </summary>
    public const double LineKnotSpacingM = 10d;

    /// <summary>Upper bound on coarse knots per lap: long circuits get a wider spacing instead of a slower solve.</summary>
    public const int MaxLineKnots = 480;

    /// <summary>
    /// Small pull of the line towards the centreline, relative to the curvature term. Only breaks ties where
    /// curvature does not care (long straights), so the solve is well posed.
    /// </summary>
    public const double LineCentringWeight = 1e-6d;

    /// <summary>Most active-set rounds of the line solve; it normally settles in far fewer.</summary>
    public const int MaxLineSolveRounds = 200;

    /// <summary>
    /// Half-width, in samples, of the three-point curvature on the racing line (same window as
    /// <c>TrackGeometry</c>'s centreline curvature).
    /// </summary>
    public const int CurvatureHalfWindow = 6;

    // ---- Speed profile (numerics) -------------------------------------------------------------------------

    /// <summary>Most forward/backward sweeps around the closed lap; a lap normally closes in two or three.</summary>
    public const int MaxSpeedSweeps = 16;
}
