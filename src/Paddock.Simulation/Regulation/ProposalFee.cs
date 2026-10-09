using Paddock.Domain.Finance;

namespace Paddock.Simulation.Regulation;

/// <summary>
/// The fee for one proposal (owner decision 7): a share of the team's revenue of the last completed season, never of its cash or
/// budget, so waiting for the day cash is lowest pays nothing less. The basis comes only from ledger lines of the season before,
/// which no later posting can change, so the fee is the same on every day of the season.
/// </summary>
public static class ProposalFee
{
    /// <summary>
    /// The revenue of <paramref name="season"/> - 1: start money, prize money and sponsors posted in that season. A team with no
    /// completed season (a career in its first season, or a new team) is measured by its opening ledger, the first line of owner
    /// funds, which is what the team was given to run a season on (an ESTIMATE of its yearly revenue).
    /// </summary>
    public static long RevenueBasis(IReadOnlyList<LedgerEntry> entries, int season)
    {
        ArgumentNullException.ThrowIfNull(entries);
        long revenue = 0;
        long opening = 0;
        foreach (var entry in entries)
        {
            if (entry.Date.Year == season - 1
                && entry.AmountCents > 0
                && entry.Category is LedgerCategories.StartMoney or LedgerCategories.PrizeMoney or LedgerCategories.Sponsor)
            {
                revenue = checked(revenue + entry.AmountCents);
            }

            if (opening == 0 && entry.Category == LedgerCategories.OwnerFunds && entry.AmountCents > 0)
            {
                opening = entry.AmountCents;
            }
        }

        return revenue > 0 ? revenue : opening;
    }

    /// <summary>The floor: a share of the era's typical budget, and never below <see cref="RegulationEstimates.MinimumFeeCents"/>.</summary>
    public static long Floor(long typicalBudgetCents) =>
        Math.Max(RegulationEstimates.MinimumFeeCents, Money.RoundCents(Math.Max(0, typicalBudgetCents) * RegulationEstimates.FeeFloorShareOfTypicalBudget));

    /// <summary>The fee in cents: the share of <paramref name="basisCents"/>, not below the floor. Never zero or negative.</summary>
    public static long Quote(long basisCents, long typicalBudgetCents) =>
        Math.Max(Floor(typicalBudgetCents), Money.RoundCents(Math.Max(0, basisCents) * RegulationEstimates.FeeRevenueShare));
}
