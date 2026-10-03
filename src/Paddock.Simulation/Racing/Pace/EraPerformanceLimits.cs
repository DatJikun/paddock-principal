namespace Paddock.Simulation.Racing.Pace;

/// <summary>What the regulations and technology of a season allow. Supplied to the lap-time model by the caller.</summary>
/// <param name="DownforceCap">
/// Highest downforce rating (0..100) that turns into lap time in this era. Rating above the cap gives no gain.
/// </param>
public sealed record EraPerformanceLimits(double DownforceCap)
{
    /// <summary>
    /// A smooth ESTIMATE of the cap for <paramref name="season"/> (see <see cref="PaceConstants.DownforceCap"/>),
    /// for callers that do not have era data yet. Not calibrated.
    /// </summary>
    public static EraPerformanceLimits EstimateFor(int season) =>
        new(EraCurve.Evaluate(PaceConstants.DownforceCap, season));
}
