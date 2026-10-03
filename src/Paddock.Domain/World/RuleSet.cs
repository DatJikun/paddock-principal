using System.Globalization;

namespace Paddock.Domain.World;

/// <summary>
/// The value of every regulation dimension in one season.
/// </summary>
public sealed class RuleSet
{
    private readonly Dictionary<string, string> _values;

    private RuleSet(int season, Dictionary<string, string> values)
    {
        Season = season;
        _values = values;
    }

    public int Season { get; }

    public IReadOnlyDictionary<string, string> Values => _values;

    public string Value(string dimensionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionId);
        if (!_values.TryGetValue(dimensionId, out var value))
        {
            throw new KeyNotFoundException(
                $"Season {Season.ToString(CultureInfo.InvariantCulture)} has no value for dimension '{dimensionId}'.");
        }

        return value;
    }

    /// <summary>
    /// Resolves every catalog dimension for <paramref name="season"/>.
    /// Each dimension must be covered by exactly one period. Overlaps and gaps throw.
    /// </summary>
    public static RuleSet For(
        int season,
        IReadOnlyList<string> dimensionIds,
        IReadOnlyList<RulePeriod> periods)
    {
        ArgumentNullException.ThrowIfNull(dimensionIds);
        ArgumentNullException.ThrowIfNull(periods);

        var values = new Dictionary<string, string>(dimensionIds.Count, StringComparer.Ordinal);
        foreach (var dimensionId in dimensionIds)
        {
            if (string.IsNullOrWhiteSpace(dimensionId))
            {
                throw new ArgumentException("A catalog dimension id is missing.", nameof(dimensionIds));
            }

            string? resolved = null;
            foreach (var period in periods)
            {
                if (!string.Equals(period.DimensionId, dimensionId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (period.Value is null)
                {
                    throw new ArgumentException(
                        $"Dimension '{dimensionId}' has a null value.",
                        nameof(periods));
                }

                if (!Covers(period, season))
                {
                    continue;
                }

                if (resolved is not null)
                {
                    throw new InvalidOperationException(
                        $"Dimension '{dimensionId}' has more than one value in {season.ToString(CultureInfo.InvariantCulture)}.");
                }

                resolved = period.Value;
            }

            if (resolved is null)
            {
                throw new InvalidOperationException(
                    $"Dimension '{dimensionId}' has no value in {season.ToString(CultureInfo.InvariantCulture)}.");
            }

            if (!values.TryAdd(dimensionId, resolved))
            {
                throw new InvalidOperationException(
                    $"Dimension '{dimensionId}' is listed more than once in the catalog.");
            }
        }

        return new RuleSet(season, values);
    }

    /// <summary>
    /// Produces the rule set of <see cref="Season"/> + 1 with <paramref name="changes"/> applied; this
    /// instance is not modified. See <see cref="With(IReadOnlyDictionary{string, RuleDimensionSpec}, int, IReadOnlyList{RuleChange})"/>.
    /// </summary>
    public RuleSet With(IReadOnlyDictionary<string, RuleDimensionSpec> catalog, params RuleChange[] changes) =>
        With(catalog, Season + 1, changes);

    /// <summary>
    /// Produces the rule set of <paramref name="nextSeason"/> with <paramref name="changes"/> applied; this
    /// instance is not modified. Every change is validated against <paramref name="catalog"/>
    /// (enum membership, numeric range). Any invalid change rejects the whole call with
    /// a <see cref="RuleChangeException"/> carrying a <see cref="RuleChangeError"/> code.
    /// </summary>
    public RuleSet With(
        IReadOnlyDictionary<string, RuleDimensionSpec> catalog,
        int nextSeason,
        IReadOnlyList<RuleChange> changes)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(changes);

        var next = new Dictionary<string, string>(_values, StringComparer.Ordinal);
        var touched = new HashSet<string>(StringComparer.Ordinal);
        foreach (var change in changes)
        {
            if (!catalog.TryGetValue(change.DimensionId, out var spec) || !_values.ContainsKey(change.DimensionId))
            {
                throw new RuleChangeException(
                    RuleChangeError.UnknownDimension,
                    $"Unknown dimension '{change.DimensionId}'.");
            }

            if (!touched.Add(change.DimensionId))
            {
                throw new RuleChangeException(
                    RuleChangeError.DuplicateDimension,
                    $"Dimension '{change.DimensionId}' is changed more than once.");
            }

            Validate(spec, change.NewValue);
            next[change.DimensionId] = change.NewValue;
        }

        return new RuleSet(nextSeason, next);
    }

    private static void Validate(RuleDimensionSpec spec, string? value)
    {
        if (spec.Kind == RuleDimensionKind.Choice)
        {
            if (value is null || !spec.Values.Contains(value))
            {
                throw new RuleChangeException(
                    RuleChangeError.ValueNotAllowed,
                    $"'{value}' is not an allowed value of '{spec.Id}'.");
            }

            return;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            || !double.IsFinite(number))
        {
            throw new RuleChangeException(RuleChangeError.NotANumber, $"'{value}' is not a number for '{spec.Id}'.");
        }

        if (number < spec.Min || number > spec.Max)
        {
            throw new RuleChangeException(
                RuleChangeError.OutOfRange,
                $"{value} is outside [{spec.Min.ToString(CultureInfo.InvariantCulture)}, {spec.Max.ToString(CultureInfo.InvariantCulture)}] for '{spec.Id}'.");
        }
    }

    private static bool Covers(RulePeriod period, int season)
    {
        if (period.ToYear is int toYear && period.FromYear > toYear)
        {
            return false;
        }

        return period.FromYear <= season && (period.ToYear is null || season <= period.ToYear.Value);
    }
}
