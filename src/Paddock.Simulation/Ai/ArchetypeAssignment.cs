using System.Globalization;
using Paddock.Domain.Random;

namespace Paddock.Simulation.Ai;

/// <summary>What the archetype of a new principal is assigned from (all public): the team's financial tier, his age and the team's recent results.</summary>
/// <param name="TierShare">Where the team's budget stands among the teams, 0 (poorest) to 1 (richest).</param>
/// <param name="PrincipalAge">The age of the principal, or null when the team has none.</param>
/// <param name="Positions">The constructors' positions of the last seasons, latest first (empty when unknown).</param>
/// <param name="Teams">How many teams stood in the table.</param>
public sealed record ArchetypeInput(double TierShare, int? PrincipalAge, IReadOnlyList<int> Positions, int Teams);

/// <summary>
/// The archetype of a new AI principal (issue #109, point 7): an ESTIMATE rule over tier, age and recent results, with a little noise
/// from the <c>AiDecisions</c> stream so teams of one tier do not all think alike. A rich team with good results is a Contender, a
/// rich or rising one with a long view a Builder, a middling team led by a bold young principal an Opportunist, a poor team a Survivor.
/// </summary>
public static class ArchetypeAssignment
{
    /// <summary>ESTIMATE: spread of the noise added to each archetype's score.</summary>
    public const double NoiseSd = 0.25;

    public static PrincipalArchetype Assign(RngStream aiDecisionsSeason, string organization, ArchetypeInput input)
    {
        ArgumentNullException.ThrowIfNull(aiDecisionsSeason);
        ArgumentException.ThrowIfNullOrWhiteSpace(organization);
        ArgumentNullException.ThrowIfNull(input);
        var scores = Scores(input);
        var best = PrincipalArchetype.Contender;
        var bestScore = double.NegativeInfinity;
        foreach (var archetype in PrincipalArchetypes.All)
        {
            var rng = aiDecisionsSeason.DeriveChild(string.Create(CultureInfo.InvariantCulture, $"archetype:{organization}:{archetype}"));
            var noise = (rng.NextDouble() + rng.NextDouble() - 1.0) * NoiseSd * Math.Sqrt(6.0);
            var score = scores[(int)archetype] + noise;
            if (score > bestScore)
            {
                best = archetype;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>The scores before noise, indexed by <see cref="PrincipalArchetype"/>. Exposed so a test can read the rule.</summary>
    public static double[] Scores(ArchetypeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var tier = Math.Clamp(input.TierShare, 0.0, 1.0);
        var half = Math.Max(1.0, input.Teams / 2.0);
        double form = 0.0;
        var improving = 0.0;
        if (input.Positions.Count > 0)
        {
            var mean = input.Positions.Average();
            form = Math.Clamp((half - mean) / half, -1.0, 1.0);
            improving = input.Positions.Count > 1 && input.Positions[0] < input.Positions[1] ? 1.0 : 0.0;
        }

        var good = Math.Max(0.0, form);
        var bad = Math.Max(0.0, -form);
        var young = input.PrincipalAge is int age && age <= AiEstimates.YoungPrincipalAge ? 1.0 : 0.0;
        var veteran = input.PrincipalAge is int old && old >= AiEstimates.VeteranAge ? 1.0 : 0.0;
        var middle = 1.0 - Math.Abs(tier - 0.5) * 2.0;

        var scores = new double[4];
        scores[(int)PrincipalArchetype.Contender] = (1.5 * tier) + (1.0 * good) + (0.5 * young);
        scores[(int)PrincipalArchetype.Builder] = (0.8 * (1.0 - Math.Abs(tier - 0.7))) + (0.6 * improving) + (0.5 * veteran) + (0.3 * good);
        scores[(int)PrincipalArchetype.Opportunist] = (0.8 * middle) + (0.8 * young) + (0.3 * bad);
        scores[(int)PrincipalArchetype.Survivor] = (1.5 * (1.0 - tier)) + (1.0 * bad) + (0.4 * veteran);
        return scores;
    }
}
