using System.Collections.Immutable;
using Paddock.Domain.Racing;
using Paddock.Simulation.Racing.Incidents;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Racing.Qualifying;
using Paddock.Simulation.Racing.Weather;
using Paddock.Simulation.Racing.Weekend;

namespace Paddock.Simulation.Racing;

/// <summary>
/// What a caller may read after a lap-engine race, besides the tape the interface returns.
/// Lap records and <c>WeekendRun</c> state are not here (PP-052): the race report and the developer spy section
/// already used these facts, and they are not the engine's private lap table.
/// </summary>
public sealed record RacePublishedFacts(
    QualifyingResult Qualifying,
    RaceTape Tape,
    ImmutableArray<CarRaceResult> CarResults,
    RaceClassification Classification,
    ImmutableArray<PersonRaceOutcome> PersonOutcomes,
    TruthWeather TruthWeather,
    ImmutableArray<PitStopRecord> PitStops,
    ImmutableArray<NeutralisationRecord> Neutralisations,
    int ScheduledLaps,
    int LapsRun)
{
    public static RacePublishedFacts From(RaceWeekendResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new RacePublishedFacts(
            result.Qualifying,
            result.Tape,
            result.CarResults,
            result.Classification,
            result.PersonOutcomes,
            result.TruthWeather,
            result.PitStops,
            result.Neutralisations,
            result.ScheduledLaps,
            result.LapsRun);
    }
}
