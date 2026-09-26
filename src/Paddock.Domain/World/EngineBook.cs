namespace Paddock.Domain.World;

/// <summary>
/// Looks up the engines supplied to a constructor in one season.
/// </summary>
public static class EngineBook
{
    public static IReadOnlyList<EngineSupply> EnginesFor(
        string constructorId,
        int season,
        IReadOnlyList<EngineSupply> supplies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(constructorId);
        ArgumentNullException.ThrowIfNull(supplies);

        var matches = new List<EngineSupply>();
        foreach (var supply in supplies)
        {
            if (supply.Season == season
                && string.Equals(supply.ConstructorId, constructorId, StringComparison.Ordinal))
            {
                matches.Add(supply);
            }
        }

        return matches;
    }
}
