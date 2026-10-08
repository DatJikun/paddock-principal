using Paddock.Application.Career;
using Paddock.Domain.Racing;
using Paddock.Simulation.Career;

namespace Paddock.Application.Racing;

/// <summary>One round column of the season grid: where it was held and whether it has been run.</summary>
public sealed record OverviewRoundView(int Round, string CircuitName, string Country, bool Finished);

/// <summary>
/// How one car ended one round. <see cref="RetirementKey"/> is the report key of the reason (empty when classified).
/// <see cref="Points"/> is what the car scored in invariant text.
/// </summary>
public sealed record OverviewResultView(int Position, bool Classified, string RetirementKey, string Points);

/// <summary>
/// One cell of the grid. A driver has no result or one; a constructor has one per car that raced, best place first.
/// A round that has not been run, or that the entry skipped, has no results.
/// </summary>
public sealed record OverviewCellView(int Round, IReadOnlyList<OverviewResultView> Results);

/// <summary>
/// One row of the grid: a driver (nationality and team of his latest round filled) or a constructor (both empty).
/// <see cref="Position"/> and <see cref="Points"/> are the championship's, so the era's counting rule applies.
/// </summary>
public sealed record OverviewRowView(
    string Id,
    string Name,
    string Nationality,
    string? TeamId,
    string? TeamName,
    int Position,
    string Points,
    IReadOnlyList<OverviewCellView> Cells);

/// <summary>The whole season at a glance: rounds as columns, drivers and constructors as rows. Public results only (INV-003).</summary>
public sealed record SeasonOverviewView(
    int Season,
    IReadOnlyList<OverviewRoundView> Rounds,
    IReadOnlyList<OverviewRowView> Drivers,
    IReadOnlyList<OverviewRowView> Constructors,
    bool HasConstructorTitle);

/// <summary>
/// Reads the season grid from the stored rounds and the standings. No state change and no RNG (INV-005); the grid is
/// built from the same stored results the race page shows, so the two never disagree.
/// </summary>
public static class SeasonOverviewRead
{
    public static SeasonOverviewView Read(CareerSession session, CareerInputs inputs, IReadOnlyDictionary<string, CircuitLabel> circuits)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(circuits);
        var calendar = ChampionshipRead.Calendar(session, inputs, circuits);
        var standings = ChampionshipRead.Standings(session, inputs);
        var archive = session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName);
        var races = archive is null
            ? new Dictionary<int, RaceResultView>()
            : archive.Races.Where(race => race.Season == calendar.Season)
                .ToDictionary(race => race.Round, race => ChampionshipRead.Result(session, race.Season, race.Round));
        var rounds = calendar.Rounds
            .Select(round => new OverviewRoundView(round.Round, round.CircuitName, round.Country, round.Finished))
            .ToArray();
        var order = rounds.Select(round => round.Round).ToArray();

        var drivers = new List<OverviewRowView>();
        foreach (var row in standings.Drivers)
        {
            drivers.Add(new OverviewRowView(row.Id, row.Name, row.Nationality, row.TeamId, row.TeamName, row.Position, row.Points, Cells(order, races, car => car.DriverId == row.Id)));
        }

        var known = new HashSet<string>(drivers.Select(row => row.Id), StringComparer.Ordinal);
        var missing = new List<OverviewRowView>();
        foreach (var round in races.OrderBy(pair => pair.Key))
        {
            foreach (var car in round.Value.Rows)
            {
                if (known.Add(car.DriverId))
                {
                    missing.Add(new OverviewRowView(car.DriverId, car.DriverName, car.Nationality, car.TeamId, car.TeamName, 0, "0", Cells(order, races, other => other.DriverId == car.DriverId)));
                }
            }
        }

        drivers.AddRange(missing);

        var titles = standings.Rules is { } rules && rules.Constructors != "NoChampionship";
        var constructors = new List<OverviewRowView>();
        if (titles)
        {
            foreach (var row in standings.Constructors)
            {
                constructors.Add(new OverviewRowView(row.Id, row.Name, "", null, null, row.Position, row.Points, Cells(order, races, car => car.TeamId == row.Id)));
            }
        }

        return new SeasonOverviewView(calendar.Season, rounds, drivers, constructors, titles);
    }

    private static OverviewCellView[] Cells(int[] order, Dictionary<int, RaceResultView> races, Func<RaceRowView, bool> pick)
    {
        var cells = new OverviewCellView[order.Length];
        for (var i = 0; i < order.Length; i++)
        {
            OverviewResultView[] results = races.TryGetValue(order[i], out var race)
                ? race.Rows.Where(pick).OrderBy(row => row.Position)
                    .Select(row => new OverviewResultView(row.Position, row.Classified, row.RetirementKey, row.Points)).ToArray()
                : [];
            cells[i] = new OverviewCellView(order[i], results);
        }

        return cells;
    }
}
