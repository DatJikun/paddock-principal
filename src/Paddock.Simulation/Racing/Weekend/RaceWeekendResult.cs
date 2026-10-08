using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Paddock.Domain.Racing;
using Paddock.Simulation.Racing.Incidents;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Racing.Qualifying;
using Paddock.Simulation.Racing.Reliability;
using Paddock.Simulation.Racing.Weather;

namespace Paddock.Simulation.Racing.Weekend;

/// <summary>How one car's race ended and what it did on the way. Listed in finishing order: laps completed, then time.</summary>
/// <param name="CarId">The car.</param>
/// <param name="DriverId">The primary driver (the id on the tape and in the classification).</param>
/// <param name="ConstructorId">The constructor.</param>
/// <param name="GridPosition">1 for pole; the order the car started in.</param>
/// <param name="Status">Running at the flag, or the bucket the retirement belongs to.</param>
/// <param name="LapsCompleted">Laps completed (a car flagged a lap down has fewer than the winner).</param>
/// <param name="TotalSeconds">Race time at the last completed lap (at the flag for a finisher); 0 when no lap was completed.</param>
/// <param name="RetiredOnLap">The lap the car stopped on; null for a finisher.</param>
/// <param name="FailedComponent">The part that ended the race, for a mechanical retirement.</param>
/// <param name="DriversWhoDrove">Everyone who drove the car, primary first (a shared car lists two once the swap happened).</param>
/// <param name="Stops">Pit stops made, repairs included.</param>
/// <param name="CompoundsUsed">Compounds fitted during the race, in order of use, duplicates kept.</param>
/// <param name="SetFastestLap">This car set the fastest race lap.</param>
/// <param name="SuddenFailure">The mechanical retirement arrived with no warning (PP-057). False for a finisher and for a warned failure.</param>
public sealed record CarRaceResult(
    string CarId,
    string DriverId,
    string ConstructorId,
    int GridPosition,
    FinishStatus Status,
    int LapsCompleted,
    double TotalSeconds,
    int? RetiredOnLap,
    MechanicalComponent? FailedComponent,
    ImmutableArray<string> DriversWhoDrove,
    int Stops,
    ImmutableArray<string> CompoundsUsed,
    bool SetFastestLap,
    bool SuddenFailure = false)
{
    /// <summary>The car as the points rules read it.</summary>
    public RaceResultInput ToInput() => new(
        DriverId,
        ConstructorId,
        Status,
        LapsCompleted,
        [.. DriversWhoDrove.Skip(1)],
        SetFastestLap);
}

/// <summary>
/// What the race did to one person, as a fact. Nothing here is applied to any state (that is T47): injury and death are
/// reported with T33's grades.
/// </summary>
/// <param name="DriverId">The person.</param>
/// <param name="CarId">The car the person drove in.</param>
/// <param name="LapsDriven">Laps the person drove.</param>
/// <param name="Retired">The person's race ended before the flag.</param>
/// <param name="Reason">Why, when retired.</param>
/// <param name="Component">The failed part, for a mechanical retirement.</param>
/// <param name="Lap">The lap the race ended on for the person, when retired.</param>
/// <param name="Injury">The injury grade (T33): none, light, serious, career-ending.</param>
/// <param name="Fatal">The person was killed (only when the career's fatality option is on).</param>
public sealed record PersonRaceOutcome(
    string DriverId,
    string CarId,
    int LapsDriven,
    bool Retired,
    RetirementReason? Reason,
    MechanicalComponent? Component,
    int? Lap,
    InjuryGrade Injury,
    bool Fatal);

/// <summary>One lap of one car, developer-only truth (never shown to a manager): what the lap was made of.</summary>
/// <param name="CarId">The car.</param>
/// <param name="Lap">The lap, from 1.</param>
/// <param name="DriverId">The driver at the wheel.</param>
/// <param name="LapSeconds">The lap time, a pit stop included.</param>
/// <param name="EndSeconds">Race time at the end of the lap.</param>
/// <param name="Position">Position at the end of the lap.</param>
/// <param name="NoiseSeconds">The noise layer of the lap time (T29).</param>
/// <param name="TyreCompound">The compound on the car during the lap.</param>
/// <param name="TyreAgeLaps">Laps run on the set before this lap.</param>
/// <param name="FuelKg">Fuel at the start of the lap.</param>
/// <param name="Neutralised">The lap was run under a safety car or a virtual safety car.</param>
/// <param name="Held">The car was held up behind a slower car it could not pass.</param>
public sealed record CarLapRecord(
    string CarId,
    int Lap,
    string DriverId,
    double LapSeconds,
    double EndSeconds,
    int Position,
    double NoiseSeconds,
    string TyreCompound,
    int TyreAgeLaps,
    double FuelKg,
    bool Neutralised,
    bool Held);

/// <summary>A pit stop (planned or a repair).</summary>
public sealed record PitStopRecord(
    string CarId,
    int Lap,
    double TotalSeconds,
    bool ChangedTyres,
    string CompoundAfter,
    double RefuelKg,
    bool DriverSwap,
    bool IsRepair,
    PitStopFault Fault);

/// <summary>A neutralisation that happened, from the lap after the incident to its last lap.</summary>
/// <param name="Kind">The kind (T33).</param>
/// <param name="IncidentLap">The lap of the incident.</param>
/// <param name="LastLap">The last lap run under it (the incident lap itself for a red flag that ends the race).</param>
/// <param name="RaceResumed">For a red flag: the race was restarted.</param>
public sealed record NeutralisationRecord(NeutralisationKind Kind, int IncidentLap, int LastLap, bool RaceResumed);

