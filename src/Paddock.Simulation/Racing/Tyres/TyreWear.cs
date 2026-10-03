using System.Globalization;

namespace Paddock.Simulation.Racing.Tyres;

/// <summary>
/// The conditions that make a tyre wear faster or slower.
/// </summary>
/// <param name="TrackAbrasiveness">0 (smooth) to 1 (very abrasive); 0.5 is a typical track.</param>
/// <param name="DriverSmoothness">0 (harsh) to 1 (gentle); 0.5 is typical. Pass the era-weighted value (DESIGN: the weight of smoothness depends on the era; that weighting lives with the ratings, not here).</param>
/// <param name="CarWeightFactor">1 at the reference weight, larger when heavier (see <see cref="FuelModel.CarWeightFactor"/>).</param>
public readonly record struct TyreConditions(double TrackAbrasiveness, double DriverSmoothness, double CarWeightFactor)
{
    /// <summary>A typical track, a typical driver, the reference weight.</summary>
    public static TyreConditions Reference { get; } = new(0.5, 0.5, 1.0);
}

/// <summary>
/// Tyre loss as pure functions of age and conditions. No RNG, no state; every number is an ESTIMATE
/// (see <see cref="TyreFuelConstants"/>).
/// <para>
/// Age is the number of laps already run on the set when the lap starts (0 for a fresh set). The effective
/// age is <c>age * WearMultiplier</c>. The loss of a lap is
/// <c>GripBase + WearRate * e * (1 + WearGrowthShare * e / CliffLap)</c> for effective age <c>e</c> up to the cliff,
/// and past it that plus <c>CliffDropSeconds + CliffSlopeMultiplier * WearRate * (e - CliffLap)</c>.
/// The warm-up penalty of a fresh set is separate (<see cref="WarmUpLoss"/>) so that the wear loss is monotonic in age.
/// </para>
/// </summary>
public static class TyreWear
{
    /// <summary>
    /// How much faster than a typical set a set wears under the conditions (1 = typical). Abrasiveness and
    /// smoothness each move it by their effect constant; weight scales it by a power.
    /// </summary>
    public static double WearMultiplier(double trackAbrasiveness, double driverSmoothness, double carWeightFactor)
    {
        Unit(trackAbrasiveness, nameof(trackAbrasiveness));
        Unit(driverSmoothness, nameof(driverSmoothness));
        if (!(double.IsFinite(carWeightFactor) && carWeightFactor > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(carWeightFactor), "The weight factor must be positive.");
        }

        var abrasive = 1 + TyreFuelConstants.AbrasivenessWearEffect * (trackAbrasiveness - 0.5) * 2;
        var smooth = 1 + TyreFuelConstants.SmoothnessWearEffect * (0.5 - driverSmoothness) * 2;
        return abrasive * smooth * Math.Pow(carWeightFactor, TyreFuelConstants.WeightWearExponent);
    }

    /// <summary><see cref="WearMultiplier(double, double, double)"/> for a <see cref="TyreConditions"/>.</summary>
    public static double WearMultiplier(TyreConditions conditions) =>
        WearMultiplier(conditions.TrackAbrasiveness, conditions.DriverSmoothness, conditions.CarWeightFactor);

    /// <summary>
    /// Seconds lost per lap, relative to the era's grippiest compound, by a set that has run <paramref name="ageLaps"/>
    /// laps, grip and wear included, warm-up not. Never decreases with age; jumps at the cliff.
    /// </summary>
    public static double LossAtAge(
        TyreCompound compound,
        int ageLaps,
        double trackAbrasiveness,
        double driverSmoothness,
        double carWeightFactor)
    {
        ArgumentNullException.ThrowIfNull(compound);
        if (ageLaps < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ageLaps), "Age cannot be negative.");
        }

        var multiplier = WearMultiplier(trackAbrasiveness, driverSmoothness, carWeightFactor);
        return LossAtWear(compound, ageLaps * multiplier);
    }

    /// <summary><see cref="LossAtAge(TyreCompound, int, double, double, double)"/> for a <see cref="TyreConditions"/>.</summary>
    public static double LossAtAge(TyreCompound compound, int ageLaps, TyreConditions conditions) =>
        LossAtAge(compound, ageLaps, conditions.TrackAbrasiveness, conditions.DriverSmoothness, conditions.CarWeightFactor);

    /// <summary>
    /// The same loss, from the accumulated effective age ("wear", in laps of typical running) instead of the age.
    /// This is what a <see cref="TyreSet"/> uses, because the conditions can change from lap to lap.
    /// </summary>
    public static double LossAtWear(TyreCompound compound, double wear)
    {
        ArgumentNullException.ThrowIfNull(compound);
        if (!(double.IsFinite(wear) && wear >= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(wear), "Wear cannot be negative.");
        }

        var loss = compound.GripBase
            + compound.WearRate * wear * (1 + TyreFuelConstants.WearGrowthShare * wear / compound.CliffLap);
        if (wear > compound.CliffLap)
        {
            loss += TyreFuelConstants.CliffDropSeconds
                + TyreFuelConstants.CliffSlopeMultiplier * compound.WearRate * (wear - compound.CliffLap);
        }

        return loss;
    }

    /// <summary>
    /// Seconds a set that is not warm yet loses on top of <see cref="LossAtAge(TyreCompound, int, double, double, double)"/>:
    /// <c>WarmUpPeakLossSeconds</c> on a fresh set, falling linearly to 0 after <c>WarmUpLaps</c> laps.
    /// </summary>
    public static double WarmUpLoss(TyreCompound compound, int ageLaps)
    {
        ArgumentNullException.ThrowIfNull(compound);
        if (ageLaps < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ageLaps), "Age cannot be negative.");
        }

        if (compound.WarmUpLaps <= 0 || ageLaps >= compound.WarmUpLaps)
        {
            return 0;
        }

        return TyreFuelConstants.WarmUpPeakLossSeconds * (1 - ageLaps / compound.WarmUpLaps);
    }

    /// <summary>The whole tyre loss of a lap: <see cref="LossAtAge(TyreCompound, int, TyreConditions)"/> plus <see cref="WarmUpLoss"/>.</summary>
    public static double LapLoss(TyreCompound compound, int ageLaps, TyreConditions conditions) =>
        LossAtAge(compound, ageLaps, conditions) + WarmUpLoss(compound, ageLaps);

    private static void Unit(double value, string name)
    {
        if (!(double.IsFinite(value) && value >= 0 && value <= 1))
        {
            throw new ArgumentOutOfRangeException(
                name,
                string.Create(CultureInfo.InvariantCulture, $"{name} must be in [0, 1]."));
        }
    }
}
