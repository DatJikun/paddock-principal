namespace Paddock.Domain.World;

/// <summary>
/// One inclusive span of seasons in which a constructor belongs to a lineage.
/// A null <see cref="ToYear"/> means the span is still open.
/// </summary>
public readonly record struct LineageSpan(string LineageId, string ConstructorId, int FromYear, int? ToYear);
