namespace Paddock.Domain.World;

/// <summary>
/// One inclusive span of seasons in which a catalog dimension holds a single value.
/// A null <see cref="ToYear"/> means the span is still open.
/// </summary>
public readonly record struct RulePeriod(string DimensionId, string Value, int FromYear, int? ToYear);
