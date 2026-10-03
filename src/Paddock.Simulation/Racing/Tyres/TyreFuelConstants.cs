namespace Paddock.Simulation.Racing.Tyres;

/// <summary>
/// Every tunable number of the tyre and fuel models, in one place.
/// ESTIMATE: every value in this file is a guess made to give plausible behaviour. None is calibrated and none is
/// a verified historical fact; a later calibration task is meant to replace them (TECH, "Numbers are estimates
/// until calibrated"). The tyre compound sets per era are synthetic fixtures with the right shape, not a record of
/// what any supplier made.
/// </summary>
public static class TyreFuelConstants
{
    // ---- Tyre wear: loss(e) = GripBase + WearRate*e*(1 + WearGrowthShare*e/CliffLap), e = age * wear multiplier ----

    /// <summary>ESTIMATE: how much faster than linear the wear loss grows; at the cliff lap the slope is (1 + 2 * this) times WearRate.</summary>
    public const double WearGrowthShare = 0.5;

    /// <summary>ESTIMATE: seconds lost at once when the effective age passes the cliff lap.</summary>
    public const double CliffDropSeconds = 0.8;

    /// <summary>ESTIMATE: after the cliff the loss grows this many times WearRate per lap, on top of the pre-cliff curve.</summary>
    public const double CliffSlopeMultiplier = 4.0;

    /// <summary>ESTIMATE: seconds lost on a fresh set that is not warmed up yet; falls linearly to 0 over WarmUpLaps.</summary>
    public const double WarmUpPeakLossSeconds = 1.2;

    /// <summary>ESTIMATE: wear speed changes by this share across the abrasiveness range (0.5 = 50 percent less to 50 percent more).</summary>
    public const double AbrasivenessWearEffect = 0.5;

    /// <summary>ESTIMATE: wear speed changes by this share across the (era-weighted) smoothness range.</summary>
    public const double SmoothnessWearEffect = 0.3;

    /// <summary>ESTIMATE: wear speed scales with the car weight factor to this power.</summary>
    public const double WeightWearExponent = 1.0;

    // ---- Tyre suppliers (PP-030 flavour) ----

    /// <summary>ESTIMATE: seconds per lap a supplier at grip balance +1 gains (balance -1 loses) against a neutral supplier.</summary>
    public const double SupplierGripSwingSeconds = 0.30;

    /// <summary>ESTIMATE: share of extra wear speed of a supplier at grip balance +1 (balance -1 wears this share less).</summary>
    public const double SupplierDurabilitySwing = 0.25;

    /// <summary>ESTIMATE: seconds per lap of grip a partner-tuned tyre gains at partner bonus 1.</summary>
    public const double PartnerGripBonusSeconds = 0.15;

    /// <summary>ESTIMATE: share of wear a partner-tuned tyre saves at partner bonus 1.</summary>
    public const double PartnerWearSaving = 0.10;

    // ---- Wet compound hook (dry-running numbers only; the weather interaction is out of scope) ----

    /// <summary>ESTIMATE: seconds per lap a wet-weather tyre loses on a dry track.</summary>
    public const double WetCompoundDryGripLossSeconds = 4.0;

    /// <summary>ESTIMATE: wear speed of a wet-weather tyre on a dry track, seconds per lap per lap.</summary>
    public const double WetCompoundDryWearRate = 0.20;

    /// <summary>ESTIMATE: cliff lap of a wet-weather tyre on a dry track.</summary>
    public const double WetCompoundDryCliffLap = 8;

    /// <summary>ESTIMATE: warm-up laps of a wet-weather tyre.</summary>
    public const double WetCompoundWarmUpLaps = 1;

    // ---- Fuel ----

    /// <summary>ESTIMATE: seconds lost per kg of fuel on board, per lap (kept equal to T29's FuelSecondsPerKg).</summary>
    public const double FuelSecondsPerKg = 0.035;

    /// <summary>ESTIMATE: kg per litre, used to turn a litre allowance from the regulation data into kg.</summary>
    public const double FuelDensityKgPerLitre = 0.74;

    /// <summary>ESTIMATE: litre allowance of the turbo class in the 1987 rule value "na_unlimited_turbo_restricted" (the data gives no number).</summary>
    public const double TurboRestrictedLitres1987 = 195;

    /// <summary>ESTIMATE: litre allowance of the turbo class in the rule value "turbo_150_na_unlimited" (the number is read from the value name).</summary>
    public const double TurboLitres150 = 150;

    /// <summary>ESTIMATE: cap in kg for a hybrid car in seasons where the data has no race fuel allowance ("unknown").</summary>
    public const double HybridUnknownAllowanceCapKg = 100;

    /// <summary>ESTIMATE: share of extra fuel a team loads over the computed need.</summary>
    public const double StartLoadSafetyMargin = 0.03;

    /// <summary>ESTIMATE: fuel-saving mode burns this share less fuel.</summary>
    public const double FuelSavingBurnReduction = 0.10;

    /// <summary>ESTIMATE: fuel-saving mode costs this many seconds per lap.</summary>
    public const double FuelSavingPaceLossSeconds = 0.30;

    /// <summary>ESTIMATE: kg of fuel per 100 km, naturally aspirated engines, by season (smoothstep between anchors, flat outside).</summary>
    public static readonly IReadOnlyList<(int Season, double Value)> AspiratedBurnKgPer100KmAnchors =
    [
        (1950, 62),
        (1990, 58),
        (2006, 55),
    ];

    /// <summary>ESTIMATE: kg of fuel per 100 km, turbo engines (unconstrained demand; a race cap may force less).</summary>
    public const double TurboBurnKgPer100Km = 72;

    /// <summary>ESTIMATE: kg of fuel per 100 km, hybrid engines; a 305 km race needs about 100 kg.</summary>
    public const double HybridBurnKgPer100Km = 33;

    /// <summary>ESTIMATE: a car's mass without fuel, kg, for the tyre weight factor.</summary>
    public const double ReferenceDryMassKg = 700;

    /// <summary>ESTIMATE: fuel load at which the tyre weight factor is exactly 1, kg.</summary>
    public const double ReferenceFuelKg = 50;

    // ---- Pit stops ----

    /// <summary>ESTIMATE: first season of the fast, pressurised refuelling rigs; before it fuel went in by gravity.</summary>
    public const int FastRigFirstSeason = 1994;

    /// <summary>ESTIMATE: refuel rate with a gravity rig, kg per second.</summary>
    public const double GravityRigKgPerSecond = 3.0;

    /// <summary>ESTIMATE: refuel rate with a pressurised rig, kg per second.</summary>
    public const double FastRigKgPerSecond = 12.0;

    /// <summary>ESTIMATE: seconds a tyre change takes (stationary), by season (smoothstep between anchors).</summary>
    public static readonly IReadOnlyList<(int Season, double Value)> TyreChangeSecondsAnchors =
    [
        (1950, 30),
        (1970, 15),
        (1990, 9),
        (2010, 3),
    ];
}
