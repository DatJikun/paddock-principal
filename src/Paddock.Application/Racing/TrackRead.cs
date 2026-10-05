using Paddock.Application.Career;
using Paddock.Domain.Racing;
using Paddock.Simulation.Career;

namespace Paddock.Application.Racing;

/// <summary>One control point of a layout's centre line, in metres (x east, y north). Point 0 sits on the start line.</summary>
public sealed record TrackPointView(double X, double Y);

/// <summary>
/// Public facts about one layout, as the authored circuit catalog lists them. The host builds these from its data;
/// this layer reads no files. <see cref="Points"/> is empty when the layout has no authored geometry.
/// </summary>
public sealed record TrackFacts(
    string LayoutId,
    string CircuitId,
    string Name,
    string Country,
    double LengthKm,
    IReadOnlyList<string> Character,
    IReadOnlyList<TrackPointView> Points);

/// <summary>A finished round at this circuit in this career: who won it and for whom.</summary>
public sealed record TrackWinnerView(int Season, int Round, string DriverId, string DriverName, string TeamId, string TeamName);

/// <summary>
/// A circuit page: the layout's public facts and what this career has stored about races there.
/// <see cref="Retirements"/> counts unclassified cars over <see cref="Races"/>; both are zero before the first race.
/// </summary>
public sealed record TrackView(
    bool Found,
    string? LayoutId,
    string? CircuitId,
    string? Name,
    string? Country,
    double? LengthKm,
    IReadOnlyList<string> Character,
    IReadOnlyList<TrackPointView> Points,
    int Races,
    int Retirements,
    IReadOnlyList<TrackWinnerView> Winners);

/// <summary>Reads a circuit page. No state change and no RNG (INV-005); results come from the career's own archive.</summary>
public static class TrackRead
{
    public static TrackView Read(
        CareerSession session,
        IReadOnlyDictionary<string, TrackFacts> layouts,
        string? layoutId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(layouts);
        var wanted = string.IsNullOrWhiteSpace(layoutId) ? NextLayout(session) : layoutId;
        if (wanted is null || !layouts.TryGetValue(wanted, out var facts))
        {
            return new TrackView(false, wanted, null, null, null, null, [], [], 0, 0, []);
        }

        var archive = session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName);
        var winners = new List<TrackWinnerView>();
        var races = 0;
        var retirements = 0;
        if (archive is not null)
        {
            foreach (var race in archive.Races)
            {
                if (!layouts.TryGetValue(race.LayoutId, out var raced) || raced.CircuitId != facts.CircuitId)
                {
                    continue;
                }

                races++;
                foreach (var row in race.Rows)
                {
                    if (!row.Classified)
                    {
                        retirements++;
                    }
                }

                var winner = race.Rows.FirstOrDefault(row => row.Classified && row.Position == 1);
                if (winner is not null)
                {
                    var result = ChampionshipRead.Result(session, race.Season, race.Round).Rows
                        .First(row => row.Position == winner.Position && row.DriverId == winner.DriverId);
                    winners.Add(new TrackWinnerView(race.Season, race.Round, result.DriverId, result.DriverName, result.TeamId, result.TeamName));
                }
            }
        }

        winners.Reverse();
        return new TrackView(
            true,
            facts.LayoutId,
            facts.CircuitId,
            facts.Name,
            facts.Country,
            facts.LengthKm,
            facts.Character,
            facts.Points,
            races,
            retirements,
            winners);
    }

    private static string? NextLayout(CareerSession session)
    {
        var next = ChampionshipRead.Next(session, new Dictionary<string, CircuitLabel>(StringComparer.Ordinal));
        return next.LayoutId;
    }
}
