using System.Collections.Immutable;
using Paddock.Domain.Racing;
using Paddock.Simulation.Racing;
using Paddock.Simulation.Racing.Incidents;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Racing.Qualifying;
using Paddock.Simulation.Racing.Weather;
using Paddock.Simulation.Racing.Weekend;

namespace Paddock.Application.Racing;

/// <summary>
/// What a spectator sees of the weather before the first lap: the state of the track and the air temperature. It is an
/// observation, not the truth of the simulation: the onset of rain, whether the race is showery and the forecast are
/// never part of it (INV-003).
/// </summary>
/// <param name="Track">The state of the racing line at the start.</param>
/// <param name="AirTempC">Air temperature at the start, in degrees Celsius.</param>
public sealed record RaceReportConditions(WetnessBand Track, double AirTempC);

/// <summary>
/// Everything the report builder reads, as plain values. <see cref="From"/> takes it from a
/// <see cref="RaceWeekendResult"/>; a test can build it by hand. The developer-only parts of the result (the true weather
/// and the lap records) are not here.
/// </summary>
public sealed record RaceReportInput(
    int Season,
    int Round,
    string TrackId,
    RaceReportConditions Conditions,
    QualifyingResult Qualifying,
    RaceTape Tape,
    ImmutableArray<CarRaceResult> CarResults,
    RaceClassification Classification,
    ImmutableArray<PersonRaceOutcome> PersonOutcomes,
    ImmutableArray<PitStopRecord> PitStops,
    ImmutableArray<NeutralisationRecord> Neutralisations,
    int ScheduledLaps,
    int LapsRun)
{
    /// <summary>
    /// Takes the input from a weekend result. The spectator conditions are the one place that touches the true weather, and
    /// only its first sample (the track and the air as they are at the start), nothing about what comes later.
    /// </summary>
    public static RaceReportInput From(RaceWeekendResult result, int season, int round, string trackId)
    {
        ArgumentNullException.ThrowIfNull(result);
        return From(RacePublishedFacts.From(result), season, round, trackId);
    }

    /// <summary>The same report input, taken from the facts the simulator publishes. Lap records are not among them.</summary>
    public static RaceReportInput From(RacePublishedFacts facts, int season, int round, string trackId)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
        var atStart = facts.TruthWeather.At(0);
        return new RaceReportInput(
            season,
            round,
            trackId,
            new RaceReportConditions(atStart.WetnessBand, atStart.AirTempC),
            facts.Qualifying,
            facts.Tape,
            facts.CarResults,
            facts.Classification,
            facts.PersonOutcomes,
            facts.PitStops,
            facts.Neutralisations,
            facts.ScheduledLaps,
            facts.LapsRun);
    }
}
