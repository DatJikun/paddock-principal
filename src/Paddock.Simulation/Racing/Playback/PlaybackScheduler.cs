using Paddock.Domain.Racing;

namespace Paddock.Simulation.Racing.Playback;

/// <summary>
/// Replays one <see cref="RaceTape"/> at watching speed. Each viewer (host, guests, PP-045) gets an
/// independent <see cref="PlaybackCursor"/>; cursors share only the immutable tape and the clock, so
/// playback can never change the tape (INV-001) and two cursors driven by the same clock readings
/// deliver identical events.
/// </summary>
public sealed class PlaybackScheduler
{
    private readonly IClock _clock;

    public PlaybackScheduler(RaceTape tape, IClock clock, double speed = 1.0)
    {
        Tape = tape ?? throw new ArgumentNullException(nameof(tape));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        PlaybackCursor.ValidateSpeed(speed);
        DefaultSpeed = speed;
    }

    public RaceTape Tape { get; }

    public double DefaultSpeed { get; }

    /// <summary>A new cursor at race time 0, playing at the scheduler's default speed.</summary>
    public PlaybackCursor CreateCursor() => new(Tape, _clock, DefaultSpeed);
}

/// <summary>
/// One viewer's position in a tape. Race time advances by (clock delta x speed) while playing.
/// <see cref="Poll"/> returns, in tape order, the events that became due since the previous call.
/// </summary>
public sealed class PlaybackCursor
{
    private readonly RaceTape _tape;
    private readonly IClock _clock;
    private double _raceTimeMs;
    private long _lastNowMs;
    private int _next;

    internal PlaybackCursor(RaceTape tape, IClock clock, double speed)
    {
        _tape = tape;
        _clock = clock;
        Speed = speed;
        _lastNowMs = clock.NowMs;
    }

    public double Speed { get; private set; }

    public bool IsPaused { get; private set; }

    /// <summary>True once every event of the tape has been delivered.</summary>
    public bool IsFinished => _next >= _tape.Count;

    /// <summary>Current race time in whole milliseconds (as of the last Poll or control call).</summary>
    public long RaceTimeMs => (long)_raceTimeMs;

    /// <summary>Number of events already delivered (or skipped by a seek).</summary>
    public int Position => _next;

    internal static void ValidateSpeed(double speed)
    {
        if (double.IsNaN(speed) || double.IsInfinity(speed) || speed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speed), "Speed must be a finite number greater than zero.");
        }
    }

    public IReadOnlyList<RaceEvent> Poll()
    {
        Advance();
        var due = new List<RaceEvent>();
        while (_next < _tape.Count && _tape.Events[_next].RaceTime <= _raceTimeMs)
        {
            due.Add(_tape.Events[_next++]);
        }

        return due;
    }

    public void Pause()
    {
        Advance();
        IsPaused = true;
    }

    public void Resume()
    {
        if (IsPaused)
        {
            IsPaused = false;
            _lastNowMs = _clock.NowMs;
        }
    }

    public void SetSpeed(double speed)
    {
        ValidateSpeed(speed);
        Advance();
        Speed = speed;
    }

    /// <summary>
    /// Jumps to the first event whose lap is at least <paramref name="lap"/> (lap 0 or less = start).
    /// Events before that point are skipped, not delivered; seeking backwards makes them due again.
    /// Past the last lap the cursor lands at the end of the tape.
    /// </summary>
    public void SeekToLap(int lap)
    {
        Advance();
        var events = _tape.Events;
        var target = -1;
        for (var i = 0; i < events.Length; i++)
        {
            if (events[i].Lap >= lap)
            {
                target = i;
                break;
            }
        }

        if (target < 0)
        {
            _next = events.Length;
            _raceTimeMs = events.Length == 0 ? 0 : events[^1].RaceTime;
            return;
        }

        var time = events[target].RaceTime;
        _raceTimeMs = time;
        _next = target;
        while (_next > 0 && events[_next - 1].RaceTime >= time)
        {
            _next--;
        }
    }

    private void Advance()
    {
        var now = _clock.NowMs;
        if (!IsPaused && now > _lastNowMs)
        {
            _raceTimeMs += (now - _lastNowMs) * Speed;
        }

        _lastNowMs = now;
    }
}
