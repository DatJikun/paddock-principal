using System.Globalization;
using Paddock.Data.Historical;

namespace Paddock.DataPipeline;

/// <summary>
/// Historical race and championship figures for calibrating the race engine (PP-012).
/// Main figures exclude shared-drive rows and Indianapolis 500 rounds. Those two
/// populations are returned separately and are not mixed back in.
/// Sprint results are ignored: they are not Grand Prix rows.
/// </summary>
public static class EraStats
{
    public static EraStatsReport Build(NormalizedHistoricalData data, int fromYear, int toYear)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (fromYear > toYear)
        {
            throw new ArgumentOutOfRangeException(nameof(fromYear), "fromYear must not be greater than toYear.");
        }

        var races = data.Races.Races.Where(race => race.Season >= fromYear && race.Season <= toYear).ToList();
        var raceByKey = new Dictionary<(int Season, int Round), HistoricalRace>();
        foreach (var race in races)
        {
            raceByKey[(race.Season, race.Round)] = race;
        }

        var driverNames = data.Drivers.Drivers.ToDictionary(driver => driver.DriverId, DisplayDriver, StringComparer.Ordinal);
        var constructorNames = data.Constructors.Constructors.ToDictionary(
            constructor => constructor.ConstructorId,
            constructor => constructor.Name,
            StringComparer.Ordinal);

        var mainRows = new List<HistoricalResult>();
        var sharedRows = new List<HistoricalResult>();
        var indyRows = new List<HistoricalResult>();
        foreach (var row in data.Results.Results)
        {
            if (row.Season < fromYear || row.Season > toYear)
            {
                continue;
            }

            if (!raceByKey.TryGetValue((row.Season, row.Round), out var race))
            {
                throw new InvalidDataException(
                    $"Result {row.Season.ToString(CultureInfo.InvariantCulture)}/{row.Round.ToString(CultureInfo.InvariantCulture)} {row.DriverId} has no race.");
            }

            if (race.IsIndianapolis500)
            {
                indyRows.Add(row);
            }
            else if (row.IsSharedDrive)
            {
                sharedRows.Add(row);
            }
            else
            {
                mainRows.Add(row);
            }
        }

        var unmapped = new Dictionary<string, int>(StringComparer.Ordinal);
        var main = BuildPopulation(mainRows, unmapped, championship: true, races, driverNames, constructorNames);
        var shared = BuildPopulation(sharedRows, unmapped, championship: false, races, driverNames, constructorNames);
        var indy = BuildPopulation(indyRows, unmapped, championship: false, races, driverNames, constructorNames);
        var unmappedList = unmapped
            .Select(pair => new UnmappedStatusCount(pair.Key, pair.Value))
            .OrderBy(item => item.Status, StringComparer.Ordinal)
            .ToList();

