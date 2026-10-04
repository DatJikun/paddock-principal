using System.Diagnostics;
using System.Globalization;
using Paddock.Data.Authored;
using Paddock.Domain.Racing;
using Paddock.Domain.Spy;
using Paddock.Simulation.Racing;
using Paddock.Simulation.Racing.Playback;
using Paddock.Simulation.Racing.Weekend;

namespace Paddock.SimRunner;

/// <summary>
/// <c>race-replay --seed N [--season Y] [--round R] [--speed X]</c>: runs the real weekend of that round through
/// <see cref="RaceSession"/> (the lap engine, <see cref="RaceWeekend"/>) and prints its tape as text lines through a
/// <see cref="PlaybackScheduler"/> at X times real time. Without <c>--season</c>/<c>--round</c> it is the first race of the
/// starting season. The field is the SYNTHETIC fixture field of the era (<see cref="SyntheticField"/>), the same as the
/// <c>race</c> command: the world-to-entries integration (T47) does not exist yet. <see cref="FakeRaceTapeBuilder"/> is kept for tests only.
/// </summary>
public static class RaceReplayCommand
{
    public const string Name = "race-replay";

    /// <summary>The starting season when <c>--season</c> is not given (<see cref="Paddock.Domain.Career.CareerConfig.MinStartYear"/>).</summary>
    public const int DefaultSeason = Paddock.Domain.Career.CareerConfig.MinStartYear;

    private const string Usage = "Usage: race-replay --seed <ulong> [--season <int>] [--round <int>] [--speed <number greater than zero>]";

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
        int? season = null;
        int? round = null;
        for (var i = 1; i < args.Length; i++)
        {
            var flag = args[i];
            if (flag is not ("--seed" or "--speed" or "--season" or "--round"))
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
            else if (flag is "--season" or "--round")
            {
                if ((flag == "--season" ? season : round) is not null)
                {
                    stderr.WriteLine($"Duplicate option: {flag}");
                    return 1;
                }

                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0)
                {
                    stderr.WriteLine($"Invalid {flag} value: {value}");
                    return 1;
                }

                if (flag == "--season")
                {
                    season = number;
                }
                else
                {
                    round = number;
                }
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

        RaceTape tape;
        try
        {
            var root = RaceCommand.FindRoot();
            if (root is null)
            {
                stderr.WriteLine("Could not locate PaddockPrincipal.sln.");
                return 1;
            }

            var data = AuthoredDataLoader.Load(Path.Combine(root, "data"));
            tape = BuildTape(data, seed.Value, season, round);
        }
        catch (Exception ex) when (ex is AuthoredDataLoadException or InvalidOperationException or IOException)
        {
            stderr.WriteLine(ex.Message);
            return 1;
        }

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

    /// <summary>
    /// The tape of the real weekend of <paramref name="round"/> (the first round of the season when null), through the race seam
    /// (<see cref="RaceSession"/>, lap engine). Deterministic: the same arguments give the same tape.
    /// </summary>
    public static RaceTape BuildTape(AuthoredData data, ulong seed, int? season, int? round)
    {
        ArgumentNullException.ThrowIfNull(data);
        var year = season ?? DefaultSeason;
        var calendar = data.RaceAssignments.Where(a => a.Season == year).Select(a => a.Round).Order().ToList();
        if (calendar.Count == 0)
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"Season {year} has no rounds in the calendar."));
        }

        var raceRound = round ?? calendar[0];
        if (!calendar.Contains(raceRound))
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"Season {year} has no round {raceRound}."));
        }

        var input = RaceCommand.BuildInput(data, year, raceRound, seed, SyntheticField.For(year));
        return RaceCommand.Simulate(RaceSimulators.Resolve(RaceEngineKind.Lap), input, NullSink.Instance);
    }

    private sealed class StopwatchClock : IClock
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public long NowMs => _stopwatch.ElapsedMilliseconds;
    }
}