/// <summary>
/// What one race weekend produced. Developer-only parts (<see cref="TruthWeather"/>, <see cref="LapRecords"/>) are simulation
/// truth: a manager or an AI never gets them (INV-003). Decision traces go to the <see cref="Paddock.Domain.Spy.ITraceSink"/>
/// that was passed in, not into this result, so the result is the same with any sink (INV-006).
/// </summary>
/// <param name="Qualifying">The qualifying result (T28).</param>
/// <param name="Tape">The race tape (T26).</param>
/// <param name="CarResults">One entry per starter, in finishing order.</param>
/// <param name="Classification">Classification and points by T27 over <see cref="CarResults"/>.</param>
/// <param name="PersonOutcomes">One entry per driver of every starter.</param>
/// <param name="TruthWeather">The true weather of the race (developer-only).</param>
/// <param name="LapRecords">Every lap of every car (developer-only).</param>
/// <param name="PitStops">Every stop, in the order they were made.</param>
/// <param name="Neutralisations">Every neutralisation, in the order they started.</param>
/// <param name="ScheduledLaps">Laps the race was scheduled for.</param>
/// <param name="LapsRun">Laps the leader ran (fewer than scheduled after a red flag that ended the race).</param>
public sealed record RaceWeekendResult(
    QualifyingResult Qualifying,
    RaceTape Tape,
    ImmutableArray<CarRaceResult> CarResults,
    RaceClassification Classification,
    ImmutableArray<PersonRaceOutcome> PersonOutcomes,
    TruthWeather TruthWeather,
    ImmutableArray<CarLapRecord> LapRecords,
    ImmutableArray<PitStopRecord> PitStops,
    ImmutableArray<NeutralisationRecord> Neutralisations,
    int ScheduledLaps,
    int LapsRun)
{
    /// <summary>
    /// What each pit wall knew about its own car at the start of every lap (#286). Derived from the same run as the laps, so it
    /// is not part of <see cref="Digest"/>; a read for a team keeps only its own cars (INV-003).
    /// </summary>
    public ImmutableArray<PitWallLap> PitWall { get; init; } = [];

    /// <summary>
    /// Lowercase hex SHA-256 of a canonical text of everything deterministic in the result (grid, tape, results, classification,
    /// outcomes, laps, stops, neutralisations, the true weather). Equal results give equal digests.
    /// </summary>
    public string Digest()
    {
        var text = new StringBuilder();
        text.Append("grid:").AppendJoin(',', Qualifying.Grid.Select(s => s.DriverId + "@" + R(s.ScoreSeconds))).Append('\n');
        text.Append("tape:").Append(Tape.ToCanonicalJson()).Append('\n');
        foreach (var car in CarResults)
        {
            text.Append("car:").Append(car.CarId).Append('|').Append(car.Status).Append('|').Append(car.LapsCompleted).Append('|')
                .Append(R(car.TotalSeconds)).Append('|').AppendJoin('+', car.DriversWhoDrove).Append('|').Append(car.Stops).Append('|')
                .AppendJoin('+', car.CompoundsUsed).Append('|').Append(car.FailedComponent).Append('|')
                .Append(car.SuddenFailure ? 1 : 0).Append('|').Append(car.SetFastestLap).Append('\n');
        }

        foreach (var car in Classification.Cars)
        {
            text.Append("cls:").Append(car.Position).Append('|').Append(car.IsClassified).Append('|').Append(car.ConstructorId).Append('|')
                .AppendJoin('+', car.DriverIds).Append('|').Append(car.CarPoints.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(car.ConstructorPoints.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        foreach (var person in PersonOutcomes)
        {
            text.Append("per:").Append(person.DriverId).Append('|').Append(person.LapsDriven).Append('|').Append(person.Retired).Append('|')
                .Append(person.Reason).Append('|').Append(person.Component).Append('|').Append(person.Lap).Append('|')
                .Append(person.Injury).Append('|').Append(person.Fatal).Append('\n');
        }

        foreach (var lap in LapRecords)
        {
            text.Append("lap:").Append(lap.CarId).Append('|').Append(lap.Lap).Append('|').Append(lap.DriverId).Append('|')
                .Append(R(lap.LapSeconds)).Append('|').Append(R(lap.EndSeconds)).Append('|').Append(lap.Position).Append('|')
                .Append(R(lap.NoiseSeconds)).Append('|').Append(lap.TyreCompound).Append('|').Append(lap.TyreAgeLaps).Append('|')
                .Append(R(lap.FuelKg)).Append('|').Append(lap.Neutralised).Append('|').Append(lap.Held).Append('\n');
        }

        foreach (var stop in PitStops)
        {
            text.Append("pit:").Append(stop.CarId).Append('|').Append(stop.Lap).Append('|').Append(R(stop.TotalSeconds)).Append('|')
                .Append(stop.ChangedTyres).Append('|').Append(stop.CompoundAfter).Append('|').Append(R(stop.RefuelKg)).Append('|')
                .Append(stop.DriverSwap).Append('|').Append(stop.IsRepair).Append('|').Append(stop.Fault).Append('\n');
        }

        foreach (var n in Neutralisations)
        {
            text.Append("neu:").Append(n.Kind).Append('|').Append(n.IncidentLap).Append('|').Append(n.LastLap).Append('|').Append(n.RaceResumed).Append('\n');
        }

        foreach (var sample in TruthWeather.Samples)
        {
            text.Append("wx:").Append(sample.Minute).Append('|').Append(R(sample.AirTempC)).Append('|').Append(R(sample.TrackTempC)).Append('|')
                .Append(R(sample.RainIntensity)).Append('|').Append(R(sample.TrackWetness)).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
    }

    private static string R(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
