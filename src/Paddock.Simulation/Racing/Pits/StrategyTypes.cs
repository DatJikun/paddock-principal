using System.Collections.Immutable;
using Paddock.Simulation.Racing.Weather;

namespace Paddock.Simulation.Racing.Pits;

/// <summary>How hard the driver is told to run.</summary>
public enum PaceMode
{
    /// <summary>Normal racing pace.</summary>
    Standard,

    /// <summary>Faster, and harder on tyres and fuel (<see cref="PitConstants.PushPaceGainSeconds"/> and friends).</summary>
    Push,

    /// <summary>Slower, easier on tyres and fuel (T30's fuel-saving numbers and <see cref="PitConstants.SaveWearFactor"/>).</summary>
    Save,
}

/// <summary>A tyre compound as the strategist sees it: an id and whether it is a wet-weather tyre. Nothing else is needed; the numbers live behind the calculators.</summary>
public sealed record CompoundInfo(string Id, bool IsWet);

/// <summary>
/// What the team knows about its own car. Telemetry the pit wall really has: tyre age (laps on the set, not its true wear),
/// fuel on board and burn, the compounds already used, the crew it employs.
/// </summary>
/// <param name="CarId">Stable id of the car.</param>
/// <param name="CompoundId">Compound on the car now.</param>
/// <param name="TyreAgeLaps">Laps run on the current set.</param>
/// <param name="FuelKg">Fuel on board, kg.</param>
/// <param name="FuelBurnPerLapKg">Fuel burnt per lap at standard pace, kg.</param>
/// <param name="PaceMode">The mode the car runs in now.</param>
/// <param name="CompoundsUsed">Compounds the car has run so far, the current one included.</param>
/// <param name="StopsMade">Stops made so far.</param>
/// <param name="CanSwapDriver">A driver change is possible: the car is shared and the co-driver has not driven yet.</param>
/// <param name="DriverStintLaps">Laps the current driver has driven.</param>
/// <param name="Crew">The team's own pit crew.</param>
public sealed record OwnCarKnowledge(
    string CarId,
    string CompoundId,
    int TyreAgeLaps,
    double FuelKg,
    double FuelBurnPerLapKg,
    PaceMode PaceMode,
    ImmutableArray<string> CompoundsUsed,
    int StopsMade,
    bool CanSwapDriver,
    int DriverStintLaps,
    PitCrew Crew);

/// <summary>The gaps the pit wall can see on the timing screen. A null gap means there is no car there.</summary>
public sealed record VisibleGaps(int Position, double? GapAheadSeconds, double? GapBehindSeconds);

/// <summary>
/// Everything a team may know when it decides (INV-003). It holds the team's own car, the gaps on the timing screen,
/// the rules, the compounds on offer and a weather <b>forecast</b>. It must never hold simulation truth: no
/// <c>TruthWeather</c>, no track wetness, no failure or incident draws, no rival telemetry. A test walks the type and
/// fails if a truth type is added.
/// </summary>
/// <param name="Season">The season.</param>
/// <param name="Lap">The lap about to be driven, from 1.</param>
/// <param name="TotalLaps">Scheduled laps of the race.</param>
/// <param name="ReferenceLapSeconds">A typical lap time, for turning laps into race minutes.</param>
/// <param name="RaceMinute">Minutes into the race now.</param>
/// <param name="Rules">The pit rules of the season.</param>
/// <param name="PitLaneLossSeconds">Time a stop costs in the lane (<see cref="PitRules.PitLaneTimeLossSeconds"/>); a track fact the team knows.</param>
/// <param name="TankCapacityKg">The most fuel the car can carry (the race fuel cap); null if unlimited.</param>
/// <param name="Compounds">The compounds the team may fit.</param>
/// <param name="Car">The team's own car.</param>
/// <param name="Gaps">The visible gaps.</param>
/// <param name="Forecast">The weather forecast the team has (a noisy view, not the truth); null if it has none.</param>
public sealed record KnowledgeSnapshot(
    int Season,
    int Lap,
    int TotalLaps,
    double ReferenceLapSeconds,
    double RaceMinute,
    PitRules Rules,
    double PitLaneLossSeconds,
    double? TankCapacityKg,
    ImmutableArray<CompoundInfo> Compounds,
    OwnCarKnowledge Car,
    VisibleGaps Gaps,
    WeatherForecast? Forecast);

