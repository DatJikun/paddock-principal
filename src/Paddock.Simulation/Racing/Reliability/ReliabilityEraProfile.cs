using System.Collections.Concurrent;

namespace Paddock.Simulation.Racing.Reliability;

/// <summary>
/// How reliable cars are in a given season, and the hazard model built on it.
/// Pure: the results depend only on the arguments and on <see cref="ReliabilityConstants"/> (all ESTIMATE).
/// </summary>
public static class ReliabilityEraProfile
{
    // Memo of a pure function; it cannot change results, only skip recomputing them.
    private static readonly ConcurrentDictionary<int, double> ScaleBySeason = new();

    /// <summary>
    /// Expected share of starters who retire mechanically in a race of <see cref="TypicalRaceLaps"/> laps.
    /// Smooth in the season. ESTIMATE.
    /// </summary>
    public static double MechanicalRetirementRate(int season) =>
        Interpolate(ReliabilityConstants.MechanicalRetirementAnchors, season);

    /// <summary>Expected share of starters who do not retire mechanically: one minus <see cref="MechanicalRetirementRate"/>.</summary>
    public static double ExpectedFinishRate(int season) => 1 - MechanicalRetirementRate(season);

    /// <summary>Typical race distance in laps for the season. ESTIMATE.</summary>
    public static double TypicalRaceLaps(int season) =>
        Interpolate(ReliabilityConstants.TypicalRaceLapsAnchors, season);

    /// <summary>
    /// Solves the base hazard scale (per lap, summed over all components) so that a reference car
    /// (the Reference* values of <see cref="ReliabilityConstants"/>) retires mechanically with probability
    /// <paramref name="targetRetirementRate"/> over <paramref name="laps"/> laps.
    /// The retirement probability is <c>1 - exp(-H)</c>, where H is the sum over laps of the retiring hazard.
    /// H is linear in the scale, so the solution is exact and not iterative.
    /// </summary>
    public static double SolveBaseHazardScale(double targetRetirementRate, int laps)
    {
        if (!(targetRetirementRate > 0 && targetRetirementRate < 1))
        {
            throw new ArgumentOutOfRangeException(nameof(targetRetirementRate), "The target must be strictly between 0 and 1.");
        }

        if (laps < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(laps), "A race has at least one lap.");
        }

        return -Math.Log(1 - targetRetirementRate) / ReferenceRetiringHazardPerUnitScale(laps);
    }

    /// <summary>
    /// The season's base hazard scale: <see cref="SolveBaseHazardScale"/> for the era's retirement rate at the typical race distance.
    /// </summary>
    public static double BaseHazardScale(int season) =>
        ScaleBySeason.GetOrAdd(
            season,
            s => SolveBaseHazardScale(MechanicalRetirementRate(s), (int)Math.Round(TypicalRaceLaps(s))));

    /// <summary>Base hazard per lap of one component in the season, before stress, rating and age. ESTIMATE.</summary>
    public static double BaseHazard(int season, MechanicalComponent component) =>
        BaseHazardScale(season) * ReliabilityConstants.ComponentShare(component);

    /// <summary>
    /// Hazard per lap: <c>baseHazard(era, component) * (1 + stress) * (1 - rating * k) * ageFactor</c>.
    /// </summary>
    /// <param name="stress">Stress in [0, 1], see <see cref="FailureInputs.Stress"/>.</param>
    /// <param name="reliabilityRating">The part's reliability rating in [0, 1].</param>
    /// <param name="mileageLaps">The part's accumulated distance in laps at the start of the lap.</param>
    public static double LapHazard(int season, MechanicalComponent component, double stress, double reliabilityRating, double mileageLaps) =>
        BaseHazard(season, component)
        * (1 + stress)
        * (1 - reliabilityRating * ReliabilityConstants.RatingEffect)
        * AgeFactor(component, mileageLaps);

    /// <summary>Age factor of a part: 1 when new, growing linearly with mileage.</summary>
    public static double AgeFactor(MechanicalComponent component, double mileageLaps) =>
        1 + ReliabilityConstants.AgeSlope * mileageLaps / ReliabilityConstants.ComponentLifeLaps(component);

    /// <summary>
    /// Probability that a reference car retires mechanically within <paramref name="laps"/> laps in the season,
    /// computed from the hazard model (not sampled). At the typical race distance it equals the era rate.
    /// </summary>
    public static double ReferenceRetirementProbability(int season, int laps) =>
        1 - Math.Exp(-BaseHazardScale(season) * ReferenceRetiringHazardPerUnitScale(laps));

    // Retiring hazard of the reference car summed over a race, for a base hazard scale of 1.
    private static double ReferenceRetiringHazardPerUnitScale(int laps)
    {
        var stress = FailureInputs.ComputeStress(
            ReliabilityConstants.ReferenceSympathy,
            ReliabilityConstants.ReferencePaceStress,
            ReliabilityConstants.ReferenceHeat);
        var ratingFactor = 1 - ReliabilityConstants.ReferenceReliabilityRating * ReliabilityConstants.RatingEffect;
        var total = 0.0;
        foreach (var component in Enum.GetValues<MechanicalComponent>())
        {
            var retireShare = ReliabilityConstants.EffectSplit(component).Retire;
            var componentShare = ReliabilityConstants.ComponentShare(component);
            for (var lap = 1; lap <= laps; lap++)
            {
                var mileage = ReliabilityConstants.ReferenceMileageLaps + (lap - 1);
                total += componentShare * retireShare * (1 + stress) * ratingFactor * AgeFactor(component, mileage);
            }
        }

        return total;
    }

    private static double Interpolate(IReadOnlyList<(int Season, double Value)> anchors, int season)
    {
        if (season <= anchors[0].Season)
        {
            return anchors[0].Value;
        }

        for (var i = 1; i < anchors.Count; i++)
        {
            if (season <= anchors[i].Season)
            {
                var (s0, v0) = anchors[i - 1];
                var (s1, v1) = anchors[i];
                var t = (double)(season - s0) / (s1 - s0);
                var eased = t * t * (3 - 2 * t);
                return v0 + (v1 - v0) * eased;
            }
        }

        return anchors[^1].Value;
    }
}
