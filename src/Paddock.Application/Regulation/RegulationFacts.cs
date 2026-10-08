using System.Globalization;
using Paddock.Domain.Finance;
using Paddock.Domain.Racing;
using Paddock.Domain.World;
using Paddock.Simulation.Regulation;

namespace Paddock.Application.Regulation;

/// <summary>
/// What an AI team and the FIA may read about a series: the public table, the public results and the public finances (insolvency is
/// news; a rival's exact balance is not, so the FIA reads only the sign). Never a hidden attribute, never the future (INV-003).
/// </summary>
public static class RegulationFacts
{
    /// <summary>ESTIMATE: the share of cars that fail to finish that is normal for racing; above it the FIA sees attrition.</summary>
    public const double NormalAttrition = 0.3;

    /// <summary>ESTIMATE: how far above <see cref="NormalAttrition"/> the FIA reads full pressure.</summary>
    public const double AttritionSpan = 0.5;

    /// <summary>The constructors' points of the table the series reads: this season's races so far, otherwise the last season raced.</summary>
    public sealed record PublicTable(int Season, IReadOnlyList<(string TeamId, decimal Points)> Ordered, double Attrition)
    {
        public static PublicTable Empty { get; } = new(0, [], 0);
    }

    public static PublicTable TableOf(WorldState world, int season)
    {
        ArgumentNullException.ThrowIfNull(world);
        var archive = world.Section<RaceResultsSection>(RaceResultsSection.SectionName);
        if (archive is null)
        {
            return PublicTable.Empty;
        }

        foreach (var candidate in new[] { season, season - 1 })
        {
            var points = new SortedDictionary<string, decimal>(StringComparer.Ordinal);
            var rows = 0;
            var retired = 0;
            foreach (var race in archive.Races)
            {
                if (race.Season != candidate)
                {
                    continue;
                }

                foreach (var row in race.Rows)
                {
                    rows++;
                    if (!row.Classified)
                    {
                        retired++;
                    }

                    points.TryGetValue(row.TeamId, out var sum);
                    points[row.TeamId] = sum + (decimal.TryParse(row.Points, NumberStyles.Number, CultureInfo.InvariantCulture, out var carPoints) ? carPoints : 0m);
                }
            }

            if (rows == 0)
            {
                continue;
            }

            var ordered = points
                .Select(pair => (TeamId: pair.Key, Points: pair.Value))
                .OrderByDescending(pair => pair.Points)
                .ThenBy(pair => pair.TeamId, StringComparer.Ordinal)
                .ToArray();
            return new PublicTable(candidate, ordered, retired / (double)rows);
        }

        return PublicTable.Empty;
    }

    /// <summary>The place of every team in the table: those with points in order, the rest after them in ordinal order of their ids.</summary>
    public static IReadOnlyDictionary<string, int> Positions(PublicTable table, IReadOnlyList<string> teamIds)
    {
        var positions = new Dictionary<string, int>(teamIds.Count, StringComparer.Ordinal);
        var place = 0;
        var known = new HashSet<string>(teamIds, StringComparer.Ordinal);
        foreach (var (teamId, _) in table.Ordered)
        {
            if (known.Contains(teamId) && !positions.ContainsKey(teamId))
            {
                positions[teamId] = ++place;
            }
        }

        foreach (var teamId in teamIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            if (!positions.ContainsKey(teamId))
            {
                positions[teamId] = ++place;
            }
        }

        return positions;
    }

    /// <summary>0 for the last place, 1 for the first, 0.5 when there is no table to read.</summary>
    public static double Strength(PublicTable table, int position, int teams) =>
        table.Season == 0 || teams <= 1 ? 0.5 : 1.0 - ((position - 1) / (double)(teams - 1));

    /// <summary>
    /// How tight a team's money is, 0 to 1: in debt is 1, and a balance of a year's revenue or more is 0. Reads only the team's own ledger.
    /// </summary>
    public static double CashStress(FinanceSection finance, OrganizationId team, long revenueBasisCents)
    {
        if (!finance.HasBook(team))
        {
            return 0.5;
        }

        var balance = finance.BalanceOf(team);
        if (balance <= 0)
        {
            return 1.0;
        }

        var ratio = balance / (double)Math.Max(1, revenueBasisCents);
        return Math.Clamp(1.0 - ratio, 0.0, 1.0);
    }

    public static TeamView ViewOf(
        FinanceSection finance,
        PublicTable table,
        IReadOnlyDictionary<string, int> positions,
        Organization team,
        int season,
        string? country)
    {
        var basis = finance.HasBook(team.Id) ? ProposalFee.RevenueBasis(finance.EntriesOf(team.Id), season) : 0;
        return new TeamView(
            team.Id.Value,
            Strength(table, positions.TryGetValue(team.Id.Value, out var position) ? position : positions.Count, positions.Count),
            CashStress(finance, team.Id, basis),
            country);
    }

    public static FiaPressures PressuresOf(PublicTable table, FinanceSection finance, IReadOnlyList<Organization> teams)
    {
        double dominance = 0;
        if (table.Season != 0 && table.Ordered.Count > 1)
        {
            var total = table.Ordered.Sum(entry => entry.Points);
            if (total > 0)
            {
                var share = (double)(table.Ordered[0].Points / total);
                var fair = 1.0 / table.Ordered.Count;
                dominance = Math.Clamp((share - fair) / (1.0 - fair), 0.0, 1.0);
            }
        }

        var attrition = table.Season == 0 ? 0 : Math.Clamp((table.Attrition - NormalAttrition) / AttritionSpan, 0.0, 1.0);
        var inDebt = 0;
        var counted = 0;
        foreach (var team in teams)
        {
            if (!finance.HasBook(team.Id))
            {
                continue;
            }

            counted++;
            if (finance.BalanceOf(team.Id) < 0)
            {
                inDebt++;
            }
        }

        return new FiaPressures(dominance, attrition, counted == 0 ? 0 : inDebt / (double)counted);
    }
}
