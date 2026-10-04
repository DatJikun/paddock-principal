using Paddock.Domain.Time;

namespace Paddock.Simulation.Time;

/// <summary>
/// Events ordered by date, then by insertion sequence.
/// The sequence is a counter on the queue: the first event inserted is 0, each later insert is one higher, and dequeue does not reuse a number.
/// That tiebreak is deterministic and does not depend on hash codes or the wall clock.
/// </summary>
public sealed class EventQueue
{
    public static EventQueue Empty { get; } = new([], 0);

    private readonly ScheduledEvent[] _ordered;

    private EventQueue(ScheduledEvent[] ordered, ulong nextSequence)
    {
        _ordered = ordered;
        NextSequence = nextSequence;
    }

    /// <summary>
    /// Rebuilds a saved queue exactly: each event keeps its own <see cref="ScheduledEvent.Sequence"/> and the next insert
    /// gets <paramref name="nextSequence"/>, so the order of events that are scheduled after a load is the same as in a
    /// run that never stopped. Ids and sequences must be unique and every sequence must be below the counter.
    /// </summary>
    public static EventQueue Restore(IReadOnlyList<ScheduledEvent> events, ulong nextSequence)
    {
        ArgumentNullException.ThrowIfNull(events);
        var ids = new HashSet<string>(events.Count, StringComparer.Ordinal);
        var sequences = new HashSet<ulong>(events.Count);
        var ordered = new ScheduledEvent[events.Count];
        for (var i = 0; i < ordered.Length; i++)
        {
            var scheduled = events[i] ?? throw new ArgumentException("The saved queue lists a null event.", nameof(events));
            if (scheduled.Sequence >= nextSequence)
            {
                throw new ArgumentException(
                    $"Event '{scheduled.Id.Value}' has sequence {scheduled.Sequence}, which is not below the queue counter {nextSequence}.",
                    nameof(events));
            }

            if (!ids.Add(scheduled.Id.Value))
            {
                throw new ArgumentException($"Event id '{scheduled.Id.Value}' is saved twice.", nameof(events));
            }

            if (!sequences.Add(scheduled.Sequence))
            {
                throw new ArgumentException($"Sequence {scheduled.Sequence} is saved twice.", nameof(events));
            }

            ordered[i] = scheduled;
        }

        Array.Sort(ordered, static (left, right) =>
        {
            var date = left.Date.CompareTo(right.Date);
            return date != 0 ? date : left.Sequence.CompareTo(right.Sequence);
        });
        return new EventQueue(ordered, nextSequence);
    }

    public int Count => _ordered.Length;

    public ulong NextSequence { get; }

    public IReadOnlyList<ScheduledEvent> Events => _ordered;

    public EventQueue Enqueue(ScheduledEvent scheduled)
    {
        ArgumentNullException.ThrowIfNull(scheduled);
        return EnqueueRange([scheduled]);
    }

    public EventQueue EnqueueRange(IReadOnlyList<ScheduledEvent> scheduled)
    {
        ArgumentNullException.ThrowIfNull(scheduled);
        if (scheduled.Count == 0)
        {
            return this;
        }

        var seen = new HashSet<string>(_ordered.Length + scheduled.Count, StringComparer.Ordinal);
        foreach (var existing in _ordered)
        {
            seen.Add(existing.Id.Value);
        }

        var combined = new List<ScheduledEvent>(_ordered.Length + scheduled.Count);
        combined.AddRange(_ordered);
        var sequence = NextSequence;
        foreach (var draft in scheduled)
        {
            ArgumentNullException.ThrowIfNull(draft);
            if (!seen.Add(draft.Id.Value))
            {
                throw new InvalidOperationException($"Event id '{draft.Id.Value}' is already queued.");
            }

            combined.Add(draft with { Sequence = sequence });
            sequence = checked(sequence + 1);
        }

        combined.Sort(static (left, right) =>
        {
            var date = left.Date.CompareTo(right.Date);
            return date != 0 ? date : left.Sequence.CompareTo(right.Sequence);
        });

        return new EventQueue(combined.ToArray(), sequence);
    }

    /// <summary>
    /// Removes events dated exactly <paramref name="today"/>, already in insertion order.
    /// An empty day, or a day before the next event, returns this same queue.
    /// </summary>
    public DueEvents TakeDue(GameDate today)
    {
        if (_ordered.Length == 0 || _ordered[0].Date > today)
        {
            return new DueEvents(this, []);
        }

        if (_ordered[0].Date < today)
        {
            throw new InvalidOperationException(
                $"Event '{_ordered[0].Id.Value}' is dated {_ordered[0].Date} which is before {today}.");
        }

        var count = 1;
        while (count < _ordered.Length && _ordered[count].Date == today)
        {
            count++;
        }

        var due = new ScheduledEvent[count];
        Array.Copy(_ordered, due, count);
        var rest = new ScheduledEvent[_ordered.Length - count];
        Array.Copy(_ordered, count, rest, 0, rest.Length);
        return new DueEvents(new EventQueue(rest, NextSequence), due);
    }
}

public readonly record struct DueEvents(EventQueue Queue, IReadOnlyList<ScheduledEvent> Due);
