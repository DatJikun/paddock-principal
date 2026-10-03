using Paddock.Domain.Random;

namespace Paddock.Simulation.Time;

/// <summary>
/// Lives one game day. Due events are emitted, registered handlers run, then the date moves forward one day.
/// A day with nothing due and no handlers only replaces the date and keeps the same queue.
/// This does not read the wall clock or an unseeded generator.
/// The multiplayer readiness gate (TECH §5.1) decides when the host calls this. It is not part of the tick.
/// </summary>
public static class WorldClock
{
    /// <summary>
    /// CI ceiling for living 365 days with an empty queue and no handlers.
    /// The fast path only moves the date. Measured work stays far below this.
    /// </summary>
    public const int EmptyYearMillisecondBudget = 500;

    public static DayAdvance AdvanceDay(WorldClockState state) =>
        AdvanceDay(state, DayHandlerRegistry.Empty);

    public static DayAdvance AdvanceDay(WorldClockState state, DayHandlerRegistry handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(state.Queue);
        ArgumentNullException.ThrowIfNull(state.RngStates);

        var today = state.Date;
        var taken = state.Queue.TakeDue(today);
        var tomorrow = today.AddDays(1);

        if (taken.Due.Count == 0 && handlers.Handlers.Count == 0)
        {
            return new DayAdvance(state with { Date = tomorrow }, DayAdvance.None);
        }

        var emitted = new List<DomainEvent>(taken.Due.Count);
        foreach (var due in taken.Due)
        {
            emitted.Add(new DomainEvent(due.Id, due.Date, due.TypeId, due.Payload));
        }

        var queue = taken.Queue;
        var nextId = state.NextEventId;
        var rngStates = state.RngStates;

        if (handlers.Handlers.Count > 0)
        {
            var context = new DayContext(state.MasterSeed, today, taken.Due, state.RngStates, nextId);
            foreach (var handler in handlers.Handlers)
            {
                handler.OnDay(context);
            }

            if (context.Scheduled.Count > 0)
            {
                queue = queue.EnqueueRange(context.Scheduled);
            }

            if (context.Emitted.Count > 0)
            {
                emitted.AddRange(context.Emitted);
            }

            nextId = context.NextEventId;
            if (context.TouchedRng)
            {
                rngStates = CopyRng(state.RngStates, context.Streams);
            }
        }

        IReadOnlyList<DomainEvent> events = emitted.Count == 0 ? DayAdvance.None : emitted.ToArray();
        var next = state with
        {
            Date = tomorrow,
            Queue = queue,
            NextEventId = nextId,
            RngStates = rngStates,
        };
        return new DayAdvance(next, events);
    }

    private static Dictionary<RngStreamSlot, RngState> CopyRng(
        IReadOnlyDictionary<RngStreamSlot, RngState> saved,
        IReadOnlyDictionary<RngStreamSlot, Xoshiro256StarStar> touched)
    {
        var copy = new Dictionary<RngStreamSlot, RngState>(saved.Count + touched.Count);
        foreach (var pair in saved)
        {
            copy[pair.Key] = pair.Value;
        }

        foreach (var pair in touched)
        {
            copy[pair.Key] = pair.Value.State;
        }

        return copy;
    }
}
