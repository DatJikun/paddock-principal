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
            "--lambda-c0",
            "--lambda-c-time",
            "--w-cross",
            "--lambda-curve",
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

        var lambdaC0 = RatingsModel.DefaultLambdaC0;
        var lambdaCTime = RatingsModel.DefaultLambdaCTime;
        var lambdaCurve = RatingsModel.DefaultLambdaCurve;
        double? wCross = null;

        if (options.ContainsKey("--lambda-c0") && !TryDouble(options, "--lambda-c0", stderr, out lambdaC0)) return 1;
        if (options.ContainsKey("--lambda-c-time") && !TryDouble(options, "--lambda-c-time", stderr, out lambdaCTime)) return 1;
        if (options.ContainsKey("--lambda-curve") && !TryDouble(options, "--lambda-curve", stderr, out lambdaCurve)) return 1;
        if (options.ContainsKey("--w-cross"))
        {
            if (!TryDouble(options, "--w-cross", stderr, out var wCrossValue)) return 1;
            wCross = wCrossValue;
        }

        if (wRace < 0 || wQuali < 0 || lambdaTime < 0 || lambda0 <= 0
            || lambdaCTime < 0 || lambdaCurve < 0 || (wCross is < 0))
        {
            stderr.WriteLine("Weights and penalties must be positive (lambda-0 must be strictly positive).");
            return 1;
        }

        if (lambdaC0 <= 0)
        {
            stderr.WriteLine("--lambda-c0 must be strictly positive (it provides car-effect identifiability).");
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
        var lineage = ConstructorLineageMap.Empty;
        try
        {
            var repoRoot = JolpicaCache.FindRepoRoot();
            var lineagePath = Path.Combine(repoRoot, "data", "authored", "teams", "lineage.json");
            if (File.Exists(lineagePath))
            {
                lineage = ConstructorLineageMap.Parse(File.ReadAllText(lineagePath));
            }

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
            lambda0,
            lambdaC0,
            lambdaCTime,
            wCross,
            lambdaCurve,
            lineage);

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

    /// <summary>
    /// Fits the v1 model (driver skill + car effect) and returns every intermediate artifact. Pure and
    /// deterministic: same input gives identical output.
    /// </summary>
    public static RatingsModelRun RunModel(
        IReadOnlyList<HistoricalRace> races,
        IReadOnlyList<HistoricalResult> results,
        int fromYear,
        int toYear,
        double wRace = RatingsModel.DefaultWRace,
        double wQuali = RatingsModel.DefaultWQuali,
        double lambdaTime = RatingsModel.DefaultLambdaTime,
        double lambda0 = RatingsModel.DefaultLambda0,
        double lambdaC0 = RatingsModel.DefaultLambdaC0,
        double lambdaCTime = RatingsModel.DefaultLambdaCTime,
        double? wCross = null,
        ConstructorLineageMap? lineage = null)
    {
        lineage ??= ConstructorLineageMap.Empty;

        var duels = RatingsModel.ExtractDuels(races, results, fromYear, toYear, wRace, wQuali, includeCross: true, wCross);

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
                timeLinks.Add(new TimeLink(curr.Index, prev.Index, curr.Season - prev.Season));
            }
        }

        // Car-effect parameters: one per (lineage key, season) that appears in a cross-constructor duel.
        var carSeasonKeys = duels
            .Where(d => d.LoserConstructorId is not null)
            .SelectMany(d => new[] { (d.ConstructorId, d.Season), (d.LoserConstructorId!, d.Season) })
            .Distinct()
            .Select(x => (Raw: x.Item1, x.Season, Key: lineage.Resolve(x.Item1, x.Season)))
            .ToList();

        var carParams = carSeasonKeys
            .Select(x => (x.Key, x.Season))
            .Distinct()
            .OrderBy(x => x.Season)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .Select((x, idx) => new CarSeasonParam(x.Key, x.Season, idx))
            .ToList();

        var carIndexByKeySeason = carParams.ToDictionary(p => (p.Key, p.Season), p => p.Index);
        var carIndexByConstructor = carSeasonKeys.ToDictionary(
            x => (x.Raw, x.Season),
            x => carIndexByKeySeason[(x.Key, x.Season)]);

        var carLinks = new List<TimeLink>();
        foreach (var group in carParams.GroupBy(p => p.Key, StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(p => p.Season).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                carLinks.Add(new TimeLink(ordered[i].Index, ordered[i - 1].Index, ordered[i].Season - ordered[i - 1].Season));
            }
        }

        var car = new CarModel(carParams, carLinks, carIndexByConstructor, lambdaC0, lambdaCTime);

        var fit = RatingsModel.Fit(duels, parameters, timeLinks, lambdaTime, lambda0, car);
        var metrics = RatingsModel.CalculateDriverMetrics(duels, parameters, fit);

        var carEffects = carParams
            .Select(p => new CarEffect(p.Key, p.Season, fit.C![p.Index], fit.CSe![p.Index]))
            .ToList();

        return new RatingsModelRun(duels, parameters, fit, metrics, carEffects);
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
        double lambda0 = RatingsModel.DefaultLambda0,
        double lambdaC0 = RatingsModel.DefaultLambdaC0,
        double lambdaCTime = RatingsModel.DefaultLambdaCTime,
        double? wCross = null,
        double lambdaCurve = RatingsModel.DefaultLambdaCurve,
        ConstructorLineageMap? lineage = null)
    {
        var driverNames = drivers.ToDictionary(
            d => d.DriverId,
            d => string.IsNullOrWhiteSpace(d.GivenName) && string.IsNullOrWhiteSpace(d.FamilyName)
                ? d.DriverId
                : $"{d.GivenName} {d.FamilyName}".Trim(),
            StringComparer.Ordinal);

        var birthYears = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var d in drivers)
        {
            if (d.DateOfBirth is { Length: >= 4 } dob
                && int.TryParse(dob.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year))
            {
                birthYears[d.DriverId] = year;
            }
        }

        var run = RunModel(races, results, fromYear, toYear, wRace, wQuali, lambdaTime, lambda0, lambdaC0, lambdaCTime, wCross, lineage);
        var duels = run.Duels;
        var parameters = run.Parameters;
        var fitResult = run.Fit;
        var driverMetrics = run.DriverMetrics;

        // Connectivity and the ranked set stay defined by the teammate graph, as in v0.
        var teammateDuels = duels.Where(d => d.LoserConstructorId is null).ToList();
        var allDriverIds = parameters.Select(p => p.DriverId).Distinct(StringComparer.Ordinal).ToList();
        var components = RatingsModel.FindComponents(teammateDuels, allDriverIds);

        var largestComponentSet = components.Count > 0
            ? new HashSet<string>(components[0], StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        var raceDuelsCount = teammateDuels.Count(d => d.Kind == DuelKind.Race);
        var qualiDuelsCount = teammateDuels.Count(d => d.Kind == DuelKind.Qualifying);
        var crossRaceDuels = duels.Count(d => d.LoserConstructorId is not null && d.Kind == DuelKind.Race);
        var crossQualiDuels = duels.Count(d => d.LoserConstructorId is not null && d.Kind == DuelKind.Qualifying);
        var racesCount = races.Count(r => r.Season >= fromYear && r.Season <= toYear && !r.IsIndianapolis500);

        var fitSummary = new RatingsFitSummary(
            RacesCount: racesCount,
            TotalDuels: teammateDuels.Count,
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
            Lambda0: lambda0,
            CrossRaceDuels: crossRaceDuels,
            CrossQualifyingDuels: crossQualiDuels,
            CarSeasonsCount: run.CarEffects.Count,
            LambdaC0: lambdaC0,
            LambdaCTime: lambdaCTime,
            WCross: wCross,
            LambdaCurve: lambdaCurve);

        // Career curves for every driver with enough seasons (v1 peak = smoothed peak, v0 3-season peak otherwise).
        var curves = new Dictionary<string, CareerCurve?>(StringComparer.Ordinal);
        foreach (var m in driverMetrics.Values)
        {
            curves[m.DriverId] = RatingsCurveModel.Build(
                m.Seasons,
                birthYears.TryGetValue(m.DriverId, out var by) ? by : null,
                lambdaCurve);
        }

        double PeakValueOf(FittedDriverMetrics m) => curves[m.DriverId]?.PeakValue ?? m.CareerPeak;

        // Drivers in largest component with >= 20 teammate duels
        var rankedMetrics = driverMetrics.Values
            .Where(m => largestComponentSet.Contains(m.DriverId) && m.TotalDuels >= 20)
            .OrderByDescending(PeakValueOf)
            .ThenByDescending(m => m.TotalDuels)
            .ThenBy(m => m.DriverId, StringComparer.Ordinal)
            .ToList();

        var sortedPeaks = rankedMetrics.Select(PeakValueOf).OrderBy(v => v).ToList();

        var rankedList = new List<DriverRankingEntry>(rankedMetrics.Count);
        var ratingEntries = new List<DriverRatingEntry>(rankedMetrics.Count);

        // Era-relative per-season values: smoothed where a curve exists, raw skill otherwise.
        var seasonValues = new Dictionary<int, List<double>>();
        double SeasonValue(FittedDriverMetrics m, int season, double raw)
        {
            var curve = curves[m.DriverId];
            return curve?.Smoothed.First(s => s.Season == season).Value ?? raw;
        }

        foreach (var m in rankedMetrics)
        {
            foreach (var (season, skill, _) in m.Seasons)
            {
                if (!seasonValues.TryGetValue(season, out var list))
                {
                    list = [];
                    seasonValues[season] = list;
                }

                list.Add(SeasonValue(m, season, skill));
            }
        }

        foreach (var list in seasonValues.Values)
        {
            list.Sort();
        }

        for (var i = 0; i < rankedMetrics.Count; i++)
        {
            var m = rankedMetrics[i];
            var name = driverNames.GetValueOrDefault(m.DriverId, m.DriverId);
            var peak = PeakValueOf(m);
            var percentile = RatingsMapping.Percentile(peak, sortedPeaks);
            var overall = RatingsMapping.Overall(percentile);
            var stars = RatingsMapping.Stars(percentile);

            rankedList.Add(new DriverRankingEntry(
                Rank: i + 1,
                DriverId: m.DriverId,
                Name: name,
                CareerPeak: m.CareerPeak,
                PeakSe: m.PeakSe,
                PeakYears: m.PeakYears,
                TotalDuels: m.TotalDuels,
                ShortCareer: m.ShortCareer,
                PeakValue: peak,
                Overall: overall,
                Stars: stars));

            var bySeason = new List<SeasonRating>(m.Seasons.Count);
            foreach (var (season, skill, _) in m.Seasons)
            {
                var value = SeasonValue(m, season, skill);
                var seasonPercentile = RatingsMapping.Percentile(value, seasonValues[season]);
                bySeason.Add(new SeasonRating(season, value, RatingsMapping.Overall(seasonPercentile)));
            }

            ratingEntries.Add(new DriverRatingEntry(
                Rank: i + 1,
                DriverId: m.DriverId,
                Name: name,
                Overall: overall,
                Stars: stars,
                Percentile: percentile,
                PeakValue: peak,
                Curve: curves[m.DriverId],
                RatingBySeason: bySeason));
        }

        var top50 = rankedList.Take(50).ToList();
        var rankedByDriverId = rankedList.ToDictionary(r => r.DriverId, r => r, StringComparer.Ordinal);

        // Top 10 per decade
        var startDecade = (fromYear / 10) * 10;
        var endDecade = (toYear / 10) * 10;
        var topByDecade = new List<DecadeTopEntry>();
        var carByDecade = new List<DecadeCarEffects>();

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

            var carsInDecade = run.CarEffects
                .Where(c => c.Season >= decStart && c.Season <= decEnd)
                .GroupBy(c => c.Key, StringComparer.Ordinal)
                .Select(g => new CarEffectEntry(g.Key, g.Average(c => c.Effect), g.Count()))
                .ToList();

            if (carsInDecade.Count > 0)
            {
                var top = carsInDecade
                    .OrderByDescending(c => c.MeanEffect)
                    .ThenBy(c => c.ConstructorKey, StringComparer.Ordinal)
                    .Take(5)
                    .ToList();
                var bottom = carsInDecade
                    .OrderBy(c => c.MeanEffect)
                    .ThenBy(c => c.ConstructorKey, StringComparer.Ordinal)
                    .Take(5)
                    .ToList();
                carByDecade.Add(new DecadeCarEffects(decStart, top, bottom));
            }
        }

        // Comparison with reference rankings (ranks follow the v1 peak)
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

        // Insufficient data drivers (< 20 teammate duels in largest component)
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
            DisconnectedDrivers: disconnected,
            CarEffectsByDecade: carByDecade,
            DriverRatings: ratingEntries);
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
