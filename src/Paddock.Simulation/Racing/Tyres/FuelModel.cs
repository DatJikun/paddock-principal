using System.Globalization;
using Paddock.Domain.World;

namespace Paddock.Simulation.Racing.Tyres;

/// <summary>
/// Fuel: how much a car burns and loads, what the mass costs in lap time, and what refuelling costs in a pit
/// stop. Pure and RNG-free; every number ESTIMATE (see <see cref="TyreFuelConstants"/>).
/// The fuel rules come from a function season to <see cref="FuelRegime"/>: use <see cref="FromRuleSets"/> for the
/// real regulation data, or pass any function (a dimension the data lacks becomes a parameter, not a data edit).
/// </summary>
public sealed class FuelModel
{
    private readonly Func<int, FuelRegime> _regimeForSeason;

    public FuelModel(Func<int, FuelRegime> regimeForSeason)
    {
        ArgumentNullException.ThrowIfNull(regimeForSeason);
        _regimeForSeason = regimeForSeason;
    }

    /// <summary>A model whose fuel rules are read from the rule set of each season.</summary>
    /// <param name="ruleSetFor">The rule set of a season, for example <c>AuthoredData.RuleSetFor</c>.</param>
    /// <param name="refuellingFallback">Used for a season whose rule set has no refuelling dimension.</param>
    public static FuelModel FromRuleSets(Func<int, RuleSet> ruleSetFor, bool? refuellingFallback = null)
    {
        ArgumentNullException.ThrowIfNull(ruleSetFor);
        return new FuelModel(season => FuelRegime.FromRules(ruleSetFor(season), refuellingFallback));
    }

    /// <summary>The seconds fuel-saving mode costs per lap. ESTIMATE.</summary>
    public static double FuelSavingPaceLossSeconds => TyreFuelConstants.FuelSavingPaceLossSeconds;

    /// <summary>The fuel rules of the season.</summary>
    public FuelRegime Regime(int season) => _regimeForSeason(season);

    /// <summary>Whether cars may be refuelled during the race in the season.</summary>
    public bool RefuellingAllowed(int season) => Regime(season).RefuellingAllowed;

    /// <summary>
    /// The race fuel cap in kg, or null for none. Where the data says the allowance is unknown, a hybrid car gets
    /// <see cref="TyreFuelConstants.HybridUnknownAllowanceCapKg"/> (ESTIMATE).
    /// </summary>
    public double? RaceFuelCapKg(int season, FuelEngineType engine)
    {
        var regime = Regime(season);
        var cap = engine == FuelEngineType.NaturallyAspirated ? regime.AspiratedCapKg : regime.ForcedInductionCapKg;
        if (cap is null && regime.AllowanceUnknown && engine == FuelEngineType.Hybrid)
        {
            return TyreFuelConstants.HybridUnknownAllowanceCapKg;
        }

        return cap;
    }

    /// <summary>What the engine would burn without a cap, kg per 100 km at race pace.</summary>
    public static double DemandKgPer100Km(int season, FuelEngineType engine) => engine switch
    {
        FuelEngineType.NaturallyAspirated => EraCurve.Interpolate(TyreFuelConstants.AspiratedBurnKgPer100KmAnchors, season),
        FuelEngineType.Turbo => TyreFuelConstants.TurboBurnKgPer100Km,
        FuelEngineType.Hybrid => TyreFuelConstants.HybridBurnKgPer100Km,
        _ => throw new ArgumentOutOfRangeException(nameof(engine)),
    };

    /// <summary>
    /// Fuel burnt per lap, kg. The engine's demand for the track length, less <see cref="TyreFuelConstants.FuelSavingBurnReduction"/>
    /// in fuel-saving mode, and never more than the race fuel cap spread over the scheduled laps (a car held to a
    /// cap has to lift and coast). With a cap that binds, the saving mode adds nothing more to the burn.
    /// </summary>
    public double BurnPerLapKg(int season, FuelEngineType engine, double trackLengthKm, int scheduledLaps, bool fuelSaving = false)
    {
        PositiveLength(trackLengthKm);
        PositiveLaps(scheduledLaps);

        var burn = DemandKgPer100Km(season, engine) * trackLengthKm / 100;
        if (fuelSaving)
        {
            burn *= 1 - TyreFuelConstants.FuelSavingBurnReduction;
        }

        if (RaceFuelCapKg(season, engine) is { } cap)
        {
            burn = Math.Min(burn, cap / scheduledLaps);
        }

        return burn;
    }

