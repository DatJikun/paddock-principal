namespace Paddock.Domain.World;

/// <summary>
/// The value of every era dimension in one season.
/// </summary>
public sealed class EraSet
{
    private readonly RuleSet _rules;

    private EraSet(RuleSet rules) => _rules = rules;

    public int Season => _rules.Season;

    public IReadOnlyDictionary<string, string> Values => _rules.Values;

    public string Value(string dimensionId) => _rules.Value(dimensionId);

    /// <summary>
    /// Resolves every era-catalog dimension for <paramref name="season"/>.
    /// Each dimension must be covered by exactly one period. Overlaps and gaps throw.
    /// </summary>
    public static EraSet For(
        int season,
        IReadOnlyList<string> dimensionIds,
        IReadOnlyList<RulePeriod> periods) =>
        new(RuleSet.For(season, dimensionIds, periods));
}
