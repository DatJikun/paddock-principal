using System.Diagnostics;
using System.Globalization;
using Paddock.Simulation.Racing;
using Paddock.Simulation.Racing.Playback;

namespace Paddock.SimRunner;

/// <summary>
/// <c>race-replay --seed N [--speed X]</c>: asks <see cref="ReplayTapeSimulator"/> for the canned tape
/// (not the lap engine) and prints it as text lines through a <see cref="PlaybackScheduler"/> at X times real time.
/// </summary>
public static class RaceReplayCommand
{
    public const string Name = "race-replay";

    private const string Usage = "Usage: race-replay --seed <ulong> [--speed <number greater than zero>]";

    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var clock = new StopwatchClock();
        return Execute(args, stdout, stderr, clock, ms => Thread.Sleep((int)ms));
    }

    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr, IClock clock, Action<long> sleepMs)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(sleepMs);

        ulong? seed = null;
        double speed = 1.0;
        var speedSet = false;
        for (var i = 1; i < args.Length; i++)
        {
            var flag = args[i];
            if (flag is not ("--seed" or "--speed"))
            {
                stderr.WriteLine($"Unknown argument: {flag}");
                return 1;
            }

            if (i + 1 >= args.Length)
            {
                stderr.WriteLine($"Missing value for {flag}.");
                return 1;
            }

            var value = args[++i];
            if (flag == "--seed")
            {
                if (seed is not null)
                {
                    stderr.WriteLine($"Duplicate option: {flag}");
                    return 1;
                }

                if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedSeed))
                {
                    stderr.WriteLine($"Invalid --seed value: {value}");
                    return 1;
                }

                seed = parsedSeed;
            }
            else
            {
                if (speedSet)
                {
                    stderr.WriteLine($"Duplicate option: {flag}");
                    return 1;
                }

                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out speed)
                    || double.IsNaN(speed) || double.IsInfinity(speed) || speed <= 0)
                {
                    stderr.WriteLine("--speed must be a number greater than zero.");
                    return 1;
                }

                speedSet = true;
            }
        }

        if (seed is null)
        {
            stderr.WriteLine($"Missing required option --seed. {Usage}");
            return 1;
        }

        IRaceSimulator simulator = ReplayTapeSimulator.Instance;
        var tape = RaceSession.Run(simulator, RaceSimulationRequest.ForSeed(seed.Value));
        var cursor = new PlaybackScheduler(tape, clock, speed).CreateCursor();
        while (true)
        {
            foreach (var raceEvent in cursor.Poll())
            {
                stdout.WriteLine(RaceEventText.Format(raceEvent));
            }

            if (cursor.IsFinished)
            {
                return 0;
            }

            sleepMs(10);
        }
    }

    private sealed class StopwatchClock : IClock
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public long NowMs => _stopwatch.ElapsedMilliseconds;
    }
}
