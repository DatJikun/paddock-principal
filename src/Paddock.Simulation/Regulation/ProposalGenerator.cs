using System.Globalization;
using Paddock.Domain.Random;
using Paddock.Domain.World;

namespace Paddock.Simulation.Regulation;

/// <summary>A proposal that failed a vote, remembered so it is not brought back at once.</summary>
public readonly record struct RejectedProposal(string DimensionId, string Value, int Season);

/// <summary>Generator limits. Every default is an ESTIMATE until calibrated.</summary>
/// <param name="ProposalsPerSeason">How many proposals one season sees (at most; fewer when nothing valid is left).</param>
/// <param name="MaxStep">Largest move from the current value: list positions for choices, step multiples for numbers.</param>
/// <param name="RejectionMemorySeasons">N: a rejected (dimension, value) is not re-proposed for this many seasons.</param>
public sealed record ProposalGeneratorOptions(
    int ProposalsPerSeason = 3,
    int MaxStep = 2,
    int RejectionMemorySeasons = 5);

public static class ProposalGenerator
{
    /// <summary>Stream for one season's proposals and votes; isolated from every other stream.</summary>
    public static Xoshiro256StarStar StreamFor(ulong masterSeed, int season) =>
        RngStreams.Derive(masterSeed, RngStreamName.Regulations, season);

    /// <summary>
    /// Proposals for <paramref name="ruleSet"/>'s next season: distinct dimensions, bounded steps from
    /// the current value, always valid for the catalog, never a recently rejected (dimension, value), never the "unknown" data-gap sentinel.
    /// Dimensions whose current value is not a catalog value (e.g. "unknown") are not touched.
    /// </summary>
    public static IReadOnlyList<RuleProposal> Generate(
        RuleSet ruleSet,
        IReadOnlyList<RuleDimensionSpec> catalog,
        IReadOnlyList<string> proposerIds,
        IReadOnlyList<RejectedProposal> rejected,
        ProposalGeneratorOptions options,
        Xoshiro256StarStar rng)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(proposerIds);
        ArgumentNullException.ThrowIfNull(rejected);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(rng);
        if (proposerIds.Count == 0)
        {
            throw new ArgumentException("At least one proposer is needed.", nameof(proposerIds));
        }

        var startSeason = ruleSet.Season + 1;
        var blocked = new HashSet<(string, string)>();
        foreach (var r in rejected)
        {
            if (startSeason - r.Season <= options.RejectionMemorySeasons)
            {
                blocked.Add((r.DimensionId, r.Value));
            }
        }

        var candidates = new List<(string Dimension, string Value)>();
        var dimensionsWithCandidates = new List<string>();
        foreach (var spec in catalog)
        {
            if (!ruleSet.Values.TryGetValue(spec.Id, out var current))
            {
                continue;
            }

            var before = candidates.Count;
            foreach (var value in Neighbours(spec, current, options.MaxStep))
            {
                if (!blocked.Contains((spec.Id, value)))
                {
                    candidates.Add((spec.Id, value));
                }
            }

            if (candidates.Count > before)
            {
                dimensionsWithCandidates.Add(spec.Id);
            }
        }

        var proposals = new List<RuleProposal>();
        var count = Math.Min(options.ProposalsPerSeason, dimensionsWithCandidates.Count);
        for (var n = 0; n < count; n++)
        {
            var dimension = dimensionsWithCandidates[rng.NextInt(0, dimensionsWithCandidates.Count)];
            dimensionsWithCandidates.Remove(dimension);
            var values = candidates.Where(c => c.Dimension == dimension).Select(c => c.Value).ToList();
            var value = values[rng.NextInt(0, values.Count)];
            var proposer = proposerIds[rng.NextInt(0, proposerIds.Count)];
            proposals.Add(new RuleProposal(
                $"{startSeason.ToString(CultureInfo.InvariantCulture)}-{n.ToString(CultureInfo.InvariantCulture)}",
                dimension,
                value,
                proposer,
                startSeason));
        }

        return proposals;
    }

    private static IEnumerable<string> Neighbours(RuleDimensionSpec spec, string current, int maxStep)
    {
        if (spec.Kind == RuleDimensionKind.Choice)
        {
            var index = IndexOf(spec.Values, current);
            if (index < 0)
            {
                yield break;
            }

            if (spec.Values.Count == 2)
            {
                if (!IsDataGap(spec.Values[1 - index]))
                {
                    yield return spec.Values[1 - index];
                }

                yield break;
            }

            for (var delta = -maxStep; delta <= maxStep; delta++)
            {
                var target = index + delta;
                if (delta != 0 && target >= 0 && target < spec.Values.Count && !IsDataGap(spec.Values[target]))
                {
                    yield return spec.Values[target];
                }
            }

            yield break;
        }

        if (!double.TryParse(current, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            || !double.IsFinite(number))
        {
            yield break;
        }

        for (var delta = -maxStep; delta <= maxStep; delta++)
        {
            var target = number + delta * spec.Step;
            if (delta != 0 && target >= spec.Min && target <= spec.Max)
            {
                yield return target.ToString("0.###", CultureInfo.InvariantCulture);
            }
        }
    }

    /// <summary>"unknown" marks a gap in the data, not a rule; it is never a proposal target.</summary>
    private static bool IsDataGap(string value) => string.Equals(value, "unknown", StringComparison.Ordinal);

    private static int IndexOf(IReadOnlyList<string> values, string value)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
