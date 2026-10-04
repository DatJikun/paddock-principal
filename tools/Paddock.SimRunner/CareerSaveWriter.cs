using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Managers;
using Paddock.Domain.Career;
using Paddock.Domain.Random;
using Paddock.Persistence;
using Paddock.Simulation.Career;
using Paddock.Simulation.Time;

namespace Paddock.SimRunner;

/// <summary>
/// Writes a career at a day boundary through T19 so that <see cref="CareerSaveReader"/> can resume it to the same future
/// (issue #123): the world, the day-clock queue and counters, the managers, the command log, the RNG stream states, the
/// talent pool and the run's tallies, all in one transaction. SimRunner is the composition root that sees both the owners'
/// codecs (events in Simulation, commands and managers in Application) and the opaque rows of Persistence, so the mapping
/// between them lives here and applies no game rules.
/// Emitted day events are not kept (the queue holds only the future). Retirement is part of the world
/// (<c>persons.retired_on</c>). The talent pool is not a field of <see cref="Paddock.Domain.World.WorldState"/>; it is saved
/// in the <c>talent_pool</c> table beside it.
/// </summary>
public static class CareerSaveWriter
{
    public static void Write(
        string path,
        CareerSession session,
        CareerConfig config,
        string playerTeamId,
        string worldDataHash,
        string careerName,
        CareerHostState? host = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerTeamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldDataHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(careerName);

        var clock = session.Clock;
        var events = new StoredEvent[clock.Queue.Count];
        for (var i = 0; i < events.Length; i++)
        {
            events[i] = ToStored(clock.Queue.Events[i]);
        }

        host ??= FreshHost();
        var managers = new List<StoredManager>();
        foreach (var manager in host.Managers.All)
        {
            managers.Add(new StoredManager(
                manager.Id.Value,
                ManagerCodec.EncodeKind(manager.Kind),
                manager.DisplayName,
                manager.BlockingItem?.Kind));
        }

        var log = new List<StoredCommand>(host.Log.Entries.Count);
        foreach (var command in host.Log.Entries)
        {
            var body = CommandCodec.Production.Encode(command);
            log.Add(new StoredCommand(command.SubmissionNumber, command.ManagerId.Value, command.IssuedOn, body.Tag, body.Text));
        }

        var years = new List<StoredYear>(session.Years.Count);
        foreach (var year in session.Years)
        {
            years.Add(new StoredYear(year.Year, year.Alive, year.Retired, year.Pool, year.Contracts, year.StateHash));
        }

        var snapshot = new WorldSnapshot(
            session.World,
            events,
            checked((long)clock.NextEventId),
            checked((long)clock.Queue.NextSequence),
            managers,
            log,
            host.NextSubmissionNumber)
        {
            Run = new CareerRunState(
                session.OpenedYear,
                session.ContractExpiries,
                session.Intakes,
                session.TalentPool.Select(id => id.Value).ToArray(),
                years),
            RngStates = RngStatesOf(clock),
        };
        var meta = new SaveMeta(
            careerName,
            "AI",
            playerTeamId,
            new DateOnly(session.Date.Year, session.Date.Month, session.Date.Day),
            worldDataHash,
            clock.MasterSeed,
            config);
        using var save = SaveFile.Create(path, meta);
        new WorldRepository(save).SaveAll(snapshot, session.Date);
    }

    /// <summary>
    /// The state of every named stream for the season of the clock's date. A stream the days have not touched yet is the one
    /// the next draw would derive from the master seed, which is exactly what a resumed clock derives for a stream it does not
    /// have, so writing it is the same as leaving it out. Earlier seasons are not kept: a season derives its own streams.
    /// </summary>
    internal static IReadOnlyDictionary<string, RngState> RngStatesOf(WorldClockState clock)
    {
        var season = clock.Date.Year;
        var states = new Dictionary<string, RngState>(RngStreamName.All.Count, StringComparer.Ordinal);
        foreach (var name in RngStreamName.All)
        {
            states[name] = clock.RngStates.TryGetValue(new RngStreamSlot(name, season), out var saved)
                ? saved
                : RngStreams.Derive(clock.MasterSeed, name, season).State;
        }

        return states;
    }

    private static CareerHostState FreshHost()
    {
        var managers = new ManagerRegistry();
        managers.Register(new ManagerId(CareerHost.AiManagerId), ManagerKind.Ai, "AI");
        return new CareerHostState(managers, new CommandLog(), 1);
    }

    private static StoredEvent ToStored(ScheduledEvent scheduled)
    {
        var payload = EventPayloadCodec.Encode(scheduled.Payload);
        return new StoredEvent(
            scheduled.Id.Value,
            scheduled.Date,
            checked((long)scheduled.Sequence),
            scheduled.TypeId,
            payload.Tag,
            payload.Text);
    }
}
