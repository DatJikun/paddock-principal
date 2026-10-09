using System.Globalization;
using System.Text.Json;
using Paddock.Domain.Racing;

namespace Paddock.Data.Historical;

/// <summary>One season of a real driver's past, as plain counts. No ranking and no comparison with anything (PP-062).</summary>
public sealed record HistoricalSeasonLine(
    int Season,
    string ConstructorId,
    string ConstructorName,
    int Starts,
    int Wins,
    int Podiums,
    int Retirements,
    int? Best,
    string Points = "0",
    int? Place = null);

/// <summary>
/// The seasons real drivers raced before a career starts, counted from the local Jolpica cache (PP-041: local only, never
/// committed). The Indianapolis 500 rounds of 1950 to 1960 are left out, as they were not Formula 1 races. A missing cache is not an
/// error: the history is empty and a driver's profile simply starts with the career's own seasons (#265).
/// </summary>
public sealed class DriverCareerHistory
{
    private readonly Dictionary<string, List<HistoricalSeasonLine>> _byDriver;

    private DriverCareerHistory(Dictionary<string, List<HistoricalSeasonLine>> byDriver) => _byDriver = byDriver;

    public static DriverCareerHistory Empty { get; } = new([]);

    public static DriverCareerHistory Load(string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        var folder = Path.Combine(Path.GetFullPath(dataRoot), "cache", "jolpica", "normalized");
        var results = Path.Combine(folder, "results.json");
        var races = Path.Combine(folder, "races.json");
        if (!File.Exists(results) || !File.Exists(races))
        {
            return Empty;
        }

        var constructors = Path.Combine(folder, "constructors.json");
        return Parse(
            File.ReadAllText(results),
            File.ReadAllText(races),
            File.Exists(constructors) ? File.ReadAllText(constructors) : null);
    }

    public static DriverCareerHistory Parse(string resultsJson, string racesJson, string? constructorsJson)
    {
        ArgumentNullException.ThrowIfNull(resultsJson);
        ArgumentNullException.ThrowIfNull(racesJson);
        var results = JsonSerializer.Deserialize<HistoricalResultsDocument>(resultsJson, HistoricalJson.Options)
            ?? throw new JsonException("The results file is empty.");
        var races = JsonSerializer.Deserialize<HistoricalRacesDocument>(racesJson, HistoricalJson.Options)
            ?? throw new JsonException("The races file is empty.");
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        if (constructorsJson is not null)
        {
            var document = JsonSerializer.Deserialize<HistoricalConstructorsDocument>(constructorsJson, HistoricalJson.Options);
            foreach (var constructor in document?.Constructors ?? [])
            {
                names[constructor.ConstructorId] = constructor.Name;
            }
        }

        var indy = new HashSet<(int, int)>();
        foreach (var race in races.Races)
        {
            if (race.IsIndianapolis500)
            {
                indy.Add((race.Season, race.Round));
            }
        }

        var tallies = new SortedDictionary<(string Driver, int Season), Tally>(Comparer<(string Driver, int Season)>.Create(
            static (left, right) =>
            {
                var driver = string.CompareOrdinal(left.Driver, right.Driver);
                return driver != 0 ? driver : left.Season.CompareTo(right.Season);
            }));
        foreach (var row in results.Results)
        {
            if (indy.Contains((row.Season, row.Round)))
            {
                continue;
            }

            var key = (row.DriverId, row.Season);
            if (!tallies.TryGetValue(key, out var tally))
            {
                tally = new Tally();
                tallies.Add(key, tally);
            }

            tally.Add(row);
        }

        // The place is read over the whole field of the season, so every driver's total goes in first (#331).
        var places = SeasonPlaces.Of(tallies.Select(entry => new SeasonTotal(
            entry.Key.Season,
            entry.Key.Driver,
            entry.Value.Points,
            entry.Value.Wins,
            entry.Value.Podiums)));

        var byDriver = new Dictionary<string, List<HistoricalSeasonLine>>(StringComparer.Ordinal);
        foreach (var ((driver, season), tally) in tallies)
        {
            if (!byDriver.TryGetValue(driver, out var list))
            {
                byDriver[driver] = list = [];
            }

            list.Add(new HistoricalSeasonLine(
                season,
                tally.ConstructorId,
                names.TryGetValue(tally.ConstructorId, out var name) ? name : tally.ConstructorId,
                tally.Starts,
                tally.Wins,
                tally.Podiums,
                tally.Retirements,
                tally.Best,
                tally.Points.ToString("0.##########", CultureInfo.InvariantCulture),
                places[(season, driver)]));
        }

        return new DriverCareerHistory(byDriver);
    }

    /// <summary>The seasons of a driver before <paramref name="beforeSeason"/>, oldest first. Empty for a driver the data does not know.</summary>
    public IReadOnlyList<HistoricalSeasonLine> Before(string driverId, int beforeSeason)
    {
        ArgumentNullException.ThrowIfNull(driverId);
        return _byDriver.TryGetValue(driverId, out var list)
            ? list.Where(line => line.Season < beforeSeason).ToArray()
            : [];
    }

    private sealed class Tally
    {
        public string ConstructorId { get; private set; } = "";

        public int Starts { get; private set; }

        public int Wins { get; private set; }

        public int Podiums { get; private set; }

        public int Retirements { get; private set; }

        public int? Best { get; private set; }

        public decimal Points { get; private set; }

        public void Add(HistoricalResult row)
        {
            ConstructorId = row.ConstructorId;
            Starts++;
            Points += row.Points;
            if (!row.IsClassified)
            {
                Retirements++;
                return;
            }

            if (row.Position == 1)
            {
                Wins++;
            }

            if (row.Position <= 3)
            {
                Podiums++;
            }

            Best = Best is int best ? Math.Min(best, row.Position) : row.Position;
        }
    }
}
