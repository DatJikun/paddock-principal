using Paddock.Domain.Racing;

namespace Paddock.Simulation.Regulation;

/// <summary>
/// The FIA president (#275, owner decision 4). He votes on a tie and only on a tie. His stance is stable and reads only what is on the
/// ballot: he backs the FIA's own agenda and the modernising drift of the sport, and leans to the rule as it is against a proposal of
/// the teams. It is no randomness and no hidden truth, so the same tie always gets the same answer.
/// </summary>
public static class FiaPresident
{
    /// <summary>ESTIMATE: how much he favours a variant that is the FIA's own.</summary>
    public const double AgendaBonus = 0.3;

    /// <summary>ESTIMATE: how much he favours the rule as it is against a proposal of the teams.</summary>
    public const double TeamProposalResistance = 0.15;

    /// <summary>
    /// The option he picks among the <paramref name="tied"/> ones (status quo first, then variants). <paramref name="deltaOf"/> gives
    /// the trait difference of a variant against the rule in force.
    /// </summary>
    public static string Choose(BallotOrigin origin, IReadOnlyList<string> tied, Func<string, ValueTraits> deltaOf)
    {
        ArgumentNullException.ThrowIfNull(tied);
        ArgumentNullException.ThrowIfNull(deltaOf);
        if (tied.Count == 0)
        {
            throw new ArgumentException("There is nothing to decide.", nameof(tied));
        }

        var best = tied[0];
        var bestScore = double.NegativeInfinity;
        foreach (var option in tied)
        {
            var score = option == BallotOptions.StatusQuo
                ? (origin == BallotOrigin.Teams ? TeamProposalResistance : 0)
                : (RegulationEstimates.FiaModernDrift * deltaOf(option).Modern) + (origin == BallotOrigin.Fia ? AgendaBonus : 0);
            if (score > bestScore)
            {
                bestScore = score;
                best = option;
            }
        }

        return best;
    }
}
