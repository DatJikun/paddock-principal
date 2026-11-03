using Paddock.Simulation.Racing.Practice;
using Paddock.Simulation.Racing.Qualifying;
using Paddock.Simulation.Racing.Weekend;

namespace Paddock.Simulation.Racing;

/// <summary>
/// The sessions of a weekend before the race (#316), whatever engine runs them (PP-052). The race itself is
/// <see cref="IRaceSimulator"/>. The weekend's steps read only these results and the race tape, never the engine's laps, so another
/// engine (<see cref="RaceEngineKind"/>) plugs in without changing the weekend. Both are pure: the same weekend gives the same
/// result, and running them changes nothing the race draws (INV-002, INV-004).
/// </summary>
public interface IWeekendSessions
{
    /// <summary>Practice: every car's times and each team's tyre reads.</summary>
    PracticeFacts Practice(RaceWeekendInput weekend);

    /// <summary>Qualifying: the grid the race of the same weekend starts from.</summary>
    QualifyingResult Qualifying(RaceWeekendInput weekend);
}

/// <summary>The lap engine's practice and qualifying (<see cref="RaceEngineKind.Lap"/>).</summary>
public sealed class LapWeekendSessions : IWeekendSessions
{
    public static LapWeekendSessions Instance { get; } = new();

    private LapWeekendSessions()
    {
    }

    public PracticeFacts Practice(RaceWeekendInput weekend)
    {
        ArgumentNullException.ThrowIfNull(weekend);
        return new WeekendRun(weekend, Paddock.Domain.Spy.NullSink.Instance, null).Practice();
    }

    public QualifyingResult Qualifying(RaceWeekendInput weekend)
    {
        ArgumentNullException.ThrowIfNull(weekend);
        return new WeekendRun(weekend, Paddock.Domain.Spy.NullSink.Instance, null).Qualify();
    }
}
