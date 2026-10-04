using Paddock.Domain.Random;
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

/// <summary>One per-season summary line of a run, as stored in <c>career_years</c>.</summary>
public sealed record StoredYear(int Year, int Alive, int Retired, int Pool, int Contracts, string StateHash);

/// <summary>
/// What a day-by-day run keeps beside <see cref="WorldState"/> (V007): the opening year, the tallies, the per-season
/// summaries, and the placeholder talent pool as person ids. None of it is part of the world hash.
/// </summary>
public sealed record CareerRunState(
    int OpenedYear,
    int ContractExpiries,
    int Intakes,
    IReadOnlyList<string> Pool,
    IReadOnlyList<StoredYear> Years);

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

    /// <summary>
    /// The run state that sits beside the world. Null for a save that has none (written by <see cref="WorldRepository.SaveWorld"/>,
    /// or before V007). <see cref="WorldRepository.SaveAll"/> replaces it, so null clears what an earlier save left.
    /// </summary>
    public CareerRunState? Run { get; init; }

    /// <summary>
    /// The state of every <see cref="RngStreamName"/> stream for the season of the save date (<c>meta.rng_states</c>), or null
    /// when the save has none. <see cref="WorldRepository.SaveAll"/> replaces it, so null clears it.
    /// </summary>
    public IReadOnlyDictionary<string, RngState>? RngStates { get; init; }
}