    /// <summary>
    /// Fuel loaded at the start, kg: the need of the first stint (the race divided by the stops plus one) plus the
    /// safety margin, never more than the race fuel cap. With refuelling banned there are no fuel stops.
    /// </summary>
    /// <exception cref="ArgumentException">Fuel stops are planned but refuelling is banned in the season.</exception>
    public double StartLoadKg(int season, FuelEngineType engine, double trackLengthKm, int scheduledLaps, int plannedFuelStops = 0)
    {
        PositiveLaps(scheduledLaps);
        if (plannedFuelStops < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(plannedFuelStops), "Planned stops cannot be negative.");
        }

        if (plannedFuelStops > 0 && !RefuellingAllowed(season))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"Refuelling is banned in {season}; no fuel stops can be planned."),
                nameof(plannedFuelStops));
        }

        var stintLaps = (int)Math.Ceiling(scheduledLaps / (double)(plannedFuelStops + 1));
        var load = BurnPerLapKg(season, engine, trackLengthKm, scheduledLaps) * stintLaps * (1 + TyreFuelConstants.StartLoadSafetyMargin);
        return RaceFuelCapKg(season, engine) is { } cap ? Math.Min(load, cap) : load;
    }

    /// <summary>Fuel left after <paramref name="laps"/> laps, kg; never below 0.</summary>
    public static double FuelAfterLapsKg(double startKg, double burnPerLapKg, int laps)
    {
        NotNegative(startKg, nameof(startKg));
        NotNegative(burnPerLapKg, nameof(burnPerLapKg));
        if (laps < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(laps), "Laps cannot be negative.");
        }

        return Math.Max(0, startKg - burnPerLapKg * laps);
    }

    /// <summary>
    /// Seconds a lap is slower with <paramref name="fuelKg"/> on board: <see cref="TyreFuelConstants.FuelSecondsPerKg"/>
    /// per kg (ESTIMATE, 0.035 s per kg per lap, the same value T29 uses).
    /// </summary>
    public static double MassPenaltySeconds(double fuelKg)
    {
        NotNegative(fuelKg, nameof(fuelKg));
        return fuelKg * TyreFuelConstants.FuelSecondsPerKg;
    }

    /// <summary>
    /// The tyre <see cref="TyreConditions.CarWeightFactor"/> of a car with <paramref name="fuelKg"/> on board:
    /// total mass over the mass at the reference fuel load. Falls over a stint as the tank empties.
    /// </summary>
    public static double CarWeightFactor(double fuelKg)
    {
        NotNegative(fuelKg, nameof(fuelKg));
        return (TyreFuelConstants.ReferenceDryMassKg + fuelKg) / (TyreFuelConstants.ReferenceDryMassKg + TyreFuelConstants.ReferenceFuelKg);
    }

    /// <summary>Refuel rate in kg per second: a gravity rig before <see cref="TyreFuelConstants.FastRigFirstSeason"/>, a fast rig from it.</summary>
    public static double RefuelRateKgPerSecond(int season) =>
        season < TyreFuelConstants.FastRigFirstSeason ? TyreFuelConstants.GravityRigKgPerSecond : TyreFuelConstants.FastRigKgPerSecond;

    /// <summary>Seconds a tyre change takes while the car is stationary.</summary>
    public static double TyreChangeSeconds(int season) => EraCurve.Interpolate(TyreFuelConstants.TyreChangeSecondsAnchors, season);

    /// <summary>Seconds it takes to put <paramref name="kg"/> of fuel in. Throws when kg is above 0 and refuelling is banned.</summary>
    public double RefuelSeconds(int season, double kg)
    {
        NotNegative(kg, nameof(kg));
        if (kg > 0 && !RefuellingAllowed(season))
        {
            throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture, $"Refuelling is banned in {season}."));
        }

        return kg / RefuelRateKgPerSecond(season);
    }

    /// <summary>
    /// Seconds the car stands still in the box. Tyres and fuel are handled at the same time, so the stop lasts as long
    /// as the slower of the two jobs. The time lost driving through the pit lane is not included (it belongs to the track).
    /// </summary>
    public double StationarySeconds(int season, double refuelKg, bool changeTyres) =>
        Math.Max(changeTyres ? TyreChangeSeconds(season) : 0, RefuelSeconds(season, refuelKg));

    private static void PositiveLength(double km)
    {
        if (!(double.IsFinite(km) && km > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(km), "The track length must be positive.");
        }
    }

    private static void PositiveLaps(int laps)
    {
        if (laps < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(laps), "A race has at least one lap.");
        }
    }

    private static void NotNegative(double value, string name)
    {
        if (!(double.IsFinite(value) && value >= 0))
        {
            throw new ArgumentOutOfRangeException(name, "The value cannot be negative.");
        }
    }
}
