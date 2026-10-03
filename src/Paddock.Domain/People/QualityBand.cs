namespace Paddock.Domain.People;

/// <summary>
/// Target quality of a generated person. Filler is the pool default.
/// The bands are estimates in <see cref="GenerationEstimates"/>.
/// </summary>
public enum QualityBand
{
    Filler = 0,
    Solid = 1,
    Contender = 2,
    FutureStar = 3,
}
