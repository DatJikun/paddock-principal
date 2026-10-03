namespace Paddock.Application.World;

/// <summary>
/// Mutation boundary for the command pipeline (INV-001).
/// T16 (#37) is still open and owns <c>GameDate</c> plus <c>WorldClock.AdvanceDay</c>.
/// This seam stays in Application so Simulation does not reference the command layer.
/// When T16 merges, add an Application adapter over the simulation clock:
/// <see cref="AdvanceDate"/> becomes <c>WorldClock.AdvanceDay</c> (that day's scheduled events included),
/// and <see cref="CurrentDate"/> becomes T16's game date. <see cref="DateOnly"/> is the stand-in
/// because the save header already stores a calendar date.
/// Queries must not call <see cref="AdvanceDate"/> (INV-005).
/// </summary>
public interface IWorldState
{
    DateOnly CurrentDate { get; }

    void AdvanceDate();

    /// <summary>
    /// Deterministic SHA-256 of the state, lowercase hex. Not <see cref="object.GetHashCode"/>.
    /// </summary>
    string ContentHash();
}
