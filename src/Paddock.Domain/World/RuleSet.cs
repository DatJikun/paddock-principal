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

    private static bool Covers(RulePeriod period, int season)
    {
        if (period.ToYear is int toYear && period.FromYear > toYear)
        {
            return false;
        }

        return period.FromYear <= season && (period.ToYear is null || season <= period.ToYear.Value);
    }
}
