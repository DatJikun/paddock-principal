namespace Paddock.Domain.World;

/// <summary>
/// The layout used for one championship round.
/// </summary>
public readonly record struct RaceAssignment(int Season, int Round, string LayoutId);
