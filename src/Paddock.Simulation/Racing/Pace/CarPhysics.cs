using System.Globalization;

namespace Paddock.Simulation.Racing.Pace;

/// <summary>
/// Physical car parameters for the speed trace (PP-049). Lateral grip is
/// <c>a0 + c·v²</c>: <see cref="MechanicalGripMs2"/> is <c>a0</c>, <see cref="DownforcePerM"/> is <c>c</c>.
/// </summary>
/// <param name="MechanicalGripMs2">Lateral acceleration without aero, m/s².</param>
/// <param name="DownforcePerM">Aero coefficient <c>c</c> in 1/m. Extra lateral acceleration is <c>c·v²</c>.</param>
/// <param name="TopSpeedMs">Speed the car cannot exceed, m/s.</param>
/// <param name="AccelerationMs2">Longitudinal acceleration at low speed, m/s².</param>
/// <param name="BrakingMs2">Constant braking deceleration, m/s².</param>
public readonly record struct CarPhysics(
    double MechanicalGripMs2,
    double DownforcePerM,
    double TopSpeedMs,
    double AccelerationMs2,
    double BrakingMs2)
{
    /// <summary>
    /// Era-average physics for <paramref name="season"/>, scaled by the team's 0..100 ratings.
    /// A rating of <see cref="PaceConstants.RatingReference"/> is the era average (scale 1).
    /// Power scales acceleration linearly and top speed by the cube root of that factor.
    /// Downforce is limited by <paramref name="limits"/>: a rating above the cap counts as the cap, so
    /// <see cref="DownforcePerM"/> never exceeds the coefficient the cap itself would produce.
    /// </summary>
    public static CarPhysics From(CarPerformance perf, int season, EraPerformanceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        Rating(perf.Power, nameof(perf.Power));
        Rating(perf.Downforce, nameof(perf.Downforce));
        Rating(perf.MechanicalGrip, nameof(perf.MechanicalGrip));
        Rating(perf.Braking, nameof(perf.Braking));
        if (!double.IsFinite(limits.DownforceCap) || limits.DownforceCap < 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limits),
                limits.DownforceCap,
                "Expected a finite downforce cap >= 0.");
        }

        var era = EraCarPhysics.For(season);
        var power = Scale(perf.Power, SpeedTraceConstants.PowerSensitivity);
        var grip = Scale(perf.MechanicalGrip, SpeedTraceConstants.MechanicalGripSensitivity);
        var braking = Scale(perf.Braking, SpeedTraceConstants.BrakingSensitivity);
        var downforceRating = Math.Min(perf.Downforce, limits.DownforceCap);
        var downforce = Scale(downforceRating, SpeedTraceConstants.DownforceSensitivity);

        return new CarPhysics(
            era.MechanicalGripMs2 * grip,
            Math.Max(0d, era.DownforcePerM * downforce),
            era.TopSpeedMs * Math.Cbrt(power),
            era.AccelerationMs2 * power,
            era.BrakingMs2 * braking);
    }

    /// <summary>1 at rating 50. Sensitivity is the fractional change over a 50-point gap.</summary>
    private static double Scale(double rating, double sensitivity) =>
        1d + (sensitivity * ((rating - PaceConstants.RatingReference) / PaceConstants.RatingReference));

    private static void Rating(double value, string name)
    {
        if (!double.IsFinite(value) || value < PaceConstants.RatingMin || value > PaceConstants.RatingMax)
        {
            throw new ArgumentOutOfRangeException(
                name,
                value,
                $"Expected a rating in 0..100, got {value.ToString("R", CultureInfo.InvariantCulture)}.");
        }
    }
}
