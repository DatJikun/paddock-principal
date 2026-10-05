using Paddock.Domain.Racing;
using Paddock.Simulation.Racing.Playback;

namespace Paddock.SimRunner;

/// <summary>
/// Replays a tape through <see cref="PlaybackScheduler"/>. The clock steps to each next event, so the lines are
/// deterministic and do not wait on the wall clock. <paramref name="speed"/> is the scheduler speed: a higher speed
/// needs less clock time to reach the same race time.
/// </summary>
public static class RacePlayback
{
    public static void Play(RaceTape tape, double speed, Action<RaceEvent> onEvent)
    {
        ArgumentNullException.ThrowIfNull(tape);
        ArgumentNullException.ThrowIfNull(onEvent);
        var clock = new StepClock();
        var cursor = new PlaybackScheduler(tape, clock, speed).CreateCursor();
        while (!cursor.IsFinished)
        {
            foreach (var raceEvent in cursor.Poll())
            {
                onEvent(raceEvent);
            }

            if (cursor.IsFinished || cursor.Position >= tape.Count)
            {
                break;
            }

            var gap = tape.Events[cursor.Position].RaceTime - cursor.RaceTimeMs;
            if (gap < 1)
            {
                gap = 1;
            }

            clock.Advance((long)Math.Ceiling(gap / speed));
        }
    }

    /// <summary>How far the stepping clock moved to deliver <paramref name="tape"/> at <paramref name="speed"/>.</summary>
    public static long ClockMs(RaceTape tape, double speed)
    {
        var clock = new StepClock();
        var cursor = new PlaybackScheduler(tape, clock, speed).CreateCursor();
        while (!cursor.IsFinished)
        {
            _ = cursor.Poll();
            if (cursor.IsFinished || cursor.Position >= tape.Count)
            {
                break;
            }

            var gap = tape.Events[cursor.Position].RaceTime - cursor.RaceTimeMs;
            if (gap < 1)
            {
                gap = 1;
            }

            clock.Advance((long)Math.Ceiling(gap / speed));
        }

        return clock.NowMs;
    }

    private sealed class StepClock : IClock
    {
        public long NowMs { get; private set; }

        public void Advance(long milliseconds) => NowMs += milliseconds;
    }
}
