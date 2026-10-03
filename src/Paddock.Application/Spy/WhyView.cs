using Paddock.Application.Access;
using Paddock.Domain.Spy;

namespace Paddock.Application.Spy;

public sealed record WhyFactor(string Name, double Contribution);

public sealed record WhyOption(string Id, double Utility, IReadOnlyList<WhyFactor> Factors);

/// <summary>What one <see cref="AccessContext"/> may see of a <see cref="DecisionTrace"/>.</summary>
/// <param name="Reason">Developer reason (developer only) or the player-facing reason.</param>
/// <param name="TruthContext">Empty unless the viewer is the developer.</param>
public sealed record WhyView(
    ManagerId Who,
    string? ChosenOptionId,
    string? Reason,
    IReadOnlyList<WhyOption> Options,
    IReadOnlyDictionary<string, string> TruthContext)
{
    private static readonly IReadOnlyDictionary<string, string> NoTruth = new Dictionary<string, string>();

    /// <summary>
    /// Developer: everything. Manager or AI: only their own decisions, and only fields marked player-visible
    /// (no truth context, no developer reason, no hidden options or factors; an option's utility is
    /// the sum of its visible factors only). Returns null when the
    /// context may not see the trace at all.
    /// </summary>
    public static WhyView? For(AccessContext context, DecisionTrace trace)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(trace);

        if (context.Kind == AccessKind.Developer)
        {
            return new WhyView(
                new ManagerId(trace.Who),
                trace.ChosenOptionId,
                trace.Reason,
                trace.Options
                    .Select(o => new WhyOption(o.Id, o.Utility, o.Factors.Select(f => new WhyFactor(f.Name, f.Contribution)).ToArray()))
                    .ToArray(),
                new Dictionary<string, string>(trace.TruthContext));
        }

        if (context.Manager is not { } manager || !string.Equals(manager.Value, trace.Who, StringComparison.Ordinal))
        {
            return null;
        }

        var visible = trace.Options.Where(o => o.PlayerVisible).ToArray();
        var chosenVisible = visible.Any(o => string.Equals(o.Id, trace.ChosenOptionId, StringComparison.Ordinal));
        return new WhyView(
            new ManagerId(trace.Who),
            chosenVisible ? trace.ChosenOptionId : null,
            trace.PlayerReason,
            visible
                .Select(o =>
                {
                    var factors = o.Factors.Where(f => f.PlayerVisible).Select(f => new WhyFactor(f.Name, f.Contribution)).ToArray();
                    // The trace utility includes hidden factors; showing it would let a manager recover them (INV-003).
                    return new WhyOption(o.Id, factors.Sum(f => f.Contribution), factors);
                })
                .ToArray(),
            NoTruth);
    }
}
