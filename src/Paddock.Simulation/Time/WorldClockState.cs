using Paddock.Domain.Random;
using Paddock.Domain.Time;

namespace Paddock.Simulation.Time;

/// <summary>
/// The day-tick state: the date being lived, the career seed, the queue, the next event id, and saved RNG streams.
/// </summary>
public readonly record struct WorldClockState
{
    private static readonly IReadOnlyDictionary<RngStreamSlot, RngState> NoStreams =
        new Dictionary<RngStreamSlot, RngState>().AsReadOnly();

    public WorldClockState(GameDate date, ulong masterSeed)
    {
        Date = date;
        MasterSeed = masterSeed;
        Queue = EventQueue.Empty;
        NextEventId = 1;
        RngStates = NoStreams;
    }

    public GameDate Date { get; init; }

    public ulong MasterSeed { get; init; }

    public EventQueue Queue { get; init; } = EventQueue.Empty;

    /// <summary>Next generated event id. Starts at 1 and only increases, so an id is never reused.</summary>
    public ulong NextEventId { get; init; } = 1;

    public IReadOnlyDictionary<RngStreamSlot, RngState> RngStates { get; init; } = NoStreams;

    public WorldClockState Enqueue(ScheduledEvent scheduled)
    {
        ArgumentNullException.ThrowIfNull(scheduled);
        return this with { Queue = Queue.Enqueue(scheduled) };
    }
}
