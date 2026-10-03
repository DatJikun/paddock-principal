using System.Globalization;
using Paddock.Domain.Random;
using Paddock.Domain.Spy;

namespace Paddock.Application.Spy;

public readonly record struct ToyResult(ulong StateHash, RngState FinalRng);

/// <summary>
/// Toy decision loop that exercises Spy before any real system exists. Each step draws option
/// utilities from the AiDecisions stream and picks the best. Tracing is built only from values
/// already drawn, so the result is identical for every sink (INV-006).
/// </summary>
public static class ToyDecisionLoop
{
    private const int OptionCount = 3;
    private const int StepsPerWeekend = 5;
    private const int Season = 1950;
    private const ulong FnvPrime = 1099511628211UL;

    public static ToyResult Run(ulong seed, int steps, ITraceSink sink)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(steps);
        ArgumentNullException.ThrowIfNull(sink);

        var rng = RngStreams.Derive(seed, RngStreamName.AiDecisions, Season);
        const string manager = "toy-manager";
        ulong state = 14695981039346656037UL;

        for (var step = 0; step < steps; step++)
        {
            var utilities = new double[OptionCount];
            var best = 0;
            for (var i = 0; i < OptionCount; i++)
            {
                utilities[i] = rng.NextDouble();
                if (utilities[i] > utilities[best])
                {
                    best = i;
                }
            }

            unchecked
            {
                state = (state ^ (ulong)best) * FnvPrime;
                state = (state ^ BitConverter.DoubleToUInt64Bits(utilities[best])) * FnvPrime;
            }

            if (sink.IsEnabled)
            {
                sink.Record(BuildTrace(manager, step, utilities, best));
            }
        }

        return new ToyResult(state, rng.State);
    }

    private static DecisionTrace BuildTrace(string manager, int step, double[] utilities, int best)
    {
        var options = new TraceOption[utilities.Length];
        for (var i = 0; i < utilities.Length; i++)
        {
            options[i] = new TraceOption(
                "option-" + i.ToString(CultureInfo.InvariantCulture),
                utilities[i],
                [
                    new TraceFactor("raw-draw", utilities[i], PlayerVisible: true),
                    new TraceFactor("hidden-bias", 0.0, PlayerVisible: false),
                ],
                PlayerVisible: true);
        }

        var chosen = "option-" + best.ToString(CultureInfo.InvariantCulture);
        return new DecisionTrace(
            new WeekendKey(Season, step / StepsPerWeekend + 1),
            manager,
            Level: 1,
            Trigger: "toy-step-" + step.ToString(CultureInfo.InvariantCulture),
            options,
            chosen,
            Reason: "highest utility " + utilities[best].ToString("R", CultureInfo.InvariantCulture),
            PlayerReason: "Looked like the best option.",
            IsKeyDecision: step % StepsPerWeekend == 0,
            TruthContext: new Dictionary<string, string> { ["truth.best-index"] = best.ToString(CultureInfo.InvariantCulture) });
    }
}
