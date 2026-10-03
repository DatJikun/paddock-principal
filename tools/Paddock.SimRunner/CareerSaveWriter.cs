using Paddock.Application.Career;
using Paddock.Domain.Career;
using Paddock.Domain.Time;
using Paddock.Persistence;
using Paddock.Simulation.Career;
using Paddock.Simulation.Time;

namespace Paddock.SimRunner;

/// <summary>
/// Writes the world, the day-clock queue, and the AI manager through T19.
/// Emitted day events are not kept (the queue holds only the future). Retirement and the talent pool
/// are not fields of <see cref="Paddock.Domain.World.WorldState"/>, so a loaded save does not restore them.
/// RNG stream states are not in the V003 snapshot either.
/// </summary>
public static class CareerSaveWriter
{
    public static void Write(
        string path,
        CareerSession session,
        CareerConfig config,
        string playerTeamId,
        string worldDataHash,
        string careerName)
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

        var managers = new[]
        {
            new StoredManager(CareerHost.AiManagerId, "Ai", "AI", null),
        };
        var snapshot = new WorldSnapshot(
            session.World,
            events,
            checked((long)clock.NextEventId),
            checked((long)clock.Queue.NextSequence),
            managers,
            [],
            1);
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

    private static StoredEvent ToStored(ScheduledEvent scheduled)
    {
        var (payloadType, payload) = scheduled.Payload switch
        {
            RaceSessionPayload race => ("race-session", Invariant(race.Season) + ";" + Invariant(race.Round) + ";" + race.LayoutId),
            MarkerPayload marker => ("marker", marker.Marker),
            _ => throw new InvalidOperationException("Cannot store a payload of type " + scheduled.Payload.GetType().Name + "."),
        };
        return new StoredEvent(
            scheduled.Id.Value,
            scheduled.Date,
            checked((long)scheduled.Sequence),
            scheduled.TypeId,
            payloadType,
            payload);
    }

    private static string Invariant(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
