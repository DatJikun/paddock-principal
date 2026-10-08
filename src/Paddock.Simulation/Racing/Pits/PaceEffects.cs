using Paddock.Simulation.Racing.Reliability;
using Paddock.Simulation.Racing.Tyres;

namespace Paddock.Simulation.Racing.Pits;

/// <summary>How hard the engine is run (#286). Only the pit wall sets it; the strategist leaves it on <see cref="Standard"/>.</summary>
public enum EngineMode
{
    /// <summary>Normal engine settings.</summary>
    Standard,

    /// <summary>Less power, less fuel, gentler on the engine.</summary>
    Lean,

    /// <summary>More power, more fuel, harder on the engine.</summary>
    Full,
}

/// <summary>What a driver mode does to one lap: lap time, tyre wear, fuel burn and the driver's aggression.</summary>
/// <param name="LapSeconds">Seconds added to the lap (negative is faster).</param>
/// <param name="WearFactor">Tyre wear as a multiple of standard.</param>
/// <param name="BurnFactor">Fuel burn as a multiple of standard.</param>
/// <param name="AggressionShift">Change of the driver's aggression (0–100), which drives the incident risk.</param>
public readonly record struct PaceEffect(double LapSeconds, double WearFactor, double BurnFactor, double AggressionShift);

/// <summary>
/// The costs of the driver and engine modes in one place, so the race and the pit wall's read use the same numbers (#286).
/// Every faster mode costs tyres, fuel or reliability; every gentler one costs time (PP-058). Standard is exactly neutral, so a
/// race nobody gives orders in is the race it always was.
/// </summary>
public static class PaceEffects
{
    public static PaceEffect Of(PaceMode mode) => mode switch
    {
        PaceMode.Push => new(-PitConstants.PushPaceGainSeconds, PitConstants.PushWearFactor, PitConstants.PushBurnFactor, 0d),
        PaceMode.Save => new(FuelModel.FuelSavingPaceLossSeconds, PitConstants.SaveWearFactor, 1d - TyreFuelConstants.FuelSavingBurnReduction, 0d),
        PaceMode.Conserve => new(PitConstants.ConservePaceLossSeconds, PitConstants.ConserveWearFactor, PitConstants.ConserveBurnFactor, PitConstants.ConserveAggressionShift),
        PaceMode.Qualifying => new(-PitConstants.QualifyingPaceGainSeconds, PitConstants.QualifyingWearFactor, PitConstants.QualifyingBurnFactor, PitConstants.QualifyingAggressionShift),
        _ => new(0d, 1d, 1d, 0d),
    };

    /// <summary>Engine power as a share of standard.</summary>
    public static double Power(EngineMode mode) => mode switch
    {
        EngineMode.Lean => PitConstants.LeanEnginePower,
        EngineMode.Full => PitConstants.FullEnginePower,
        _ => 1d,
    };

    /// <summary>Fuel burn as a multiple of standard.</summary>
    public static double Burn(EngineMode mode) => mode switch
    {
        EngineMode.Lean => PitConstants.LeanEngineBurn,
        EngineMode.Full => PitConstants.FullEngineBurn,
        _ => 1d,
    };

    /// <summary>The failure hazard of <paramref name="component"/> as a multiple of standard: only the engine and its cooling feel the mode.</summary>
    public static double Hazard(EngineMode mode, MechanicalComponent component) =>
        component is not (MechanicalComponent.Engine or MechanicalComponent.Cooling) ? 1d : mode switch
        {
            EngineMode.Lean => PitConstants.LeanEngineHazard,
            EngineMode.Full => PitConstants.FullEngineHazard,
            _ => 1d,
        };
}
