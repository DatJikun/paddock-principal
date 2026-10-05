using System.Globalization;
using Paddock.Application.Access;
using Paddock.Application.Contracts;
using Paddock.Domain.Spy;
using Paddock.Domain.World;

namespace Paddock.Application.Career;

/// <summary>One player-visible factor of a decision. The contribution is the trace's number, not a sentence.</summary>
public sealed record WhyFactor(string Name, string Contribution);

/// <summary>
/// One decision a viewer may ask about. <see cref="PlayerReason"/> is the trace's player text (often a translation key)
/// and is null when the trace has nothing a player may read. Truth context is never copied onto this type.
/// </summary>
public sealed record WhyLine(string DecisionId, string? PlayerReason, IReadOnlyList<WhyFactor> Factors);

/// <summary>
/// The player's "why" (INV-003, INV-005). A manager sees player-visible factors of decisions that are theirs, of talks
/// they run, or of their own board. A developer is not served here: the play shell is a manager. The read draws no RNG
/// and changes nothing.
/// </summary>
public static class WhyQuery
{
    public static IReadOnlyList<WhyLine> Visible(
        ITraceSink sink,
        AccessContext access,
        OrganizationId? ownTeam,
        IReadOnlyList<string> ownNegotiationIds,
        string? decisionId)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(ownNegotiationIds);
        if (sink is not MemorySink memory)
        {
            return [];
        }

        var manager = access.Manager?.Value;
        var lines = new List<WhyLine>();
        foreach (var trace in memory.KeyDecisions.Concat(memory.Traces))
        {
            if (decisionId is not null && !string.Equals(trace.Trigger, decisionId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!MaySee(trace, manager, ownTeam, ownNegotiationIds))
            {
                continue;
            }

            if (lines.Any(line => string.Equals(line.DecisionId, trace.Trigger, StringComparison.Ordinal)))
            {
                continue;
            }

            lines.Add(Describe(trace));
        }

        return lines;
    }

    private static bool MaySee(DecisionTrace trace, string? manager, OrganizationId? ownTeam, IReadOnlyList<string> ownNegotiationIds)
    {
        if (manager is not null && string.Equals(trace.Who, manager, StringComparison.Ordinal))
        {
            return true;
        }

        if (ownTeam is not null && string.Equals(trace.Who, "board:" + ownTeam.Value.Value, StringComparison.Ordinal))
        {
            return true;
        }

        const string prefix = "negotiation.response:";
        if (trace.Trigger.StartsWith(prefix, StringComparison.Ordinal))
        {
            var id = trace.Trigger[prefix.Length..];
            foreach (var own in ownNegotiationIds)
            {
                if (string.Equals(own, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static WhyLine Describe(DecisionTrace trace)
    {
        var factors = new List<WhyFactor>();
        foreach (var option in trace.Options)
        {
            if (!option.PlayerVisible && option.Id != trace.ChosenOptionId)
            {
                continue;
            }

            foreach (var factor in option.Factors)
            {
                if (!factor.PlayerVisible)
                {
                    continue;
                }

                factors.Add(new WhyFactor(factor.Name, factor.Contribution.ToString("0.###", CultureInfo.InvariantCulture)));
            }
        }

        return new WhyLine(trace.Trigger, trace.PlayerReason, factors);
    }
}
