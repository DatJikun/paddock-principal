using Paddock.Domain.World;

namespace Paddock.Data.Authored;

/// <summary>Turns catalog entries into the domain specs that <see cref="RuleSet.With(IReadOnlyDictionary{string, RuleDimensionSpec}, int, IReadOnlyList{RuleChange})"/> validates against.</summary>
public static class RuleCatalog
{
    /// <summary>
    /// The catalog has no numeric range for number dimensions, so these are ESTIMATED bounds
    /// (min, max, step) chosen to bracket every historical value. Not sourced; calibrate later.
    /// </summary>
    private static readonly Dictionary<string, (double Min, double Max, double Step)> EstimatedNumberRanges =
        new(StringComparer.Ordinal)
        {
            ["minimum_weight_kg"] = (400, 900, 10),
            ["driver_mass_allowance_kg"] = (0, 120, 5),
        };

    public static IReadOnlyList<RuleDimensionSpec> ToSpecs(IReadOnlyList<CatalogDimension> catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var specs = new List<RuleDimensionSpec>(catalog.Count);
        foreach (var dimension in catalog)
        {
            if (dimension.Values is { Count: > 0 } values)
            {
                specs.Add(new RuleDimensionSpec(dimension.Id, values));
            }
            else if (EstimatedNumberRanges.TryGetValue(dimension.Id, out var range))
            {
                specs.Add(new RuleDimensionSpec(dimension.Id, range.Min, range.Max, range.Step));
            }
            else
            {
                throw new InvalidOperationException(
                    $"Dimension '{dimension.Id}' has neither allowed values nor an estimated numeric range.");
            }
        }

        return specs;
    }
}
