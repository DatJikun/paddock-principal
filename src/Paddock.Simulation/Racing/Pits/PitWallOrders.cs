using Paddock.Simulation.Racing.Tyres;

namespace Paddock.Simulation.Racing.Pits;

/// <summary>What a pit wall order tells a car to do (#286).</summary>
public enum PitWallOrderKind
{
    /// <summary>Run in <see cref="PitWallOrder.Pace"/> from the order's lap on, whatever the strategist says, until an <see cref="Auto"/>.</summary>
    Pace,

    /// <summary>Come in at the end of the order's lap, for <see cref="PitWallOrder.CompoundId"/> (null keeps the tyres) and the fuel to the flag.</summary>
    Pit,

    /// <summary>Give the car's pace back to the strategist from the order's lap on.</summary>
    Auto,

    /// <summary>Run the engine in <see cref="PitWallOrder.Engine"/> from the order's lap on.</summary>
    Engine,

    /// <summary>Team order: from the order's lap on, let the team-mate by when it is right behind (<see cref="PitWallOrder.On"/>), or stop.</summary>
    LetBy,
}

/// <summary>
/// An order from a team's pit wall to one of its cars during the race (#286). It is data with a lap number, so a race run
/// with the same orders is the same race (INV-002), and an order only changes laps from its own on: what was driven before
/// it stays as it was. <see cref="CarId"/> is the id the tape uses for the car (its first driver).
/// </summary>
/// <param name="CarId">The car, by its tape id.</param>
/// <param name="Lap">The lap the order acts on: a pace from this lap on, a stop at the end of this lap.</param>
/// <param name="Kind">What the order does.</param>
/// <param name="Pace">The pace of a <see cref="PitWallOrderKind.Pace"/> order.</param>
/// <param name="CompoundId">The tyres of a <see cref="PitWallOrderKind.Pit"/> order; null for no tyre change.</param>
/// <param name="Engine">The engine mode of an <see cref="PitWallOrderKind.Engine"/> order.</param>
/// <param name="On">Whether a <see cref="PitWallOrderKind.LetBy"/> order switches the team order on or off.</param>
public sealed record PitWallOrder(
    string CarId,
    int Lap,
    PitWallOrderKind Kind,
    PaceMode Pace = PaceMode.Standard,
    string? CompoundId = null,
    EngineMode Engine = EngineMode.Standard,
    bool On = false);

/// <summary>How the driver says the tyres feel. The pit wall hears this on the radio; it is not the true wear (INV-003).</summary>
public enum TyreFeel
{
    /// <summary>The set still has grip.</summary>
    Good,

    /// <summary>The set is going off.</summary>
    Worn,

    /// <summary>The set is past its cliff.</summary>
    Gone,
}

/// <summary>
/// What a team's pit wall knows about its own car at the start of a lap (#286): the telemetry the strategist already reads
/// (<see cref="OwnCarKnowledge"/>) plus how the driver says the tyres feel. Never the true wear, wetness or rival data (INV-003).
/// </summary>
/// <param name="CarId">The car, by its tape id.</param>
/// <param name="Lap">The lap about to be driven.</param>
/// <param name="StartMs">Race time at the start of the lap.</param>
/// <param name="CompoundId">The tyres on the car.</param>
/// <param name="TyreAgeLaps">Laps on the set.</param>
/// <param name="FuelKg">Fuel on board.</param>
/// <param name="BurnKg">Fuel burnt per lap at standard pace.</param>
/// <param name="Pace">The pace the car runs this lap.</param>
/// <param name="Manual">True when the pace is the pit wall's order, not the strategist's.</param>
/// <param name="Feel">How the driver says the tyres feel.</param>
/// <param name="Engine">The engine mode this lap.</param>
/// <param name="LetBy">True while the team order to let the team-mate by is on.</param>
public sealed record PitWallLap(
    string CarId,
    int Lap,
    long StartMs,
    string CompoundId,
    int TyreAgeLaps,
    double FuelKg,
    double BurnKg,
    PaceMode Pace,
    bool Manual,
    TyreFeel Feel,
    EngineMode Engine = EngineMode.Standard,
    bool LetBy = false);

/// <summary>The driver's sense of the tyres. ESTIMATE thresholds, uncalibrated.</summary>
public static class TyreFeelBands
{
    /// <summary>ESTIMATE: share of the cliff lap after which the driver says the tyres are going off.</summary>
    public const double WornFromCliffShare = 0.6;

    /// <summary>The driver's word for a set.</summary>
    public static TyreFeel Of(TyreSet set) =>
        set.PastCliff ? TyreFeel.Gone
        : set.Wear >= WornFromCliffShare * set.Compound.CliffLap ? TyreFeel.Worn
        : TyreFeel.Good;
}