/// <summary>What a strategist decided to do.</summary>
public enum StrategyAction
{
    /// <summary>Carry on; pace mode unchanged.</summary>
    StayOut,

    /// <summary>Come in this lap.</summary>
    PitNow,

    /// <summary>Stay out and change the pace mode.</summary>
    ChangePace,
}

/// <summary>One term of an option's utility, in seconds (negative is a cost).</summary>
public sealed record StrategyFactor(string Name, double Contribution);

/// <summary>One option the strategist considered. <see cref="Utility"/> is minus the perceived time lost over the rest of the race, seconds (higher is better).</summary>
public sealed record StrategyOption(string Id, double Utility, ImmutableArray<StrategyFactor> Factors);

/// <summary>
/// Why a strategist decided what it decided: every option with its utility and factors (TECH §7, so Spy can explain it).
/// Integration point: this record is shaped like the <c>TraceOption</c> / <c>TraceFactor</c> / <c>DecisionTrace</c> of
/// Application.Spy (which this project cannot reference); the race loop's adapter maps one to the other.
/// </summary>
/// <param name="CarId">The car the decision was for.</param>
/// <param name="Lap">The lap it was made on.</param>
/// <param name="Skill">The strategist's skill, 0 to 100.</param>
/// <param name="Trigger">What prompted the decision (a regular lap check).</param>
/// <param name="Options">Everything considered, in a fixed order.</param>
/// <param name="ChosenOptionId">Id of the option with the highest utility.</param>
/// <param name="Reason">Developer-facing text; not for the player.</param>
public sealed record StrategyDecisionRecord(
    string CarId,
    int Lap,
    int Skill,
    string Trigger,
    ImmutableArray<StrategyOption> Options,
    string ChosenOptionId,
    string Reason);

/// <summary>
/// The decision of a strategist for one lap. Swapping the driver is a <see cref="StrategyAction.PitNow"/> with
/// <see cref="SwapDriver"/> set (with a null <see cref="CompoundId"/> and no fuel if nothing else is done).
/// </summary>
/// <param name="Action">What to do.</param>
/// <param name="CompoundId">The compound to fit when pitting; null for no tyre change.</param>
/// <param name="RefuelKg">Fuel to put in when pitting, kg; 0 for none (always 0 where refuelling is banned).</param>
/// <param name="SwapDriver">Change the driver in this stop.</param>
/// <param name="Pace">The pace mode from this lap on (a stop is made at the end of the lap).</param>
/// <param name="Record">The options considered.</param>
public sealed record StrategyDecision(
    StrategyAction Action,
    string? CompoundId,
    double RefuelKg,
    bool SwapDriver,
    PaceMode Pace,
    StrategyDecisionRecord Record)
{
    /// <summary>A canonical text of the decision, for logs and for comparing decisions.</summary>
    public string Summary => Action switch
    {
        StrategyAction.StayOut => "stay_out/" + Pace,
        StrategyAction.ChangePace => "change_pace/" + Pace,
        _ => string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"pit_now/{CompoundId ?? "keep"}/{RefuelKg:F1}kg/{(SwapDriver ? "swap" : "same")}/{Pace}"),
    };
}

/// <summary>A strategist: reads what the team knows and decides. It never sees the simulation truth.</summary>
public interface IRaceStrategist
{
    /// <summary>
    /// Decides for the lap in <paramref name="snapshot"/>. Must not change the snapshot or any shared state, and must give the
    /// same answer for the same snapshot (INV-002, INV-005).
    /// </summary>
    StrategyDecision Decide(KnowledgeSnapshot snapshot);
}
