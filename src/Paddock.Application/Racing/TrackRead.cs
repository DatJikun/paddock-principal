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

/// <summary>One podium place of a past race at a circuit. The team id is a constructor id, the key the livery table knows.</summary>
public sealed record PastPodiumView(int Position, string DriverName, string Nationality, string TeamId, string TeamName);

/// <summary>The two origins of a <see cref="PastRaceView"/>.</summary>
public static class PastRaceSource
{
    /// <summary>A round this career ran.</summary>
    public const string Career = "career";

    /// <summary>A real race from before the career began, read from the local record of past seasons.</summary>
    public const string Archive = "archive";
}

/// <summary>A past race at a circuit: the top three, and where the record comes from.</summary>
public sealed record PastRaceView(int Season, string Source, IReadOnlyList<PastPodiumView> Podium);

/// <summary>
/// A real race from the local record of past seasons, already reduced to its circuit and podium. The host reads the
/// files (PP-041: local only) and only passes seasons before the career began; this layer receives none without the cache.
/// </summary>
public sealed record HistoricalRaceFact(int Season, string CircuitId, IReadOnlyList<PastPodiumView> Podium);

/// <summary>
/// A circuit page: the layout's public facts and what this career has stored about races there.
/// <see cref="Retirements"/> counts unclassified cars over <see cref="Races"/>; both are zero before the first race.
/// <see cref="Past"/> lists earlier races at the circuit, newest first: this career's own rounds, then the real races
/// from before the career began when the host has them. Results only, no comparison (PP-062).
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
    IReadOnlyList<TrackWinnerView> Winners,
    IReadOnlyList<PastRaceView> Past);

/// <summary>Reads a circuit page. No state change and no RNG (INV-005); results come from the career's own archive.</summary>
public static class TrackRead
{
    public static TrackView Read(
        CareerSession session,
        IReadOnlyDictionary<string, TrackFacts> layouts,
        string? layoutId,
        IReadOnlyList<HistoricalRaceFact>? history = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(layouts);
        var wanted = string.IsNullOrWhiteSpace(layoutId) ? NextLayout(session) : layoutId;
        if (wanted is null || !layouts.TryGetValue(wanted, out var facts))
        {
            return new TrackView(false, wanted, null, null, null, null, [], [], 0, 0, [], []);
        }

        var archive = session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName);
        var winners = new List<TrackWinnerView>();
        var past = new List<PastRaceView>();
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
                    var rows = ChampionshipRead.Result(session, race.Season, race.Round).Rows;
                    var result = rows.First(row => row.Position == winner.Position && row.DriverId == winner.DriverId);
                    winners.Add(new TrackWinnerView(race.Season, race.Round, result.DriverId, result.DriverName, result.TeamId, result.TeamName));
                    past.Add(new PastRaceView(
                        race.Season,
                        PastRaceSource.Career,
                        rows.Where(row => row.Classified && row.Position <= 3)
                            .OrderBy(row => row.Position)
                            .Select(row => new PastPodiumView(row.Position, row.DriverName, row.Nationality, row.TeamId, row.TeamName))
                            .ToArray()));
                }
            }
        }

        winners.Reverse();
        past.Reverse();
        if (history is not null)
        {
            foreach (var fact in history.Where(item => item.CircuitId == facts.CircuitId).OrderByDescending(item => item.Season))
            {
                past.Add(new PastRaceView(fact.Season, PastRaceSource.Archive, fact.Podium));
            }
        }

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
            winners,
            past);
    }

    private static string? NextLayout(CareerSession session)
    {
        var next = ChampionshipRead.Next(session, new Dictionary<string, CircuitLabel>(StringComparer.Ordinal));
        return next.LayoutId;
    }
}
