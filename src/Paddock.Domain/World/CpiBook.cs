using System.Globalization;

namespace Paddock.Domain.World;

/// <summary>
/// One US CPI annual average. <see cref="Index"/> is the index level, not a year-over-year change.
/// </summary>
public readonly record struct CpiObservation(int Year, decimal Index);

/// <summary>
/// US CPI annual averages. <see cref="ToUsd2025"/> scales a nominal amount by the ratio of the
/// <see cref="BaseYear"/> average to the average of the requested year.
/// </summary>
public sealed class CpiBook
{
    /// <summary>Annual-average year used as the conversion base.</summary>
    public const int BaseYear = 2025;

    private readonly IReadOnlyList<CpiObservation> _observations;

    public CpiBook(IReadOnlyList<CpiObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        _observations = observations;
    }

    public decimal Index(int year) => Require(year);

    /// <summary>
    /// Converts <paramref name="nominal"/> into <see cref="BaseYear"/> dollars.
    /// The base year round-trips: the ratio is 1, so the nominal amount is returned unchanged.
    /// </summary>
    public decimal ToUsd2025(decimal nominal, int year)
    {
        var index = Require(year);
        var baseIndex = Require(BaseYear);
        if (index == baseIndex)
        {
            return nominal;
        }

        return nominal * baseIndex / index;
    }

    private decimal Require(int year)
    {
        decimal? found = null;
        foreach (var observation in _observations)
        {
            if (observation.Year != year)
            {
                continue;
            }

            if (found is not null)
            {
                throw new InvalidOperationException(
                    $"CPI lists {year.ToString(CultureInfo.InvariantCulture)} more than once.");
            }

            found = observation.Index;
        }

        if (found is null)
        {
            throw new InvalidOperationException(
                $"CPI has no annual average for {year.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (found.Value <= 0)
        {
            throw new InvalidOperationException(
                $"CPI for {year.ToString(CultureInfo.InvariantCulture)} must be positive.");
        }

        return found.Value;
    }
}
