using System.Globalization;
using Paddock.Domain.Spy;

namespace Paddock.Simulation.Ai;

/// <summary>One term of an option's utility before noise and bias. <see cref="PlayerVisible"/> marks what a player could know of the choice.</summary>
public sealed record FactorDraft(string Name, double Contribution, bool PlayerVisible = true);

/// <summary>One option a principal considers, with the terms of its utility. <see cref="Id"/> is a stable machine id.</summary>
public sealed record OptionDraft(string Id, IReadOnlyList<FactorDraft> Factors);

/// <summary>The result of a choice. <see cref="Utilities"/> are in the order of the options handed in, noise and bias included.</summary>
public sealed record Chosen(int Index, string Id, IReadOnlyList<double> Utilities);

/// <summary>What the trace of a decision says besides its options. <see cref="PlayerReasonKey"/> is a translation key (<see cref="AiTextKeys"/>).</summary>
public sealed record TraceNote(string Kind, string Key, string Trigger, string Reason, string? PlayerReasonKey, bool IsKeyDecision);

/// <summary>
/// Maximum-utility selection with a factor breakdown for every option (DESIGN section 8, TECH section 7). The utility of an option is the
/// sum of its terms, plus the historical pull (<see cref="IHistoricalBias"/>) and the principal's noise; the best option wins and an exact
/// tie goes to the option with the higher tie draw from the <c>AiDecisions</c> stream. The noise and the bias are hidden factors: they
/// are in the trace for the developer and not in the view of a player. Building the trace changes no choice: the numbers are
/// computed first and the sink only reads them (INV-006).
/// </summary>
public static class UtilityChooser
{
    public const string NoiseFactor = AiTextKeys.FactorNoise;

    public const string HistoryFactor = AiTextKeys.FactorHistory;

    public static Chosen Choose(
        DecisionContext context,
        DecisionFacet facet,
        TraceNote note,
        IReadOnlyList<OptionDraft> options,
        Func<string, string?>? playerReason = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Count == 0)
        {
            throw new ArgumentException("A choice needs at least one option.", nameof(options));
        }

        var utilities = new double[options.Count];
        var noises = new double[options.Count];
        var biases = new double[options.Count];
        var decisionKey = note.Kind + "|" + note.Key;
        var best = -1;
        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            var sum = 0.0;
            foreach (var factor in option.Factors)
            {
                sum += factor.Contribution;
            }

            biases[i] = context.Bias.Adjustment(context.Organization, note.Kind, option.Id, context.Today);
            noises[i] = context.Noise(facet, decisionKey, option.Id);
            utilities[i] = sum + biases[i] + noises[i];
            if (best < 0 || Beats(context, decisionKey, options, utilities, i, best))
            {
                best = i;
            }
        }

        var chosen = new Chosen(best, options[best].Id, utilities);
        if (context.Sink.IsEnabled)
        {
            context.Sink.Record(Trace(context, note, options, utilities, noises, biases, best, playerReason?.Invoke(options[best].Id) ?? note.PlayerReasonKey));
        }

        return chosen;
    }

    private static bool Beats(DecisionContext context, string decisionKey, IReadOnlyList<OptionDraft> options, double[] utilities, int challenger, int holder)
    {
        if (utilities[challenger] > utilities[holder])
        {
            return true;
        }

        if (utilities[challenger] < utilities[holder])
        {
            return false;
        }

        return context.Tie(decisionKey, options[challenger].Id) > context.Tie(decisionKey, options[holder].Id);
    }

    private static DecisionTrace Trace(
        DecisionContext context,
        TraceNote note,
        IReadOnlyList<OptionDraft> options,
        double[] utilities,
        double[] noises,
        double[] biases,
        int best,
        string? playerReason)
    {
        var traced = new TraceOption[options.Count];
        for (var i = 0; i < options.Count; i++)
        {
            var factors = new List<TraceFactor>(options[i].Factors.Count + 2);
            foreach (var factor in options[i].Factors)
            {
                factors.Add(new TraceFactor(factor.Name, factor.Contribution, factor.PlayerVisible));
            }

            factors.Add(new TraceFactor(HistoryFactor, biases[i], PlayerVisible: false));
            factors.Add(new TraceFactor(NoiseFactor, noises[i], PlayerVisible: false));
            traced[i] = new TraceOption(options[i].Id, utilities[i], factors, PlayerVisible: true);
        }

        return new DecisionTrace(
            new WeekendKey(context.Season, 0),
            context.Manager,
            context.Level,
            note.Kind + ":" + note.Trigger,
            traced,
            options[best].Id,
            note.Reason + " (archetype " + context.Archetype + ", level " + context.Level.ToString(CultureInfo.InvariantCulture) + ")",
            playerReason,
            note.IsKeyDecision,
            new Dictionary<string, string>
            {
                ["archetype"] = context.Archetype.ToString(),
                ["decision"] = note.Kind,
            });
    }

    /// <summary>
    /// Records the trace of a decision no utility settled (a rule fired, or a constraint left one option). The same record shape, so a
    /// reader finds one kind of trace for every decision.
    /// </summary>
    public static void Record(DecisionContext context, TraceNote note, IReadOnlyList<OptionDraft> options, string chosenId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(options);
        if (!context.Sink.IsEnabled)
        {
            return;
        }

        var traced = new List<TraceOption>(options.Count);
        foreach (var option in options)
        {
            var sum = 0.0;
            var factors = new List<TraceFactor>(option.Factors.Count);
            foreach (var factor in option.Factors)
            {
                sum += factor.Contribution;
                factors.Add(new TraceFactor(factor.Name, factor.Contribution, factor.PlayerVisible));
            }

            traced.Add(new TraceOption(option.Id, sum, factors, PlayerVisible: true));
        }

        context.Sink.Record(new DecisionTrace(
            new WeekendKey(context.Season, 0),
            context.Manager,
            context.Level,
            note.Kind + ":" + note.Trigger,
            traced,
            chosenId,
            note.Reason + " (archetype " + context.Archetype + ", level " + context.Level.ToString(CultureInfo.InvariantCulture) + ")",
            note.PlayerReasonKey,
            note.IsKeyDecision,
            new Dictionary<string, string>
            {
                ["archetype"] = context.Archetype.ToString(),
                ["decision"] = note.Kind,
            }));
    }
}
