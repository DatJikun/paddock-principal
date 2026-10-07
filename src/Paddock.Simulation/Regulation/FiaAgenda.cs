using System.Globalization;
using Paddock.Domain.Random;

namespace Paddock.Simulation.Regulation;

/// <summary>What the FIA can see of the state of a series' world, each from 0 to 1 (public facts only).</summary>
/// <param name="Dominance">How much one team has taken the season: its share of the wins, or of the points.</param>
/// <param name="Attrition">The share of starters that did not finish, which stands in for accidents and breakdowns.</param>
/// <param name="Distress">The share of teams that are in debt.</param>
public readonly record struct FiaPressures(double Dominance, double Attrition, double Distress)
{
    public static FiaPressures Calm => new(0, 0, 0);
}

/// <summary>One change the FIA could bring: the rule, its value now, the value it would move to, and how the two differ.</summary>
public sealed record RuleCandidate(string DimensionId, string Current, string Value, ValueTraits Delta);

/// <summary>A proposal of the FIA and why it brings it: the pressure that weighed most, as a translation key with its size.</summary>
public sealed record FiaProposal(string DimensionId, string Value, string ReasonKey, IReadOnlyList<KeyValuePair<string, string>> ReasonArguments);

/// <summary>
/// The FIA's own votes (#275, owner decision 5 and scope 9). They come from the state of the series' world and not from a random
/// list: dominance pushes the rules that narrow the gap or add spectacle, accidents push the safer rule, costs push the cheaper
/// one, and the sport drifts to the newer rule. Among the best-scoring changes one is drawn from the Regulations stream. A change
/// that makes no difference to anyone is never a candidate (see <see cref="LiveRules.HasEffect"/>), and a rule changed last year may
/// be proposed again: nothing is locked.
/// </summary>
public static class FiaAgenda
{
    public const string ReasonDominance = "regulation.fia.reason.dominance";

    public const string ReasonAccidents = "regulation.fia.reason.accidents";

    public const string ReasonCosts = "regulation.fia.reason.costs";

    public const string ReasonModernise = "regulation.fia.reason.modernise";

    public const string ReasonReview = "regulation.fia.reason.review";

    /// <summary>
    /// The proposal for one FIA slot, or null when there is no candidate left. The draw uses <paramref name="rng"/> only to pick among
    /// the shortlist, so the number of draws does not depend on the outcome.
    /// </summary>
    public static FiaProposal? Propose(FiaPressures pressures, IReadOnlyList<RuleCandidate> candidates, Xoshiro256StarStar rng)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(rng);
        var scored = new List<(RuleCandidate Candidate, double Score, string Reason, double Pressure)>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var delta = candidate.Delta;
            var contributions = new (string Reason, double Value, double Pressure)[]
            {
                (ReasonDominance, pressures.Dominance * (delta.Equal + (0.5 * delta.Show)), pressures.Dominance),
                (ReasonAccidents, pressures.Attrition * 1.5 * delta.Safety, pressures.Attrition),
                (ReasonCosts, pressures.Distress * 1.5 * delta.Cheap, pressures.Distress),
                (ReasonModernise, RegulationEstimates.FiaModernDrift * delta.Modern, 0),
            };
            var best = contributions[0];
            double score = 0;
            foreach (var contribution in contributions)
            {
                score += contribution.Value;
                if (contribution.Value > best.Value)
                {
                    best = contribution;
                }
            }

            scored.Add((candidate, score, best.Value > 0 ? best.Reason : ReasonReview, best.Pressure));
        }

        if (scored.Count == 0)
        {
            return null;
        }

        scored.Sort(static (left, right) =>
        {
            var byScore = right.Score.CompareTo(left.Score);
            if (byScore != 0)
            {
                return byScore;
            }

            var byDimension = string.CompareOrdinal(left.Candidate.DimensionId, right.Candidate.DimensionId);
            return byDimension != 0 ? byDimension : string.CompareOrdinal(left.Candidate.Value, right.Candidate.Value);
        });
        var shortlist = Math.Min(RegulationEstimates.FiaShortlist, scored.Count);
        var picked = scored[rng.NextInt(0, shortlist)];
        var level = Math.Round(picked.Pressure * 100, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
        return new FiaProposal(
            picked.Candidate.DimensionId,
            picked.Candidate.Value,
            picked.Reason,
            picked.Reason == ReasonModernise || picked.Reason == ReasonReview ? [] : [new KeyValuePair<string, string>("level", level)]);
    }
}
