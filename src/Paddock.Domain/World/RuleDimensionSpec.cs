namespace Paddock.Domain.World;

public enum RuleDimensionKind
{
    /// <summary>Closed list of allowed values. The list order is the catalog order and defines "one step".</summary>
    Choice,

    /// <summary>Numeric value inside an inclusive range, moved in multiples of the step.</summary>
    Number,
}

/// <summary>
/// What values one regulation dimension may take. A plain domain mirror of a catalog entry,
/// so that <see cref="RuleSet"/> can validate changes without referencing the data layer.
/// </summary>
public sealed record RuleDimensionSpec
{
    public RuleDimensionSpec(string id, IReadOnlyList<string> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(values);
        Id = id;
        Kind = RuleDimensionKind.Choice;
        Values = values;
    }

    public RuleDimensionSpec(string id, double min, double max, double step)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!(min < max) || !(step > 0))
        {
            throw new ArgumentException("A numeric dimension needs min < max and a positive step.");
        }

        Id = id;
        Kind = RuleDimensionKind.Number;
        Values = [];
        Min = min;
        Max = max;
        Step = step;
    }

    public string Id { get; }

    public RuleDimensionKind Kind { get; }

    public IReadOnlyList<string> Values { get; }

    public double Min { get; }

    public double Max { get; }

    public double Step { get; }
}
