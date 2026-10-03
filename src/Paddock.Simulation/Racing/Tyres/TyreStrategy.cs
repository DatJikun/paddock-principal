namespace Paddock.Simulation.Racing.Tyres;

/// <summary>
/// The window in which a stop on a set of tyres makes sense, in stint laps (the number of laps run on the set,
/// counting from 1). Calculators only: this is not a decision, the strategist decides.
/// </summary>
/// <param name="EarliestLap">The stint length with the lowest average cost per lap once the stop is paid; stopping earlier wastes the pit loss.</param>
/// <param name="LatestLap">The last lap that is run before the tyre falls off its cliff; stopping later is not worth the risk.</param>
public readonly record struct PitWindow(int EarliestLap, int LatestLap);

/// <summary>
/// Strategy calculators built on the loss functions of <see cref="TyreWear"/>. They only compute; they decide nothing
/// and use no RNG. The conditions are taken as constant over the stint; pass an average weight factor for a stint in
/// which the car gets lighter. All numbers behind them are ESTIMATE.
/// </summary>
public static class TyreStrategy
{
    /// <summary>
    /// The number of laps a set can run before the cliff: laps whose start age keeps the effective age at or below
    /// the cliff lap. With <paramref name="maxWearLossSeconds"/> the stint also ends before the lap whose wear loss
    /// (the loss above a fresh set's) would exceed it. At least 1.
    /// </summary>
    public static int StintLength(TyreCompound compound, TyreConditions conditions, double? maxWearLossSeconds = null)
    {
        ArgumentNullException.ThrowIfNull(compound);
        if (maxWearLossSeconds is { } budget && !(double.IsFinite(budget) && budget >= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(maxWearLossSeconds), "The budget cannot be negative.");
        }

        var multiplier = TyreWear.WearMultiplier(conditions);
        var untilCliff = Math.Max(1, (int)Math.Ceiling(compound.CliffLap / multiplier));
        if (maxWearLossSeconds is null)
        {
            return untilCliff;
        }

        var fresh = TyreWear.LossAtAge(compound, 0, conditions);
        var laps = 1;
        while (laps < untilCliff && TyreWear.LossAtAge(compound, laps, conditions) - fresh <= maxWearLossSeconds)
        {
            laps++;
        }

        return laps;
    }

    /// <summary>
    /// The pit window of a set. The latest lap is <see cref="StintLength"/>. The earliest is the stint length
    /// <c>n</c> that minimises <c>(pitLossSeconds + sum of the lap losses of laps 1..n) / n</c>, because a shorter stint
    /// spreads the pit loss over too few laps; it is never later than the latest lap.
    /// </summary>
    /// <param name="pitLossSeconds">Total time one stop costs against not stopping (pit lane plus stationary time).</param>
    public static PitWindow PitWindow(TyreCompound compound, TyreConditions conditions, double pitLossSeconds)
    {
        ArgumentNullException.ThrowIfNull(compound);
        if (!(double.IsFinite(pitLossSeconds) && pitLossSeconds >= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(pitLossSeconds), "The pit loss cannot be negative.");
        }

        var latest = StintLength(compound, conditions);
        var cumulative = 0.0;
        var best = 1;
        var bestAverage = double.PositiveInfinity;
        for (var n = 1; n <= latest; n++)
        {
            cumulative += TyreWear.LapLoss(compound, n - 1, conditions);
            var average = (pitLossSeconds + cumulative) / n;
            if (average < bestAverage)
            {
                bestAverage = average;
                best = n;
            }
        }

        return new PitWindow(best, latest);
    }

    /// <summary>
    /// Estimated seconds an undercut gains. The undercutting car is on a fresh <paramref name="newCompound"/> set from
    /// its out-lap while the rival stays out for <paramref name="rivalExtraLaps"/> laps on a set that is
    /// <paramref name="rivalAgeLaps"/> laps old at the first of them. Positive means the undercut gains time. Both cars
    /// pay a pit stop (the rival later), so the stop itself is not counted. Warm-up of the new set is included.
    /// </summary>
    public static double UndercutGain(
        TyreCompound rivalCompound,
        int rivalAgeLaps,
        TyreCompound newCompound,
        TyreConditions conditions,
        int rivalExtraLaps = 1)
    {
        ArgumentNullException.ThrowIfNull(rivalCompound);
        ArgumentNullException.ThrowIfNull(newCompound);
        if (rivalAgeLaps < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rivalAgeLaps), "Age cannot be negative.");
        }

        if (rivalExtraLaps < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rivalExtraLaps), "The rival stays out at least one lap.");
        }

        var gain = 0.0;
        for (var i = 0; i < rivalExtraLaps; i++)
        {
            gain += TyreWear.LapLoss(rivalCompound, rivalAgeLaps + i, conditions)
                - TyreWear.LapLoss(newCompound, i, conditions);
        }

        return gain;
    }
}