        return new EraStatsReport(fromYear, toYear, unmappedList, main, shared, indy);
    }

    private static PopulationReport BuildPopulation(
        List<HistoricalResult> rows,
        Dictionary<string, int> unmapped,
        bool championship,
        List<HistoricalRace> races,
        Dictionary<string, string> driverNames,
        Dictionary<string, string> constructorNames)
    {
        var tallies = new List<RaceTally>();
        foreach (var group in rows.GroupBy(row => (row.Season, row.Round)).OrderBy(group => group.Key.Season).ThenBy(group => group.Key.Round))
        {
            tallies.Add(Tally(group.Key.Season, group.Key.Round, group.ToList(), unmapped));
        }

        var titles = championship
            ? Championships(rows, races, driverNames)
            : new Dictionary<int, SeasonTitle>();

        var seasons = new List<SeasonReport>();
        foreach (var seasonGroup in tallies.GroupBy(tally => tally.Season).OrderBy(group => group.Key))
        {
            titles.TryGetValue(seasonGroup.Key, out var title);
            seasons.Add(SeasonFrom(seasonGroup.Key, seasonGroup.ToList(), title, constructorNames));
        }

        var decades = new List<DecadeReport>();
        foreach (var decadeGroup in tallies.GroupBy(tally => tally.Season / 10 * 10).OrderBy(group => group.Key))
        {
            var seasonReports = seasons.Where(season => season.Season / 10 * 10 == decadeGroup.Key).ToList();
            decades.Add(DecadeFrom(decadeGroup.Key, decadeGroup.ToList(), seasonReports, constructorNames));
        }

        return new PopulationReport(seasons, decades);
    }

    private static RaceTally Tally(int season, int round, List<HistoricalResult> rows, Dictionary<string, int> unmapped)
    {
        var tally = new RaceTally(season, round);
        foreach (var row in rows)
        {
            tally.Entries++;
            var kind = FinishStatus.Classify(row.Status);
            if (kind == FinishStatus.Kind.NonStart)
            {
                tally.NonStarts++;
                NotePole(tally, row);
                continue;
            }

            if (row.IsClassified)
            {
                tally.Starters++;
                tally.Classified++;
                // A numeric positionText is still a classified finish. The status can
                // nevertheless name a failure (common when running at the end was not required).
                // Those rows stay inside classified % and are counted again here.
                if (kind == FinishStatus.Kind.Mechanical)
                {
                    tally.ClassifiedMechanical++;
                }
                else if (kind == FinishStatus.Kind.Accident)
                {
                    tally.ClassifiedAccident++;
                }

                NotePole(tally, row);
                continue;
            }

            tally.Starters++;
            switch (kind)
            {
                case FinishStatus.Kind.Mechanical:
                    tally.Mechanical++;
                    break;
                case FinishStatus.Kind.Accident:
                    tally.Accident++;
                    break;
                case FinishStatus.Kind.Other:
                    tally.Other++;
                    break;
                default:
                    tally.Unmapped++;
                    unmapped.TryGetValue(row.Status, out var count);
                    unmapped[row.Status] = count + 1;
                    tally.UnmappedByStatus.TryGetValue(row.Status, out var statusCount);
                    tally.UnmappedByStatus[row.Status] = statusCount + 1;
                    break;
            }

            NotePole(tally, row);
        }

        var winners = rows.Where(row => row.IsClassified && row.PositionText == "1").ToList();
        if (winners.Count == 1)
        {
            var winner = winners[0];
            tally.WinnerDriverId = winner.DriverId;
            tally.WinnerConstructorId = winner.ConstructorId;
            tally.WinnerFromPole = winner.Grid == 1;
            var seconds = rows.Where(row => row.IsClassified && row.PositionText == "2").ToList();
            if (seconds.Count == 1
                && winner.TimeMillis is long winnerMillis
                && seconds[0].TimeMillis is long secondMillis
                && secondMillis >= winnerMillis)
            {
                tally.MarginMillis = secondMillis - winnerMillis;
            }
        }

        return tally;
    }

    private static void NotePole(RaceTally tally, HistoricalResult row)
    {
        if (row.Grid == 1)
        {
            tally.PoleSitters.Add(row.DriverId);
        }
    }

    private static Dictionary<int, SeasonTitle> Championships(
        List<HistoricalResult> rows,
        List<HistoricalRace> races,
        Dictionary<string, string> driverNames)
    {
        var titles = new Dictionary<int, SeasonTitle>();
        var finalRoundBySeason = races
            .GroupBy(race => race.Season)
            .ToDictionary(group => group.Key, group => group.Max(race => race.Round));

        foreach (var seasonGroup in rows.GroupBy(row => row.Season))
        {
            var season = seasonGroup.Key;
            if (!finalRoundBySeason.TryGetValue(season, out var seasonFinalRound))
            {
                throw new InvalidDataException($"Season {season.ToString(CultureInfo.InvariantCulture)} has results but no races.");
            }

            var rule = ChampionshipRules.ForSeason(season);
            var roundsByDriver = new Dictionary<string, Dictionary<int, decimal>>(StringComparer.Ordinal);
            var finishes = new Dictionary<string, Dictionary<int, int>>(StringComparer.Ordinal);
            foreach (var row in seasonGroup)
            {
                if (!roundsByDriver.TryGetValue(row.DriverId, out var rounds))
                {
                    rounds = new Dictionary<int, decimal>();
                    roundsByDriver[row.DriverId] = rounds;
                }

                rounds.TryGetValue(row.Round, out var points);
                rounds[row.Round] = points + row.Points;
                if (!row.IsClassified || !int.TryParse(row.PositionText, NumberStyles.None, CultureInfo.InvariantCulture, out var place))
                {
                    continue;
                }

                if (!finishes.TryGetValue(row.DriverId, out var places))
                {
                    places = new Dictionary<int, int>();
                    finishes[row.DriverId] = places;
                }

                places.TryGetValue(place, out var placeCount);
                places[place] = placeCount + 1;
            }

            if (roundsByDriver.Count == 0)
            {
                continue;
            }

            var mainFinal = seasonGroup.Max(row => row.Round);
            var counted = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var pair in roundsByDriver)
            {
                counted[pair.Key] = ChampionshipRules.CountRounds(RoundList(pair.Value), rule);
            }

            // Highest counted points, then more wins, then more second places, and so on.
            // A remaining tie uses the driver id so the report does not depend on row order.
            var champion = counted.Keys.Aggregate((left, right) => BetterChampion(left, right, counted, finishes));

            var total = counted.Values.Sum();
            decimal? share = total == 0 ? null : counted[champion] / total;
            var maximum = ChampionshipRules.MaximumForRound(rule, mainFinal, seasonFinalRound);
            var preFinalLeaderPoints = decimal.MinValue;
            string? preFinalLeader = null;
            var tiedLead = false;
            foreach (var driver in counted.Keys)
            {
                var preFinal = ChampionshipRules.CountRounds(RoundList(roundsByDriver[driver], mainFinal), rule);
                if (preFinalLeader is null || preFinal > preFinalLeaderPoints)
                {
                    preFinalLeader = driver;
                    preFinalLeaderPoints = preFinal;
                    tiedLead = false;
                }
                else if (preFinal == preFinalLeaderPoints)
                {
                    tiedLead = true;
                }
            }

            var clinched = !tiedLead && preFinalLeader is not null && counted.Keys
                .Where(driver => driver != preFinalLeader)
                .All(driver =>
                {
                    var withFinal = RoundList(roundsByDriver[driver], mainFinal).ToList();
                    withFinal.Add((mainFinal, maximum));
                    return ChampionshipRules.CountRounds(withFinal, rule) < preFinalLeaderPoints;
                });

            driverNames.TryGetValue(champion, out var name);
            titles[season] = new SeasonTitle(
                counted[champion],
                total,
                share,
                champion,
                name,
                DecidedInFinalRace: !clinched);
        }

        return titles;
    }

    private static string BetterChampion(
        string left,
        string right,
        Dictionary<string, decimal> counted,
        Dictionary<string, Dictionary<int, int>> finishes)
    {
        var points = counted[left].CompareTo(counted[right]);
        if (points != 0)
        {
            return points > 0 ? left : right;
        }

        var highest = 1;
        void Consider(Dictionary<int, int> places)
        {
            foreach (var place in places.Keys)
            {
                if (place > highest)
                {
                    highest = place;
                }
            }
        }

        if (finishes.TryGetValue(left, out var leftPlaces))
        {
            Consider(leftPlaces);
        }

        if (finishes.TryGetValue(right, out var rightPlaces))
        {
            Consider(rightPlaces);
        }

        for (var place = 1; place <= highest; place++)
        {
            var compare = PlaceCount(finishes, left, place).CompareTo(PlaceCount(finishes, right, place));
            if (compare != 0)
            {
                return compare > 0 ? left : right;
            }
        }

        return string.CompareOrdinal(left, right) <= 0 ? left : right;
    }

    private static int PlaceCount(Dictionary<string, Dictionary<int, int>> finishes, string driver, int place)
    {
        return finishes.TryGetValue(driver, out var places) && places.TryGetValue(place, out var count) ? count : 0;
    }

    private static List<(int Round, decimal Points)> RoundList(Dictionary<int, decimal> rounds, int? excludeRound = null)
    {
        var list = new List<(int Round, decimal Points)>();
        foreach (var pair in rounds)
        {
            if (excludeRound is int skip && pair.Key == skip)
            {
                continue;
            }

            list.Add((pair.Key, pair.Value));
        }

        return list;
    }

    private static SeasonReport SeasonFrom(
        int season,
        List<RaceTally> races,
        SeasonTitle? title,
        Dictionary<string, string> constructorNames)
    {
        var pooled = Pool(races);
        var (constructorId, constructorWins) = TopConstructor(races);
        string? constructorName = null;
        if (constructorId is not null)
        {
            constructorNames.TryGetValue(constructorId, out constructorName);
        }

        return new SeasonReport(
            season,
            races.Count,
            pooled.Entries,
            pooled.Starters,
            pooled.Classified,
            pooled.Mechanical,
            pooled.Accident,
            pooled.Other,
            pooled.Unmapped,
            pooled.ClassifiedMechanical,
            pooled.ClassifiedAccident,
            pooled.NonStarts,
            Median(races.Where(race => race.MarginMillis is not null).Select(race => race.MarginMillis!.Value).ToList()),
            races.Count(race => race.MarginMillis is not null),
            pooled.Wins,
            pooled.WinsFromPole,
            pooled.Winners.Count,
            pooled.PoleSitters.Count,
            constructorWins,
            constructorId,
            constructorName,
            title?.ChampionPoints,
            title?.PointsCounted,
            title?.Share,
            title?.ChampionDriverId,
            title?.ChampionName,
            title?.DecidedInFinalRace);
    }

    private static DecadeReport DecadeFrom(
        int decadeStart,
        List<RaceTally> races,
        List<SeasonReport> seasons,
        Dictionary<string, string> constructorNames)
    {
        var pooled = Pool(races);
        var (constructorId, constructorWins) = TopConstructor(races);
        string? constructorName = null;
        if (constructorId is not null)
        {
            constructorNames.TryGetValue(constructorId, out constructorName);
        }

        var shares = seasons.Where(season => season.ChampionShare is not null).Select(season => season.ChampionShare!.Value).ToList();
        var titled = seasons.Where(season => season.TitleDecidedInFinalRace is not null).ToList();
        return new DecadeReport(
            decadeStart,
            races.Count,
            pooled.Entries,
            pooled.Starters,
            pooled.Classified,
            pooled.Mechanical,
            pooled.Accident,
            pooled.Other,
            pooled.Unmapped,
            pooled.ClassifiedMechanical,
            pooled.ClassifiedAccident,
            pooled.NonStarts,
            Median(races.Where(race => race.MarginMillis is not null).Select(race => race.MarginMillis!.Value).ToList()),
            races.Count(race => race.MarginMillis is not null),
            pooled.Wins,
            pooled.WinsFromPole,
            pooled.Winners.Count,
            pooled.PoleSitters.Count,
            constructorWins,
            constructorId,
            constructorName,
            shares.Count == 0 ? null : shares.Sum() / shares.Count,
            titled.Count == 0 ? null : titled.Count(season => season.TitleDecidedInFinalRace == true),
            titled.Count == 0 ? null : titled.Count,
            UnmappedBreakdown(pooled.UnmappedByStatus));
    }

    private static Pooled Pool(List<RaceTally> races)
    {
        var pooled = new Pooled();
        foreach (var race in races)
        {
            pooled.Entries += race.Entries;
            pooled.Starters += race.Starters;
            pooled.Classified += race.Classified;
            pooled.Mechanical += race.Mechanical;
            pooled.Accident += race.Accident;
            pooled.Other += race.Other;
            pooled.Unmapped += race.Unmapped;
            pooled.ClassifiedMechanical += race.ClassifiedMechanical;
            pooled.ClassifiedAccident += race.ClassifiedAccident;
            pooled.NonStarts += race.NonStarts;
            foreach (var pair in race.UnmappedByStatus)
            {
                pooled.UnmappedByStatus.TryGetValue(pair.Key, out var count);
                pooled.UnmappedByStatus[pair.Key] = count + pair.Value;
            }
            if (race.WinnerDriverId is string winner)
            {
                pooled.Wins++;
                pooled.Winners.Add(winner);
                if (race.WinnerFromPole)
                {
                    pooled.WinsFromPole++;
                }
            }

            foreach (var pole in race.PoleSitters)
            {
                pooled.PoleSitters.Add(pole);
            }
        }

        return pooled;
    }

    private static (string? ConstructorId, int Wins) TopConstructor(List<RaceTally> races)
    {
        var wins = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var race in races)
        {
            if (race.WinnerConstructorId is not string constructor)
            {
                continue;
            }

            wins.TryGetValue(constructor, out var count);
            wins[constructor] = count + 1;
        }

        if (wins.Count == 0)
        {
            return (null, 0);
        }

        var top = wins
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .First();
        return (top.Key, top.Value);
    }

    private static decimal? Median(List<long> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        values.Sort();
        var mid = values.Count / 2;
        if (values.Count % 2 == 1)
        {
            return values[mid];
        }

        return (values[mid - 1] + values[mid]) / 2m;
    }

    private static List<UnmappedStatusCount> UnmappedBreakdown(Dictionary<string, int> counts)
    {
        var listed = new List<UnmappedStatusCount>();
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var status in FinishStatus.DeliberatelyUnmappedStatuses)
        {
            counts.TryGetValue(status, out var count);
            listed.Add(new UnmappedStatusCount(status, count));
            known.Add(status);
        }

        foreach (var pair in counts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (known.Add(pair.Key))
            {
                listed.Add(new UnmappedStatusCount(pair.Key, pair.Value));
            }
        }

        return listed;
    }

    private static string DisplayDriver(HistoricalDriver driver)
    {
        return driver.GivenName + " " + driver.FamilyName;
    }

    private sealed class RaceTally(int season, int round)
    {
        public int Season { get; } = season;
        public int Round { get; } = round;
        public int Entries { get; set; }
        public int Starters { get; set; }
        public int Classified { get; set; }
        public int Mechanical { get; set; }
        public int Accident { get; set; }
        public int Other { get; set; }
        public int Unmapped { get; set; }
        public int ClassifiedMechanical { get; set; }
        public int ClassifiedAccident { get; set; }
        public int NonStarts { get; set; }
        public Dictionary<string, int> UnmappedByStatus { get; } = new(StringComparer.Ordinal);
        public long? MarginMillis { get; set; }
        public string? WinnerDriverId { get; set; }
        public string? WinnerConstructorId { get; set; }
        public bool WinnerFromPole { get; set; }
        public HashSet<string> PoleSitters { get; } = new(StringComparer.Ordinal);
    }

    private sealed class Pooled
    {
        public int Entries { get; set; }
        public int Starters { get; set; }
        public int Classified { get; set; }
        public int Mechanical { get; set; }
        public int Accident { get; set; }
        public int Other { get; set; }
        public int Unmapped { get; set; }
        public int ClassifiedMechanical { get; set; }
        public int ClassifiedAccident { get; set; }
        public int NonStarts { get; set; }
        public Dictionary<string, int> UnmappedByStatus { get; } = new(StringComparer.Ordinal);
        public int Wins { get; set; }
        public int WinsFromPole { get; set; }
        public HashSet<string> Winners { get; } = new(StringComparer.Ordinal);
        public HashSet<string> PoleSitters { get; } = new(StringComparer.Ordinal);
    }

    private sealed record SeasonTitle(
        decimal ChampionPoints,
        decimal PointsCounted,
        decimal? Share,
        string ChampionDriverId,
        string? ChampionName,
        bool DecidedInFinalRace);
}

