using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Managers;
using Paddock.Domain.Pool;
using Paddock.Domain.Random;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Simulation.Career;
using Paddock.Simulation.Time;

namespace Paddock.SimRunner;

/// <summary>
/// A save that has no run state (written before V007 or by a world-only save) or whose RNG states are missing.
/// Its world loads, but the future it would play is not the future that was saved, so resuming is refused.
/// </summary>
public sealed class SaveNotResumableException : InvalidOperationException
{
    public SaveNotResumableException(string message)
        : base(message)
    {
    }
}

/// <summary>A career read back from a save: the header, the session state, and the host state.</summary>
public sealed record LoadedCareer(SaveMeta Meta, CareerSessionResume Session, CareerHostState Host);

/// <summary>
/// Reads what <see cref="CareerSaveWriter"/> wrote and rebuilds the owners' types through their codecs. An unknown event
/// payload, command or manager tag stops the load (<see cref="Paddock.Domain.Codec.UnknownTagException"/>).
/// </summary>
public static class CareerSaveReader
{
    public static LoadedCareer Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var save = SaveFile.Open(path);
        var meta = save.ReadMeta();
        var repository = new WorldRepository(save);
        if (!repository.HasWorld)
        {
            throw new SaveNotResumableException("This save has no world, so there is nothing to resume.");
        }

        var snapshot = repository.LoadAll();
        if (snapshot.Run is null)
        {
            throw new SaveNotResumableException(
                "This save has no run state (it was written before the run state was saved), so it cannot be resumed exactly.");
        }

        if (snapshot.RngStates is null)
        {
            throw new SaveNotResumableException("This save has no RNG stream states, so it cannot be resumed exactly.");
        }

        var date = snapshot.World.CurrentDate;
        var queue = new List<ScheduledEvent>(snapshot.Events.Count);
        foreach (var stored in snapshot.Events)
        {
            var payload = EventPayloadCodec.Decode(stored.PayloadType, stored.Payload);
            queue.Add(new ScheduledEvent(new EventId(stored.Id), stored.Date, stored.TypeId, payload)
            {
                Sequence = checked((ulong)stored.Sequence),
            });
        }

        // A stream still in the state its first draw would derive has not been touched this season, and the clock treats a missing
        // slot exactly like that, so it stays missing: the resumed clock is then the same value as one that never stopped.
        var streams = new Dictionary<RngStreamSlot, RngState>(snapshot.RngStates.Count);
        foreach (var pair in snapshot.RngStates)
        {
            if (pair.Value != RngStreams.Derive(meta.MasterSeed, pair.Key, date.Year).State)
            {
                streams.Add(new RngStreamSlot(pair.Key, date.Year), pair.Value);
            }
        }

        var clock = new WorldClockState(date, meta.MasterSeed)
        {
            Queue = EventQueue.Restore(queue, checked((ulong)snapshot.NextEventSequence)),
            NextEventId = checked((ulong)snapshot.NextEventId),
            RngStates = streams,
        };

        if (snapshot.World.Section(TalentPoolSection.SectionName) is null)
        {
            throw new SaveNotResumableException(
                "This save has no talent pool section (it was written before the talent pool was a world section), so it cannot be resumed exactly.");
        }

        var years = new List<CareerYearSummary>(snapshot.Run.Years.Count);
        foreach (var year in snapshot.Run.Years)
        {
            years.Add(new CareerYearSummary(year.Year, year.Alive, year.Retired, year.Pool, year.Contracts, year.StateHash));
        }

        var session = new CareerSessionResume(
            snapshot.World,
            clock,
            snapshot.Run.OpenedYear,
            snapshot.Run.ContractExpiries,
            snapshot.Run.Intakes,
            years);

        var managers = ManagerCodec.Restore(snapshot.Managers.Select(manager =>
            (manager.Id, manager.Kind, manager.DisplayName, manager.BlockingKind)));
        var log = new CommandLog();
        foreach (var stored in snapshot.CommandLog)
        {
            log.Append(CommandCodec.Production.Decode(
                stored.CommandType,
                stored.Payload,
                new ManagerId(stored.ManagerId),
                stored.SubmissionNumber,
                stored.IssuedOn));
        }

        return new LoadedCareer(meta, session, new CareerHostState(managers, log, snapshot.NextSubmissionNumber));
    }
}
