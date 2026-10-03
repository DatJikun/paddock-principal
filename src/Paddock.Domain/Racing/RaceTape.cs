using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Paddock.Domain.Racing;

/// <summary>
/// The ordered, append-only record of one race. A tape can only be built through
/// <see cref="Builder"/> (or <see cref="From"/>), which enforces the integrity rules, so every
/// existing <see cref="RaceTape"/> is valid and immutable.
/// Rules: the first event is <see cref="RaceStarted"/> with Seq 0; Seq is strictly increasing;
/// RaceTime never decreases and is not negative; Lap is within 0..TotalLaps; every driver id is a
/// starter; a driver has no events after Retirement or Finished (an <see cref="Incident"/> may still
/// name a retired car); nothing follows RaceEnded.
/// </summary>
public sealed class RaceTape
{
    private RaceTape(ImmutableArray<RaceEvent> events)
    {
        Events = events;
    }

    public static RaceTape Empty { get; } = new([]);

    public ImmutableArray<RaceEvent> Events { get; }

    public int Count => Events.Length;

    /// <summary>Lowercase hex SHA-256 of the UTF-8 canonical JSON.</summary>
    public string Hash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ToCanonicalJson()))).ToLowerInvariant();

    public static RaceTape From(IEnumerable<RaceEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var builder = new Builder();
        foreach (var raceEvent in events)
        {
            builder.Append(raceEvent);
        }

        return builder.Build();
    }

    /// <summary>Canonical JSON: fixed property order, no whitespace, invariant numbers. Equal tapes give identical text.</summary>
    public string ToCanonicalJson() => RaceTapeJson.Write(Events);

    public sealed class Builder
    {
        private readonly List<RaceEvent> _events = [];
        private readonly HashSet<string> _drivers = new(StringComparer.Ordinal);
        private readonly HashSet<string> _done = new(StringComparer.Ordinal);
        private int _totalLaps;
        private bool _ended;

        public int Count => _events.Count;

        public Builder Append(RaceEvent raceEvent)
        {
            ArgumentNullException.ThrowIfNull(raceEvent);
            Validate(raceEvent);
            _events.Add(raceEvent);
            Track(raceEvent);
            return this;
        }

        public RaceTape Build() => new([.. _events]);

        private static IEnumerable<string> DriversOf(RaceEvent e) => e switch
        {
            LapCompleted x => [x.DriverId],
            PitStop x => [x.DriverId],
            PositionChange x => [x.DriverId],
            Retirement x => [x.DriverId],
            FastestLap x => [x.DriverId],
            Finished x => [x.DriverId],
            Incident x => x.InvolvedIds.IsDefault ? [] : x.InvolvedIds,
            _ => [],
        };

        private static RaceTapeException Fail(RaceEvent e, string message) =>
            new($"Invalid {e.Kind} at Seq {e.Seq}: {message}");

        private void Validate(RaceEvent e)
        {
            if (_ended)
            {
                throw Fail(e, "no events are allowed after RaceEnded.");
            }

            if (_events.Count == 0)
            {
                if (e is not RaceStarted)
                {
                    throw Fail(e, "the first event must be RaceStarted.");
                }

                if (e.Seq != 0)
                {
                    throw Fail(e, "the first event must have Seq 0.");
                }
            }
            else
            {
                var previous = _events[^1];
                if (e is RaceStarted)
                {
                    throw Fail(e, "RaceStarted may only be the first event.");
                }

                if (e.Seq <= previous.Seq)
                {
                    throw Fail(e, $"Seq must be strictly increasing (previous {previous.Seq}).");
                }

                if (e.RaceTime < previous.RaceTime)
                {
                    throw Fail(e, $"RaceTime must not decrease (previous {previous.RaceTime}).");
                }
            }

            if (e.RaceTime < 0)
            {
                throw Fail(e, "RaceTime must not be negative.");
            }

            if (e is RaceStarted started)
            {
                if (started.TotalLaps <= 0)
                {
                    throw Fail(e, "TotalLaps must be positive.");
                }

                if (started.Lap != 0 || started.RaceTime != 0)
                {
                    throw Fail(e, "RaceStarted must be at lap 0 and race time 0.");
                }

                if (started.DriverIds.IsDefaultOrEmpty || started.DriverIds.Distinct(StringComparer.Ordinal).Count() != started.DriverIds.Length)
                {
                    throw Fail(e, "DriverIds must be non-empty and unique.");
                }
            }
            else if (e.Lap < 0 || e.Lap > _totalLaps)
            {
                throw Fail(e, $"Lap must be within 0..{_totalLaps}.");
            }

            foreach (var driver in DriversOf(e))
            {
                if (!_drivers.Contains(driver))
                {
                    throw Fail(e, $"unknown driver '{driver}'.");
                }

                if (e is not Incident && _done.Contains(driver))
                {
                    throw Fail(e, $"driver '{driver}' already retired or finished.");
                }
            }
        }

        private void Track(RaceEvent e)
        {
            switch (e)
            {
                case RaceStarted started:
                    _totalLaps = started.TotalLaps;
                    foreach (var id in started.DriverIds)
                    {
                        _drivers.Add(id);
                    }

                    break;
                case Retirement retirement:
                    _done.Add(retirement.DriverId);
                    break;
                case Finished finished:
                    _done.Add(finished.DriverId);
                    break;
                case RaceEnded:
                    _ended = true;
                    break;
            }
        }
    }
}
