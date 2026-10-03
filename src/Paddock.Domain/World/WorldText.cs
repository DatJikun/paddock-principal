using Paddock.Domain.People;

namespace Paddock.Domain.World;

/// <summary>
/// Player-facing explanations for world numbers. Copy lives in <c>strings/pl.json</c> and <c>strings/en.json</c>.
/// The core returns these keys. The 1–20 limits are <see cref="GenerationEstimates.AttributeMin"/> and
/// <see cref="GenerationEstimates.AttributeMax"/>, so the scale stays in one place.
/// </summary>
public static class WorldText
{
    public const string AttributeScaleExplanation = "world.attribute.scale_explanation";

    public const string AttributeBandExplanation = "world.knowledge.band_explanation";

    public const string PotentialExplanation = "world.potential.explanation";

    public const string SalaryExplanation = "world.salary.explanation";

    public const string BudgetExplanation = "world.budget.explanation";

    public static (string Name, int Value)[] AttributeScaleArguments() =>
    [
        ("min", GenerationEstimates.AttributeMin),
        ("max", GenerationEstimates.AttributeMax),
    ];

    public static (string Name, int Value)[] PotentialArguments() => AttributeScaleArguments();
}
