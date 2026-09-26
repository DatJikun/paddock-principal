using System.Globalization;
using System.Text.Json;
using Paddock.Data.Historical;

namespace Paddock.DataPipeline;

public static class RatingsCommand
{
    public const int DefaultFrom = 1950;
    public const int DefaultTo = 2025;

    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        var allowed = new[]
        {
            "--cache",
            "--from",
            "--to",
            "--w-race",
            "--w-quali",
            "--lambda-time",
            "--lambda-0",
        };

        if (!CommandArgs.TryParse(args, start: 1, allowed, stderr, out var options, out _))
        {
            return 1;
        }

        var from = DefaultFrom;
        var to = DefaultTo;
        if (options.ContainsKey("--from") && !TryInt(options, "--from", stderr, out from))
        {
            return 1;
        }

        if (options.ContainsKey("--to") && !TryInt(options, "--to", stderr, out to))
        {
            return 1;
        }

        if (from > to)
        {
            stderr.WriteLine("--from must not be greater than --to.");
            return 1;
        }

        var wRace = RatingsModel.DefaultWRace;
        var wQuali = RatingsModel.DefaultWQuali;
        var lambdaTime = RatingsModel.DefaultLambdaTime;
        var lambda0 = RatingsModel.DefaultLambda0;

        if (options.ContainsKey("--w-race") && !TryDouble(options, "--w-race", stderr, out wRace)) return 1;
        if (options.ContainsKey("--w-quali") && !TryDouble(options, "--w-quali", stderr, out wQuali)) return 1;
        if (options.ContainsKey("--lambda-time") && !TryDouble(options, "--lambda-time", stderr, out lambdaTime)) return 1;
        if (options.ContainsKey("--lambda-0") && !TryDouble(options, "--lambda-0", stderr, out lambda0)) return 1;

        if (wRace < 0 || wQuali < 0 || lambdaTime < 0 || lambda0 <= 0)
        {
            stderr.WriteLine("Weights and penalties must be positive (lambda-0 must be strictly positive).");
            return 1;
        }

        var cacheRoot = options.TryGetValue("--cache", out var cache) ? cache : JolpicaCache.DefaultRoot();
        var normalizedRoot = JolpicaCache.Normalized(cacheRoot);
        if (!Directory.Exists(normalizedRoot))
        {
            stderr.WriteLine($"Normalized cache not found: {normalizedRoot}");
            return 1;
        }

        var resultsPath = Path.Combine(normalizedRoot, "results.json");
        var racesPath = Path.Combine(normalizedRoot, "races.json");
        var driversPath = Path.Combine(normalizedRoot, "drivers.json");

        if (!File.Exists(resultsPath) || !File.Exists(racesPath) || !File.Exists(driversPath))
        {
            stderr.WriteLine($"Required normalized files missing in {normalizedRoot}");
            return 1;
        }

        var resultsDoc = JsonSerializer.Deserialize<HistoricalResultsDocument>(File.ReadAllText(resultsPath), HistoricalJson.Options);
        var racesDoc = JsonSerializer.Deserialize<HistoricalRacesDocument>(File.ReadAllText(racesPath), HistoricalJson.Options);
        var driversDoc = JsonSerializer.Deserialize<HistoricalDriversDocument>(File.ReadAllText(driversPath), HistoricalJson.Options);

        if (resultsDoc is null || racesDoc is null || driversDoc is null)
        {
            stderr.WriteLine("Failed to deserialize normalized documents.");
            return 1;
        }

        IReadOnlyList<ReferenceRankingDocument> refRankings = [];
        try
        {
            var repoRoot = JolpicaCache.FindRepoRoot();
            var refPath = Path.Combine(repoRoot, "data", "authored", "ratings", "reference_rankings.json");
            if (File.Exists(refPath))
            {
                var loaded = JsonSerializer.Deserialize<List<ReferenceRankingDocument>>(File.ReadAllText(refPath), HistoricalJson.Options);
                if (loaded is not null)
                {
                    refRankings = loaded;
                }
            }
        }
        catch
        {
            // Optional reference comparison if repo root or file cannot be resolved
        }

