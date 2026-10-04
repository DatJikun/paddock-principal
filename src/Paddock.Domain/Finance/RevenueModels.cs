using Paddock.Domain.World;

namespace Paddock.Domain.Finance;

/// <summary>
/// What a race pays and costs. Doubles stay inside this type; each amount is rounded once to cents.
/// <see cref="WarningKey"/> is set when the era model fell back to <see cref="FinanceEstimates.PoolByPositionModel"/> (ESTIMATE).
/// </summary>
public sealed record RevenueReport(
    string RequestedModel,
    string AppliedModel,
    bool UsedFallback,
    string? WarningKey,
    IReadOnlyList<LedgerDraft> Postings);

public static class RevenueModels
{
    public const string FallbackWarningKey = "finance.revenue.fallback_estimate";

    public static RevenueReport Quote(RaceResultsPublished race, long typicalCents, int popularityMilli)
    {
        ArgumentNullException.ThrowIfNull(race);
        ArgumentOutOfRangeException.ThrowIfNegative(typicalCents);
        var popularity = popularityMilli / 1000.0;
        var promoter = string.Equals(race.RevenueModel, FinanceEstimates.PromoterModel, StringComparison.Ordinal);
        var applied = promoter ? FinanceEstimates.PromoterModel : FinanceEstimates.PoolByPositionModel;
        var drafts = new List<LedgerDraft>();
        var entries = race.Entries
            .OrderBy(entry => entry.Organization.Value, StringComparer.Ordinal)
            .ThenBy(entry => entry.DriverId, StringComparer.Ordinal)
            .ThenBy(entry => entry.Position)
            .ToArray();

        if (promoter)
        {
            var start = PerRaceCents(typicalCents, FinanceEstimates.StartMoneyShare, popularity, race.RacesInSeason);
            foreach (var entry in entries)
            {
                if (start != 0)
                {
                    drafts.Add(new LedgerDraft(entry.Organization, LedgerCategories.StartMoney, entry.DriverId, start, FinanceReason.StartMoney));
                }
            }
        }

        var poolShare = promoter ? FinanceEstimates.PrizeMoneyShare : FinanceEstimates.FallbackPoolShare;
        var pool = PerRaceCents(typicalCents, poolShare, popularity, race.RacesInSeason);
        drafts.AddRange(SplitPrize(entries, pool));

        var running = PerRaceCents(typicalCents, FinanceEstimates.RaceRunningShare, popularity, race.RacesInSeason);
        if (running != 0)
        {
            foreach (var organization in entries.Select(entry => entry.Organization.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                var id = entries.First(entry => entry.Organization.Value == organization).Organization;
                drafts.Add(new LedgerDraft(id, LedgerCategories.RaceRunning, null, -running, FinanceReason.RaceRunning));
            }
        }

        return new RevenueReport(
            race.RevenueModel,
            applied,
            !promoter,
            promoter ? null : FallbackWarningKey,
            drafts);
    }

    /// <summary>Weight of a classified place. ESTIMATE: 6, 5, 4, 3, 2, 1 for places 1–6, otherwise 0.</summary>
    public static int PrizeWeight(int position, bool classified)
    {
        if (!classified || position < 1 || position > FinanceEstimates.PrizePositions)
        {
            return 0;
        }

        return FinanceEstimates.PrizePositions + 1 - position;
    }

    public static int WeightSum => FinanceEstimates.PrizePositions * (FinanceEstimates.PrizePositions + 1) / 2;

    private static long PerRaceCents(long typicalCents, double share, double popularity, int races)
    {
        var raw = typicalCents * share * popularity / races;
        return Money.RoundCents(raw);
    }

    private static List<LedgerDraft> SplitPrize(IReadOnlyList<RaceEntryResult> entries, long pool)
    {
        var drafts = new List<LedgerDraft>();
        if (pool == 0)
        {
            return drafts;
        }

        var weights = entries.Select(entry => PrizeWeight(entry.Position, entry.Classified)).ToArray();
        var sum = 0;
        foreach (var weight in weights)
        {
            sum += weight;
        }

        if (sum == 0)
        {
            return drafts;
        }

        long given = 0;
        var lastWeighted = -1;
        for (var i = 0; i < weights.Length; i++)
        {
            if (weights[i] > 0)
            {
                lastWeighted = i;
            }
        }

        for (var i = 0; i < entries.Count; i++)
        {
            if (weights[i] == 0)
            {
                continue;
            }

            long share;
            if (i == lastWeighted)
            {
                share = pool - given;
            }
            else
            {
                share = pool * weights[i] / sum;
                given += share;
            }

            if (share != 0)
            {
                drafts.Add(new LedgerDraft(entries[i].Organization, LedgerCategories.PrizeMoney, entries[i].DriverId, share, FinanceReason.PrizeMoney));
            }
        }

        return drafts;
    }
}

/// <summary>Reason keys written on ledger lines. The same literals carry <c>[TranslationKey]</c> in the application layer.</summary>
public static class FinanceReason
{
    public const string OpeningCapital = "finance.reason.opening_capital";

    public const string StartMoney = "finance.reason.start_money";

    public const string PrizeMoney = "finance.reason.prize_money";

    public const string RaceRunning = "finance.reason.race_running";

    public const string Salary = "finance.reason.salary";

    public const string Termination = "finance.reason.termination";
}
