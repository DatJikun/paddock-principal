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
        var margin = TitleMargin(season.Constructors.Select(row => (row.Position, row.Points)));
        var spread = season.Races == 0 ? 0.0 : season.DistinctWinningOrganizations.Count / (double)season.Races;
        var close = (1.0 - margin) * spread;
        var dominant = margin * (1.0 - spread);
        var driverFight = DriverFight(season.Drivers);
        var delta = (FinanceEstimates.CloseFightGain * close)
            - (FinanceEstimates.DominanceLoss * dominant)
            + (FinanceEstimates.DriverFightGain * driverFight);
        var milli = (int)Math.Round((current + delta) * 1000.0, MidpointRounding.AwayFromZero);
        return Math.Clamp(milli, FinanceEstimates.MinPopularityMilli, FinanceEstimates.MaxPopularityMilli);
    }

    /// <summary>
    /// 1 when two drivers are level on points, 0 when the table has fewer than two drivers or the leader's team-mate is nowhere.
    /// A missing table does not count as a fight.
    /// </summary>
    private static double DriverFight(IReadOnlyList<DriverTitleRow> drivers)
    {
        if (drivers.Count < 2)
        {
            return 0;
        }

        return 1.0 - TitleMargin(drivers.Select(row => (row.Position, row.Points)));
    }

    /// <summary>0 when the leader and the second are level (or the table is too thin). 1 when the second scored nothing.</summary>
    private static double TitleMargin(IEnumerable<(int Position, decimal Points)> table)
    {
        (int Position, decimal Points)? leader = null;
        (int Position, decimal Points)? second = null;
        foreach (var row in table)
        {
            if (leader is null || row.Position < leader.Value.Position)
            {
                second = leader;
                leader = row;
            }
            else if (second is null || row.Position < second.Value.Position)
            {
                second = row;
            }
        }

        if (leader is null || second is null || leader.Value.Points <= 0)
        {
            return 0;
        }

        var gap = leader.Value.Points - second.Value.Points;
        if (gap < 0)
        {
            gap = 0;
        }

        var ratio = (double)(gap / leader.Value.Points);
        if (ratio < 0)
        {
            return 0;
        }

        return ratio > 1 ? 1 : ratio;
    }
}
