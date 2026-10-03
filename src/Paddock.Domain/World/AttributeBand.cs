using Paddock.Domain.People;

namespace Paddock.Domain.World;

/// <summary>
/// Inclusive range an organization believes for one attribute (for example 16–19).
/// This is knowledge, not the simulation's true number (INV-003).
/// Ends use the 1–20 scale in <see cref="GenerationEstimates"/>.
/// </summary>
public readonly record struct AttributeBand
{
    public AttributeBand(int low, int high)
    {
        if (low < GenerationEstimates.AttributeMin || low > GenerationEstimates.AttributeMax)
        {
            throw new ArgumentOutOfRangeException(nameof(low), low, "Attribute bands use the 1–20 scale.");
        }

        if (high < low || high > GenerationEstimates.AttributeMax)
        {
            throw new ArgumentOutOfRangeException(nameof(high), high, "A band's high end is between its low end and 20.");
        }

        Low = low;
        High = high;
    }

    public int Low { get; }

    public int High { get; }

    public override string ToString() => Low + "-" + High;
}
