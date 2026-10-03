using System.Globalization;
using Paddock.Application.Spy;

namespace Paddock.SimRunner;

/// <summary>
/// <c>toy --seed &lt;ulong&gt; --steps &lt;int&gt; [--spy]</c>: runs the toy decision loop. Prints the state hash;
/// with <c>--spy</c> also prints one developer trace JSON line per decision. The hash is the same either way.
/// </summary>
public static class ToyCommand
{
    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        ulong? seed = null;
        int? steps = null;
        var spy = false;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--spy":
                    spy = true;
                    break;
                case "--seed" when i + 1 < args.Length && seed is null:
                    if (!ulong.TryParse(args[++i], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedSeed))
                    {
                        stderr.WriteLine($"Invalid --seed value: {args[i]}");
                        return 1;
                    }

                    seed = parsedSeed;
                    break;
                case "--steps" when i + 1 < args.Length && steps is null:
                    if (!int.TryParse(args[++i], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedSteps))
                    {
                        stderr.WriteLine($"Invalid --steps value: {args[i]}");
                        return 1;
                    }

                    steps = parsedSteps;
                    break;
                default:
                    stderr.WriteLine($"Unknown, duplicate or incomplete argument: {args[i]}");
                    return 1;
            }
        }

        if (seed is null || steps is null)
        {
            stderr.WriteLine("Missing required option. Expected toy --seed <ulong> --steps <int> [--spy].");
            return 1;
        }

        // MemorySink keeps only the current weekend (TECH §7), so --spy collects every trace itself.
        var collector = spy ? new CollectingSink() : null;
        ITraceSink sink = collector ?? (ITraceSink)NullSink.Instance;
        var result = ToyDecisionLoop.Run(seed.Value, steps.Value, sink);
        stdout.WriteLine(result.StateHash.ToString(CultureInfo.InvariantCulture));
        if (collector is not null)
        {
            foreach (var trace in collector.Traces)
            {
                stdout.WriteLine(TraceJson.Serialize(trace));
            }
        }

        return 0;
    }

    /// <summary>Keeps every trace in order, across weekends; passive like every sink (INV-006).</summary>
    private sealed class CollectingSink : ITraceSink
    {
        public List<DecisionTrace> Traces { get; } = [];

        public bool IsEnabled => true;

        public void Record(DecisionTrace trace)
        {
            ArgumentNullException.ThrowIfNull(trace);
            Traces.Add(trace);
        }
    }
}