        var report = BuildReport(
            racesDoc.Races,
            resultsDoc.Results,
            driversDoc.Drivers,
            refRankings,
            from,
            to,
            wRace,
            wQuali,
            lambdaTime,
            lambda0);

        var reportsDir = StatsCommand.ReportsDirectory(cacheRoot);
        Directory.CreateDirectory(reportsDir);

        var markdownPath = Path.Combine(reportsDir, "ratings.md");
        var jsonPath = Path.Combine(reportsDir, "ratings.json");

        var markdown = RatingsMarkdown.Format(report);
        var json = JsonSerializer.Serialize(report, HistoricalJson.Options);
        if (!json.EndsWith('\n'))
        {
            json += "\n";
        }

        JolpicaCache.WriteAtomic(markdownPath, markdown);
        JolpicaCache.WriteAtomic(jsonPath, json);

        foreach (var line in RatingsMarkdown.SummaryLines(report))
        {
            stdout.WriteLine(line);
        }

        stdout.WriteLine("wrote " + markdownPath);
        stdout.WriteLine("wrote " + jsonPath);
        return 0;
    }

    public static RatingsReportDocument BuildReport(
        IReadOnlyList<HistoricalRace> races,
        IReadOnlyList<HistoricalResult> results,
        IReadOnlyList<HistoricalDriver> drivers,
        IReadOnlyList<ReferenceRankingDocument> referenceRankings,
        int fromYear,
        int toYear,
        double wRace = RatingsModel.DefaultWRace,
        double wQuali = RatingsModel.DefaultWQuali,
        double lambdaTime = RatingsModel.DefaultLambdaTime,
        double lambda0 = RatingsModel.DefaultLambda0)
    {
        var driverNames = drivers.ToDictionary(
            d => d.DriverId,
            d => string.IsNullOrWhiteSpace(d.GivenName) && string.IsNullOrWhiteSpace(d.FamilyName)
                ? d.DriverId
                : $"{d.GivenName} {d.FamilyName}".Trim(),
            StringComparer.Ordinal);

        var duels = RatingsModel.ExtractDuels(races, results, fromYear, toYear, wRace, wQuali);

        var distinctParams = duels
            .Select(d => (d.WinnerDriverId, d.Season))
            .Concat(duels.Select(d => (d.LoserDriverId, d.Season)))
            .Distinct()
            .OrderBy(p => p.Season)
            .ThenBy(p => p.Item1, StringComparer.Ordinal)
            .ToList();

        var parameters = distinctParams
            .Select((p, idx) => new DriverSeasonParam(p.Item1, p.Season, idx))
            .ToList();

        var paramByDriverAndSeason = parameters.ToDictionary(p => (p.DriverId, p.Season), p => p.Index);

        var timeLinks = new List<TimeLink>();
        var driverSeasonsGrouped = parameters
            .GroupBy(p => p.DriverId)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Season).ToList(), StringComparer.Ordinal);

        foreach (var (_, list) in driverSeasonsGrouped)
        {
            for (var i = 1; i < list.Count; i++)
            {
                var prev = list[i - 1];
                var curr = list[i];
                var gap = curr.Season - prev.Season;
                timeLinks.Add(new TimeLink(curr.Index, prev.Index, gap));
            }
        }

        var allDriverIds = parameters.Select(p => p.DriverId).Distinct(StringComparer.Ordinal).ToList();
        var components = RatingsModel.FindComponents(duels, allDriverIds);

        var largestComponentSet = components.Count > 0
            ? new HashSet<string>(components[0], StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        var fitResult = RatingsModel.Fit(duels, parameters, timeLinks, lambdaTime, lambda0);
        var driverMetrics = RatingsModel.CalculateDriverMetrics(duels, parameters, fitResult);

        var raceDuelsCount = duels.Count(d => d.Kind == DuelKind.Race);
        var qualiDuelsCount = duels.Count(d => d.Kind == DuelKind.Qualifying);
        var racesCount = races.Count(r => r.Season >= fromYear && r.Season <= toYear && !r.IsIndianapolis500);

        var fitSummary = new RatingsFitSummary(
            RacesCount: racesCount,
            TotalDuels: duels.Count,
            RaceDuels: raceDuelsCount,
            QualifyingDuels: qualiDuelsCount,
            DriverSeasonsCount: parameters.Count,
            Iterations: fitResult.Iterations,
            Converged: fitResult.Converged,
            MaxGradient: fitResult.MaxGradient,
            ComponentsCount: components.Count,
            LargestComponentSize: largestComponentSet.Count,
            FromYear: fromYear,
            ToYear: toYear,
            WRace: wRace,
            WQuali: wQuali,
            LambdaTime: lambdaTime,
            Lambda0: lambda0);

        // Drivers in largest component with >= 20 duels
        var rankedMetrics = driverMetrics.Values
            .Where(m => largestComponentSet.Contains(m.DriverId) && m.TotalDuels >= 20)
            .OrderByDescending(m => m.CareerPeak)
            .ThenByDescending(m => m.TotalDuels)
            .ThenBy(m => m.DriverId, StringComparer.Ordinal)
            .ToList();

        var rankedList = new List<DriverRankingEntry>(rankedMetrics.Count);
        for (var i = 0; i < rankedMetrics.Count; i++)
        {
            var m = rankedMetrics[i];
            var name = driverNames.GetValueOrDefault(m.DriverId, m.DriverId);
            rankedList.Add(new DriverRankingEntry(
                Rank: i + 1,
                DriverId: m.DriverId,
                Name: name,
                CareerPeak: m.CareerPeak,
                PeakSe: m.PeakSe,
                PeakYears: m.PeakYears,
                TotalDuels: m.TotalDuels,
                ShortCareer: m.ShortCareer));
        }

        var top50 = rankedList.Take(50).ToList();
        var rankedByDriverId = rankedList.ToDictionary(r => r.DriverId, r => r, StringComparer.Ordinal);

        // Top 10 per decade
        var startDecade = (fromYear / 10) * 10;
        var endDecade = (toYear / 10) * 10;
        var topByDecade = new List<DecadeTopEntry>();

        for (var dec = startDecade; dec <= endDecade; dec += 10)
        {
            var decStart = dec;
            var decEnd = dec + 9;

            var decadeDrivers = new List<(string DriverId, string Name, double MeanSkill, int SeasonsCount, int CareerDuels)>();

            foreach (var r in rankedList)
            {
                var m = driverMetrics[r.DriverId];
                var seasonsInDecade = m.Seasons
                    .Where(s => s.Season >= decStart && s.Season <= decEnd)
                    .ToList();

                if (seasonsInDecade.Count > 0)
                {
                    var meanSkill = seasonsInDecade.Average(s => s.Skill);
                    decadeDrivers.Add((
                        r.DriverId,
                        r.Name,
                        meanSkill,
                        seasonsInDecade.Count,
                        r.TotalDuels));
                }
            }

            var top10 = decadeDrivers
                .OrderByDescending(d => d.MeanSkill)
                .ThenByDescending(d => d.CareerDuels)
                .ThenBy(d => d.DriverId, StringComparer.Ordinal)
                .Take(10)
                .Select((d, idx) => new DecadeDriverEntry(
                    Rank: idx + 1,
                    DriverId: d.DriverId,
                    Name: d.Name,
                    MeanSkill: d.MeanSkill,
                    SeasonsCount: d.SeasonsCount,
                    CareerDuels: d.CareerDuels))
                .ToList();

            if (top10.Count > 0)
            {
                topByDecade.Add(new DecadeTopEntry(decStart, top10));
            }
        }

        // Comparison with reference rankings
        var refComparisons = new List<ReferenceComparisonReport>();
        foreach (var refRank in referenceRankings)
        {
            var overlaps = new List<(ReferenceRankingEntry Entry, DriverRankingEntry Ranked)>();
            foreach (var entry in refRank.Entries)
            {
                if (rankedByDriverId.TryGetValue(entry.DriverId, out var ourRanked))
                {
                    overlaps.Add((entry, ourRanked));
                }
            }

            if (overlaps.Count == 0)
            {
                continue;
            }

            double? spearman = null;
            if (overlaps.Count >= 2)
            {
                var theirRanks = overlaps.Select(o => (double)o.Entry.Rank).ToList();
                var ourRanks = overlaps.Select(o => (double)o.Ranked.Rank).ToList();
                spearman = RatingsModel.SpearmanCorrelation(theirRanks, ourRanks);
            }

            var disagreements = overlaps
                .Select(o => new DisagreementEntry(
                    DriverId: o.Entry.DriverId,
                    Name: o.Ranked.Name,
                    TheirRank: o.Entry.Rank,
                    OurRank: o.Ranked.Rank,
                    Difference: Math.Abs(o.Entry.Rank - o.Ranked.Rank)))
                .OrderByDescending(d => d.Difference)
                .ThenBy(d => d.OurRank)
                .ThenBy(d => d.DriverId, StringComparer.Ordinal)
                .Take(5)
                .ToList();

            refComparisons.Add(new ReferenceComparisonReport(
                RankingId: refRank.Id,
                RankingTitle: refRank.Title,
                OverlapCount: overlaps.Count,
                SpearmanCorrelation: spearman,
                TopDisagreements: disagreements));
        }

        // Insufficient data drivers (< 20 duels in largest component)
        var insufficientData = driverMetrics.Values
            .Where(m => largestComponentSet.Contains(m.DriverId) && m.TotalDuels < 20)
            .OrderByDescending(m => m.TotalDuels)
            .ThenBy(m => m.DriverId, StringComparer.Ordinal)
            .Select(m => new DriverDataSummary(
                DriverId: m.DriverId,
                Name: driverNames.GetValueOrDefault(m.DriverId, m.DriverId),
                TotalDuels: m.TotalDuels,
                SeasonsCount: m.SeasonsCount))
            .ToList();

        // Disconnected drivers (in components after the largest)
        var disconnected = new List<DisconnectedDriverSummary>();
        for (var cIdx = 1; cIdx < components.Count; cIdx++)
        {
            var comp = components[cIdx];
            foreach (var driverId in comp)
            {
                var duelsCount = driverMetrics.TryGetValue(driverId, out var m) ? m.TotalDuels : 0;
                disconnected.Add(new DisconnectedDriverSummary(
                    DriverId: driverId,
                    Name: driverNames.GetValueOrDefault(driverId, driverId),
                    ComponentIndex: cIdx + 1,
                    ComponentSize: comp.Count,
                    TotalDuels: duelsCount));
            }
        }
        disconnected.Sort((d1, d2) =>
        {
            var cmp = d1.ComponentIndex.CompareTo(d2.ComponentIndex);
            if (cmp != 0) return cmp;
            return string.Compare(d1.DriverId, d2.DriverId, StringComparison.Ordinal);
        });

        return new RatingsReportDocument(
            FitSummary: fitSummary,
            AllTimeTop50: top50,
            AllRankedDrivers: rankedList,
            TopByDecade: topByDecade,
            ReferenceComparisons: refComparisons,
            InsufficientDataDrivers: insufficientData,
            DisconnectedDrivers: disconnected);
    }

    private static bool TryInt(Dictionary<string, string> options, string flag, TextWriter stderr, out int val)
    {
        if (!int.TryParse(options[flag], NumberStyles.None, CultureInfo.InvariantCulture, out val))
        {
            stderr.WriteLine($"Invalid {flag} value: {options[flag]}");
            return false;
        }

        return true;
    }

    private static bool TryDouble(Dictionary<string, string> options, string flag, TextWriter stderr, out double val)
    {
        if (!double.TryParse(options[flag], NumberStyles.Float, CultureInfo.InvariantCulture, out val))
        {
            stderr.WriteLine($"Invalid {flag} value: {options[flag]}");
            return false;
        }

        return true;
    }
}
