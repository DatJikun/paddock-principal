using System.Globalization;
using Paddock.Domain.World;

namespace Paddock.Domain.Finance;

/// <summary>The era figures finance reads for one season. Budgets are nominal USD (whole dollars). ESTIMATE where the source says so.</summary>
public sealed record EraFinanceFacts(string RevenueModel, long LowDollars, long TypicalDollars, long TopDollars)
{
    public long Dollars(TeamTier tier) => tier switch
    {
        TeamTier.Low => LowDollars,
        TeamTier.Typical => TypicalDollars,
        TeamTier.Top => TopDollars,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown team tier."),
    };
}

/// <summary>
/// Reads era budget steps. A year inside a step uses that step's value, so 1955 is the 1950–1957 figure exactly.
/// ESTIMATE blend: the first year of a step that follows a different value is the integer average of the two,
/// so the money level does not jump on the boundary year. The revenue model is not averaged: the new step's model applies
/// from its first year.
/// </summary>
public static class EraFinance
{
    public static EraFinanceFacts ForYear(IReadOnlyList<RulePeriod> periods, int year)
    {
        ArgumentNullException.ThrowIfNull(periods);
        return new EraFinanceFacts(
            Text(periods, FinanceEstimates.RevenueModelDimension, year),
            Dollars(periods, FinanceEstimates.BudgetLow, year),
            Dollars(periods, FinanceEstimates.BudgetTypical, year),
            Dollars(periods, FinanceEstimates.BudgetTop, year));
    }

    public static long Dollars(IReadOnlyList<RulePeriod> periods, string dimension, int year)
    {
        var (current, previous) = Covering(periods, dimension, year);
        var value = ParseDollars(current.Value, dimension, year);
        if (previous is RulePeriod prior && year == current.FromYear)
        {
            var before = ParseDollars(prior.Value, dimension, prior.FromYear);
            if (before != value)
            {
                return (before + value) / 2;
            }
        }

        return value;
    }

    private static string Text(IReadOnlyList<RulePeriod> periods, string dimension, int year) =>
        Covering(periods, dimension, year).Current.Value;

    private static (RulePeriod Current, RulePeriod? Previous) Covering(
        IReadOnlyList<RulePeriod> periods,
        string dimension,
        int year)
    {
        RulePeriod? current = null;
        RulePeriod? previous = null;
        foreach (var period in periods)
        {
            if (!string.Equals(period.DimensionId, dimension, StringComparison.Ordinal))
            {
                continue;
            }

            var covers = period.FromYear <= year && (period.ToYear is null || year <= period.ToYear.Value);
            if (covers)
            {
                if (current is not null)
                {
                    throw new InvalidOperationException(
                        "Dimension '" + dimension + "' covers " + year.ToString(CultureInfo.InvariantCulture) + " twice.");
                }

                current = period;
            }

            if (period.ToYear == year - 1)
            {
                if (previous is not null)
                {
                    throw new InvalidOperationException(
                        "Dimension '" + dimension + "' has two steps ending just before " + year.ToString(CultureInfo.InvariantCulture) + ".");
                }

                previous = period;
            }
        }

        if (current is null)
        {
            throw new InvalidOperationException(
                "Dimension '" + dimension + "' does not cover " + year.ToString(CultureInfo.InvariantCulture) + ".");
        }

        return (current.Value, previous);
    }

    private static long ParseDollars(string text, string dimension, int year)
    {
        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            throw new InvalidOperationException(
                "Dimension '" + dimension + "' in " + year.ToString(CultureInfo.InvariantCulture) + " is not a non-negative dollar amount.");
        }

        return value;
    }
}

/// <summary>Where era facts come from. The authored timeline is the production source; tests pass a fixed year.</summary>
public interface IEraFinanceSource
{
    EraFinanceFacts Facts(int season);
}

/// <summary>Era facts from authored <see cref="RulePeriod"/> rows.</summary>
public sealed class EraPeriodFinance : IEraFinanceSource
{
    private readonly IReadOnlyList<RulePeriod> _periods;

    public EraPeriodFinance(IReadOnlyList<RulePeriod> periods)
    {
        ArgumentNullException.ThrowIfNull(periods);
        _periods = periods;
    }

    public EraFinanceFacts Facts(int season) => EraFinance.ForYear(_periods, season);
}
