using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Persistence;

/// <summary>
/// One scheduled event as stored. Persistence sits beside Simulation, not above it, so it keeps the payload
/// as the type tag and text its owner wrote and never interprets it. <see cref="Sequence"/> is the queue's
/// insertion sequence.
/// </summary>
public sealed record StoredEvent(string Id, GameDate Date, long Sequence, string TypeId, string PayloadType, string Payload);

/// <summary>
/// One accepted command as stored, in the order it was executed. The body is opaque, like an event payload.
/// <see cref="IssuedOn"/> is the date the command was issued on.
/// </summary>
public sealed record StoredCommand(long SubmissionNumber, string ManagerId, DateOnly IssuedOn, string CommandType, string Payload);

/// <summary>
/// One registered manager. <see cref="Kind"/> is <c>Human</c> or <c>Ai</c>. Readiness is not stored: it resets
/// every day, and a save is only taken at a day boundary.
/// </summary>
public sealed record StoredManager(string Id, string Kind, string DisplayName, string? BlockingKind);

/// <summary>
/// Everything a save keeps about the world at one day boundary: the world itself, the day clock's queue,
/// the manager registry, and the command log, with the counters that make their ids and numbers unique.
/// </summary>
public sealed record WorldSnapshot
{
    public WorldSnapshot(
        WorldState world,
        IReadOnlyList<StoredEvent> events,
        long nextEventId,
        long nextEventSequence,
        IReadOnlyList<StoredManager> managers,
        IReadOnlyList<StoredCommand> commandLog,
        long nextSubmissionNumber)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(managers);
        ArgumentNullException.ThrowIfNull(commandLog);
        ArgumentOutOfRangeException.ThrowIfLessThan(nextEventId, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(nextEventSequence);
        ArgumentOutOfRangeException.ThrowIfLessThan(nextSubmissionNumber, 1);
        World = world;
        Events = events;
        NextEventId = nextEventId;
        NextEventSequence = nextEventSequence;
        Managers = managers;
        CommandLog = commandLog;
        NextSubmissionNumber = nextSubmissionNumber;
    }

    public WorldState World { get; }

    public IReadOnlyList<StoredEvent> Events { get; }

    /// <summary>Next generated event id. Starts at 1.</summary>
    public long NextEventId { get; }

    /// <summary>Next queue insertion sequence. Starts at 0.</summary>
    public long NextEventSequence { get; }

    public IReadOnlyList<StoredManager> Managers { get; }

    public IReadOnlyList<StoredCommand> CommandLog { get; }

    /// <summary>Next command submission number. Starts at 1.</summary>
    public long NextSubmissionNumber { get; }
}
