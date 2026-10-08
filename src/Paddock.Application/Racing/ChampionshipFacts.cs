using Paddock.Application.Career;
using Paddock.Domain.Objectives;
using Paddock.Domain.Racing;
using Paddock.Domain.World;
using Paddock.Simulation.Career;
using Paddock.Simulation.Racing.Points;

namespace Paddock.Application.Racing;

/// <summary>
/// Championship facts an objective can read (T36). A pure query: no state change and no RNG (INV-005). Unknown when the
/// team has not appeared in this season's table.
/// </summary>
public static class ChampionshipFacts
{
    public static decimal? Number(CareerSession session, CareerInputs inputs, OrganizationId owner, string factKey)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(inputs);
        var section = session.World.Section<ChampionshipSection>(ChampionshipSection.SectionName);
        if (section is null || section.Season != session.Date.Year)
        {
            return null;
        }

        var rules = Rules(inputs, session.World, section.Season);
        if (rules is null)
        {
            return null;
        }

        var standings = Standings.Restore(
            PointsRules.For(rules),
            section.TotalRounds,
            section.RoundsCompleted,
            Snapshots(section.Drivers),
            Snapshots(section.Constructors));
        StandingsRow? row = null;
        foreach (var candidate in standings.Constructors())
        {
            if (candidate.Id == owner.Value)
            {
                row = candidate;
                break;
            }
        }

        if (row is null)
        {
            return null;
        }

        return factKey switch
        {
            ObjectiveFactKeys.ChampionshipPosition => row.Position,
            ObjectiveFactKeys.SeasonPoints => row.CountedPoints,
            ObjectiveFactKeys.SeasonPodiums => Podiums(section, owner.Value),
            ObjectiveFactKeys.SeasonWins => Wins(section, owner.Value),
            _ => null,
        };
    }

    private static int Wins(ChampionshipSection section, string organization)
    {
        foreach (var row in section.Constructors)
        {
            if (row.Id == organization)
            {
                return row.Positions.Count(position => position == 1);
            }
        }

        return 0;
    }

    private static int Podiums(ChampionshipSection section, string organization)
    {
        foreach (var row in section.Constructors)
        {
            if (row.Id != organization)
            {
                continue;
            }

            var podiums = 0;
            foreach (var position in row.Positions)
            {
                if (position is >= 1 and <= 3)
                {
                    podiums++;
                }
            }

            return podiums;
        }

        return 0;
    }

    private static RuleSet? Rules(CareerInputs inputs, WorldState world, int season)
    {
        var stored = world.Section<RegulationsSection>(RegulationsSection.SectionName);
        if (stored is not null && stored.Season == season)
        {
            return stored.ToRuleSet();
        }

        if (inputs.RegulationDimensionIds is not { } dimensions || inputs.RulePeriods is not { } periods)
        {
            return null;
        }

        return RuleSet.For(season, dimensions, periods);
    }

    private static Standings.LedgerSnapshot[] Snapshots(IReadOnlyList<ChampionshipLedger> rows)
    {
        var snapshots = new Standings.LedgerSnapshot[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            snapshots[i] = new Standings.LedgerSnapshot(rows[i].Id, [.. rows[i].RoundPoints], [.. rows[i].Positions]);
        }

        return snapshots;
    }
}
