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
/// race (INV-002) and nothing already seen changes. Every race that is watched before it is booked has one: the quick race and the
/// career's race of today (#316).
/// </summary>
public sealed class RaceSteering
{
    private readonly IRaceRerun _rerun;

    /// <summary>A steering with no orders yet, or with the orders a career already holds for the race (#316).</summary>
    public RaceSteering(IRaceRerun rerun, ImmutableArray<IssuedOrder> issued = default)
    {
        ArgumentNullException.ThrowIfNull(rerun);
        _rerun = rerun;
        Issued = issued.IsDefault ? [] : issued;
    }

    /// <summary>Every order taken so far, in the order the host took them.</summary>
    public ImmutableArray<IssuedOrder> Issued { get; private set; }

    /// <summary>
    /// Where a career race keeps its orders (#316): called with the new list and its tape before the race is shown, it submits
    /// the world command and returns its refusal key, or null. A quick race books nothing and has none.
    /// </summary>
    public Func<ImmutableArray<IssuedOrder>, RaceTape, string?>? Booking { get; set; }

    /// <summary>Grows with every order the race was re-run for, so a viewer knows its copy of the race is old.</summary>
    public int Revision { get; private set; }

    /// <summary>
    /// Re-runs the race with <paramref name="issued"/> and shows it when everything before <paramref name="nowMs"/> stayed as
    /// it was. False (and nothing changes) when the race cannot be run or the past would move.
    /// </summary>
    internal bool TryApply(ImmutableArray<IssuedOrder> issued, RaceTape current, long nowMs)
    {
        return TryApply(issued, current, nowMs, out _);
    }

    /// <summary>As <see cref="TryApply(ImmutableArray{IssuedOrder}, RaceTape, long)"/>; <paramref name="refusal"/> is the booking's refusal key.</summary>
    internal bool TryApply(ImmutableArray<IssuedOrder> issued, RaceTape current, long nowMs, out string? refusal)
    {
        refusal = null;
        if (_rerun.Prepare([.. issued.Select(o => o.Order)]) is not { } run || !current.SameBefore(run.Tape, nowMs))
        {
            return false;
        }

        if (Booking?.Invoke(issued, run.Tape) is { } refused)
        {
            refusal = refused;
            return false;
        }

        run.Commit();
        Issued = issued;
        Revision++;
        return true;
    }
}