public sealed record EraStatsReport(
    int FromYear,
    int ToYear,
    IReadOnlyList<UnmappedStatusCount> UnmappedStatuses,
    PopulationReport Main,
    PopulationReport SharedDrives,
    PopulationReport Indianapolis500);

public sealed record UnmappedStatusCount(string Status, int Count);

public sealed record PopulationReport(
    IReadOnlyList<SeasonReport> Seasons,
    IReadOnlyList<DecadeReport> Decades);

public sealed record SeasonReport(
    int Season,
    int Races,
    int Entries,
    int Starters,
    int Classified,
    int DnfMechanical,
    int DnfAccident,
    int DnfOther,
    int DnfUnmapped,
    int ClassifiedMechanical,
    int ClassifiedAccident,
    int NonStarts,
    decimal? MedianWinningMarginMillis,
    int RacesWithWinningMargin,
    int Wins,
    int WinsFromPole,
    int DifferentWinners,
    int DifferentPoleSitters,
    int TopConstructorWins,
    string? TopConstructorId,
    string? TopConstructorName,
    decimal? ChampionPoints,
    decimal? PointsCounted,
    decimal? ChampionShare,
    string? ChampionDriverId,
    string? ChampionName,
    bool? TitleDecidedInFinalRace);

public sealed record DecadeReport(
    int DecadeStart,
    int Races,
    int Entries,
    int Starters,
    int Classified,
    int DnfMechanical,
    int DnfAccident,
    int DnfOther,
    int DnfUnmapped,
    int ClassifiedMechanical,
    int ClassifiedAccident,
    int NonStarts,
    decimal? MedianWinningMarginMillis,
    int RacesWithWinningMargin,
    int Wins,
    int WinsFromPole,
    int DifferentWinners,
    int DifferentPoleSitters,
    int TopConstructorWins,
    string? TopConstructorId,
    string? TopConstructorName,
    decimal? MeanChampionShare,
    int? TitlesDecidedInFinalRace,
    int? SeasonsWithChampionship,
    IReadOnlyList<UnmappedStatusCount> UnmappedByStatus);
