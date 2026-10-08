using System.Collections.Immutable;
using Paddock.Domain.Racing;
using Paddock.Simulation.Racing.Pits;

namespace Paddock.Application.Racing;

/// <summary>One pit wall order as the host took it: who gave it, when in the race, and the order the simulation reads.</summary>
public sealed record IssuedOrder(string ManagerId, long AtMs, PitWallOrder Order);

/// <summary>A race run with a list of orders, held until the host decides to show it.</summary>
/// <param name="Tape">The tape of the run.</param>
/// <param name="Commit">Hands the run to the watch and the round archive.</param>
public sealed record PreparedRace(RaceTape Tape, Action Commit);

/// <summary>Runs the same round again with other orders. The race day gives one to a race whose result is not booked yet.</summary>
public interface IRaceRerun
{
    /// <summary>The round raced with <paramref name="orders"/>; null when it cannot be raced. Nothing is published until <see cref="PreparedRace.Commit"/>.</summary>
    PreparedRace? Prepare(ImmutableArray<PitWallOrder> orders);
}

/// <summary>
/// The pit walls' hold on a race that is still being watched (#286). The orders are data with a lap number and the id of the
/// manager who gave them; the host re-runs the race with the whole list, from the same seed, so the same orders give the same
/// race (INV-002) and nothing already seen changes. Only a race with no booked result has one: today the quick race.
/// </summary>
public sealed class RaceSteering
{
    private readonly IRaceRerun _rerun;

    public RaceSteering(IRaceRerun rerun)
    {
        ArgumentNullException.ThrowIfNull(rerun);
        _rerun = rerun;
    }

    /// <summary>Every order taken so far, in the order the host took them.</summary>
    public ImmutableArray<IssuedOrder> Issued { get; private set; } = [];

    /// <summary>Grows with every order the race was re-run for, so a viewer knows its copy of the race is old.</summary>
    public int Revision { get; private set; }

    /// <summary>
    /// Re-runs the race with <paramref name="issued"/> and shows it when everything before <paramref name="nowMs"/> stayed as
    /// it was. False (and nothing changes) when the race cannot be run or the past would move.
    /// </summary>
    internal bool TryApply(ImmutableArray<IssuedOrder> issued, RaceTape current, long nowMs)
    {
        if (_rerun.Prepare([.. issued.Select(o => o.Order)]) is not { } run || !current.SameBefore(run.Tape, nowMs))
        {
            return false;
        }

        run.Commit();
        Issued = issued;
        Revision++;
        return true;
    }
}
