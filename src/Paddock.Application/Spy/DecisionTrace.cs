using Paddock.Application.Access;

namespace Paddock.Application.Spy;

/// <summary>Race weekend a trace belongs to; the RAM buffer keeps one weekend at a time (TECH §7).</summary>
public readonly record struct WeekendKey(int Season, int Round);

/// <summary>One term of an option's utility. <see cref="PlayerVisible"/> marks it as safe for the player's "why" view.</summary>
public sealed record TraceFactor(string Name, double Contribution, bool PlayerVisible);

/// <summary>One option the decider considered, with its utility and the factors behind it.</summary>
public sealed record TraceOption(string Id, double Utility, IReadOnlyList<TraceFactor> Factors, bool PlayerVisible);

/// <summary>
/// Why an actor decided what it decided (TECH §7). Immutable; building one must never touch RNG or
/// simulation state (INV-005, INV-006).
/// </summary>
/// <param name="Who">The deciding manager.</param>
/// <param name="Level">Competence tier of the decider (estimate, to be calibrated).</param>
/// <param name="Trigger">What prompted the decision.</param>
/// <param name="Options">Everything considered, with utilities and factor breakdowns.</param>
/// <param name="ChosenOptionId">Id of the chosen option.</param>
/// <param name="Reason">Developer-facing reason; may mention truth.</param>
/// <param name="PlayerReason">Reason text fit for the player (null if nothing may be shown).</param>
/// <param name="IsKeyDecision">Would be kept in a save (transfers, titles, big failures).</param>
/// <param name="TruthContext">Simulation truth behind the decision. Developer only, never shown to managers.</param>
public sealed record DecisionTrace(
    WeekendKey Weekend,
    ManagerId Who,
    int Level,
    string Trigger,
    IReadOnlyList<TraceOption> Options,
    string ChosenOptionId,
    string Reason,
    string? PlayerReason,
    bool IsKeyDecision,
    IReadOnlyDictionary<string, string> TruthContext);
