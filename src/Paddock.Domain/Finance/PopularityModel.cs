using Paddock.Domain.World;

namespace Paddock.Domain.Finance;

/// <summary>
/// Global sport popularity (PP-025, PP-050: global only). The index is thousandths so the hash does not depend on
/// binary floating point. A close title fight raises it; a dominant season lowers it. Clamped to
/// <see cref="FinanceEstimates.MinPopularityMilli"/>–<see cref="FinanceEstimates.MaxPopularityMilli"/> (ESTIMATE).
/// </summary>
public static class PopularityModel
{
    public static int Next(int currentMilli, SeasonEnded season)
    {
        ArgumentNullException.ThrowIfNull(season);
        var current = currentMilli / 1000.0;
        var margin = TitleMargin(season.Constructors);
        var spread = season.Races == 0 ? 0.0 : season.DistinctWinningOrganizations.Count / (double)season.Races;
        var close = (1.0 - margin) * spread;
        var dominant = margin * (1.0 - spread);
        var delta = (FinanceEstimates.CloseFightGain * close) - (FinanceEstimates.DominanceLoss * dominant);
        var milli = (int)Math.Round((current + delta) * 1000.0, MidpointRounding.AwayFromZero);
        return Math.Clamp(milli, FinanceEstimates.MinPopularityMilli, FinanceEstimates.MaxPopularityMilli);
    }

    /// <summary>0 when the leader and the second are level (or the table is too thin). 1 when the second scored nothing.</summary>
    private static double TitleMargin(IReadOnlyList<ConstructorTitleRow> table)
    {
        ConstructorTitleRow? leader = null;
        ConstructorTitleRow? second = null;
        foreach (var row in table)
        {
            if (leader is null || row.Position < leader.Position)
            {
                second = leader;
                leader = row;
            }
            else if (second is null || row.Position < second.Position)
            {
                second = row;
            }
        }

        if (leader is null || second is null || leader.Points <= 0)
        {
            return 0;
        }

        var gap = leader.Points - second.Points;
        if (gap < 0)
        {
            gap = 0;
        }

        var ratio = (double)(gap / leader.Points);
        if (ratio < 0)
        {
            return 0;
        }

        return ratio > 1 ? 1 : ratio;
    }
}
