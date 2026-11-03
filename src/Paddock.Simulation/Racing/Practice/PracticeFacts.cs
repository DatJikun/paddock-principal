using System.Collections.Immutable;

namespace Paddock.Simulation.Racing.Practice;

/// <summary>
/// What a team learned about one dry compound in practice (#316): the laps a set lasts before it goes off, as a range. A better
/// strategist reads it more narrowly. It is the team's own knowledge, never shown for a rival (INV-003).
/// </summary>
public sealed record PracticeTyreRead(string CompoundId, int LowLaps, int HighLaps);

/// <summary>One car in practice: its best lap and long-run pace (public timing) and its tyre reads (the team's own).</summary>
/// <param name="CarId">The car, by its tape id (its first driver).</param>
/// <param name="ConstructorId">The team that entered the car.</param>
/// <param name="BestLapSeconds">The fastest lap on low fuel.</param>
/// <param name="LongRunSeconds">The average lap of a run on half the race fuel and one set of the middle compound.</param>
/// <param name="Tyres">The team's read of every dry race compound.</param>
public sealed record PracticeCar(
    string CarId,
    string ConstructorId,
    double BestLapSeconds,
    double LongRunSeconds,
    ImmutableArray<PracticeTyreRead> Tyres);

/// <summary>The practice of a weekend, fastest first. Pure output of an engine; nothing in the race reads it.</summary>
public sealed record PracticeFacts(ImmutableArray<PracticeCar> Cars);

/// <summary>Practice numbers of the lap engine. ESTIMATE: uncalibrated.</summary>
public static class PracticeConstants
{
    /// <summary>ESTIMATE: low-fuel laps a car sets in practice; the best one counts.</summary>
    public const int FastLaps = 5;

    /// <summary>ESTIMATE: laps of the long run on half the race fuel.</summary>
    public const int LongRunLaps = 10;

    /// <summary>ESTIMATE: the narrowest tyre read, in laps either side, even for the best strategist.</summary>
    public const double TyreReadMinLaps = 1;

    /// <summary>ESTIMATE: share of a set's life a strategist of skill 0 can be off by, either side.</summary>
    public const double TyreReadShare = 0.3;
}
