using Paddock.Application.Access;

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
    /// (no truth context, no developer reason, no hidden options or factors). Returns null when the
    /// context may not see the trace at all.
    /// </summary>
    public static WhyView? For(AccessContext context, DecisionTrace trace)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(trace);

        if (context.Kind == AccessKind.Developer)
        {
            return new WhyView(
                trace.Who,
                trace.ChosenOptionId,
                trace.Reason,
                trace.Options
                    .Select(o => new WhyOption(o.Id, o.Utility, o.Factors.Select(f => new WhyFactor(f.Name, f.Contribution)).ToArray()))
                    .ToArray(),
                new Dictionary<string, string>(trace.TruthContext));
        }

        if (context.Manager != trace.Who)
        {
            return null;
        }

        var visible = trace.Options.Where(o => o.PlayerVisible).ToArray();
        var chosenVisible = visible.Any(o => string.Equals(o.Id, trace.ChosenOptionId, StringComparison.Ordinal));
        return new WhyView(
            trace.Who,
            chosenVisible ? trace.ChosenOptionId : null,
            trace.PlayerReason,
            visible
                .Select(o => new WhyOption(
                    o.Id,
                    o.Utility,
                    o.Factors.Where(f => f.PlayerVisible).Select(f => new WhyFactor(f.Name, f.Contribution)).ToArray()))
                .ToArray(),
            NoTruth);
    }
}
