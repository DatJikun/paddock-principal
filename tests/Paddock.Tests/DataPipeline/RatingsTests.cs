using System.Globalization;
using System.Text.Json;
using Paddock.Data.Historical;
using Paddock.DataPipeline;
using Paddock.Domain.Random;

namespace Paddock.Tests.DataPipeline;

public class RatingsTests
{
    [Fact]
    public void RecoveryOnSyntheticData_RecoversDriverSkillWithSpearmanAtLeast085()
    {
        // Deterministic RNG with fixed seed
        var rng = Xoshiro256StarStar.FromSeed(42UL);

        const int totalSeasons = 30;
        const int numTeams = 10;
        const int racesPerSeason = 16;

        var driverPool = new List<SyntheticDriver>();
        var driverIdCounter = 0;

        SyntheticDriver SpawnDriver(int startSeason)
        {
            var career = 3 + (int)(rng.NextDouble() * 8); // 3 to 10 seasons
            var initialSkill = (rng.NextDouble() * 4.0) - 2.0; // -2.0 to +2.0
            var d = new SyntheticDriver($"driver_{++driverIdCounter}", startSeason, career, initialSkill);
            driverPool.Add(d);
            return d;
        }

        var activeDrivers = new List<SyntheticDriver>();
        for (var i = 0; i < numTeams * 2; i++)
        {
            activeDrivers.Add(SpawnDriver(1));
        }

        var races = new List<HistoricalRace>();
        var results = new List<HistoricalResult>();
        var driversList = new List<HistoricalDriver>();

        var trueSkillsByDriverSeason = new Dictionary<(string DriverId, int Season), double>();

        for (var season = 1; season <= totalSeasons; season++)
        {
            // Retire drivers whose career ended
            activeDrivers.RemoveAll(d => d.StartSeason + d.CareerLength <= season);

            // Spawn replacements
            while (activeDrivers.Count < numTeams * 2)
            {
                activeDrivers.Add(SpawnDriver(season));
            }

            // Apply slow random walk to active drivers
            foreach (var d in activeDrivers)
            {
                trueSkillsByDriverSeason[(d.DriverId, season)] = d.CurrentSkill;
                var drift = (rng.NextDouble() * 0.1) - 0.05; // slow drift
                d.CurrentSkill += drift;
            }

            // Shuffle with probability to simulate transfers while keeping some partnerships stable
            if (season > 1)
            {
                Shuffle(activeDrivers, rng);
            }

            // Generate races and results
            for (var round = 1; round <= racesPerSeason; round++)
            {
                races.Add(new HistoricalRace(
                    Season: season,
                    Round: round,
                    Name: $"Grand Prix {season}/{round}",
                    CircuitId: "circuit_synthetic",
                    Date: $"{season:D4}-05-01",
                    Time: null,
                    Url: string.Empty,
                    IsIndianapolis500: false));

                for (var team = 0; team < numTeams; team++)
                {
                    var constructorId = $"team_{team}";
                    var driverA = activeDrivers[team * 2];
                    var driverB = activeDrivers[team * 2 + 1];

                    var skillA = trueSkillsByDriverSeason[(driverA.DriverId, season)];
                    var skillB = trueSkillsByDriverSeason[(driverB.DriverId, season)];

                    var probA = RatingsModel.Sigmoid(skillA - skillB);
                    var aWinsRace = rng.NextDouble() < probA;
                    var aWinsQuali = rng.NextDouble() < probA;

                    var posA = aWinsRace ? 1 : 2;
                    var posB = aWinsRace ? 2 : 1;
                    var gridA = aWinsQuali ? 1 : 2;
                    var gridB = aWinsQuali ? 2 : 1;

                    results.Add(new HistoricalResult(
                        Season: season,
                        Round: round,
                        DriverId: driverA.DriverId,
                        ConstructorId: constructorId,
                        CarNumber: "1",
                        Position: posA,
                        PositionText: posA.ToString(CultureInfo.InvariantCulture),
                        Status: "Finished",
                        Points: posA == 1 ? 10 : 6,
                        Grid: gridA,
                        Laps: 50,
                        Time: null,
                        TimeMillis: null,
                        IsClassified: true,
                        IsDisqualified: false,
                        IsSharedDrive: false));

                    results.Add(new HistoricalResult(
                        Season: season,
                        Round: round,
                        DriverId: driverB.DriverId,
                        ConstructorId: constructorId,
                        CarNumber: "2",
                        Position: posB,
                        PositionText: posB.ToString(CultureInfo.InvariantCulture),
                        Status: "Finished",
                        Points: posB == 1 ? 10 : 6,
                        Grid: gridB,
                        Laps: 50,
                        Time: null,
                        TimeMillis: null,
                        IsClassified: true,
                        IsDisqualified: false,
                        IsSharedDrive: false));
                }
            }
        }

        foreach (var d in driverPool)
        {
            driversList.Add(new HistoricalDriver(
                DriverId: d.DriverId,
                GivenName: "Driver",
                FamilyName: d.DriverId,
                DateOfBirth: null,
                Nationality: null,
                Code: null,
                PermanentNumber: null,
                Url: null));
        }

        var report = RatingsCommand.BuildReport(
            races,
            results,
            driversList,
            referenceRankings: [],
            fromYear: 1,
            toYear: totalSeasons);

        Assert.True(report.AllTimeTop50.Count >= 20);

        // Compute true peak for every ranked driver
        var truePeaks = new List<double>();
        var fittedPeaks = new List<double>();

        foreach (var ranked in report.AllRankedDrivers)
        {
            var driverSeasons = trueSkillsByDriverSeason
                .Where(kvp => kvp.Key.DriverId == ranked.DriverId)
                .OrderBy(kvp => kvp.Key.Season)
                .Select(kvp => (kvp.Key.Season, Skill: kvp.Value, Se: 0.0))
                .ToList();

            var (truePeak, _, _, _) = RatingsModel.CalculatePeak(driverSeasons);
            truePeaks.Add(truePeak);
            fittedPeaks.Add(ranked.CareerPeak);
        }

        var spearman = RatingsModel.SpearmanCorrelation(truePeaks, fittedPeaks);
        Assert.True(spearman >= 0.85, $"Expected Spearman correlation >= 0.85, got {spearman:F3}");
    }

    [Fact]
    public void Filtering_HandBuiltRowsProveCorrectExclusionsAndDuelRules()
    {
        var races = new List<HistoricalRace>
        {
            new(1950, 1, "Normal GP", "circuit_1", "1950-05-13", null, string.Empty, IsIndianapolis500: false),
            new(1950, 2, "Indy 500", "indianapolis", "1950-05-30", null, string.Empty, IsIndianapolis500: true),
            new(1950, 3, "Shared Drive GP", "circuit_2", "1950-06-18", null, string.Empty, IsIndianapolis500: false),
        };

        var results = new List<HistoricalResult>
        {
            // Indy 500 race (round 2): should be completely excluded
            new(1950, 2, "indy_driver_1", "team_indy", "1", 1, "1", "Finished", 8, 1, 200, null, null, true, false, false),
            new(1950, 2, "indy_driver_2", "team_indy", "2", 2, "2", "Finished", 6, 2, 200, null, null, true, false, false),

            // Shared-drive rows in round 3: should be excluded
            new(1950, 3, "shared_1", "team_shared", "3", 1, "1", "Finished", 8, 1, 50, null, null, true, false, true),
            new(1950, 3, "shared_2", "team_shared", "4", 2, "2", "Finished", 6, 2, 50, null, null, true, false, true),

            // Normal GP (round 1), team Alpha:
            // Driver A1: Finished, 1st, grid 2
            new(1950, 1, "alpha_1", "team_alpha", "10", 1, "1", "Finished", 8, 2, 50, null, null, true, false, false),

            // Driver A2 (mechanical DNF): Engine -> NO race duel with A1. Grid 4 -> quali duel (A1 grid 2 beats A2 grid 4)
            new(1950, 1, "alpha_2", "team_alpha", "11", 10, "R", "Engine", 0, 4, 20, null, null, false, false, false),

            // Driver A3 (accident DNF, pit-lane start grid 0): Accident -> race duel (A1 wins!). Grid 0 -> NO quali duel
            new(1950, 1, "alpha_3", "team_alpha", "12", 11, "R", "Accident", 0, 0, 15, null, null, false, false, false),

            // Driver A4 (non-start): Did not start -> excluded entirely
            new(1950, 1, "alpha_4", "team_alpha", "13", 0, "W", "Did not start", 0, 0, 0, null, null, false, false, false),

            // Driver A5 (classified mechanical): status Overheating, positionText 8 -> NO race duel with A1
            new(1950, 1, "alpha_5", "team_alpha", "14", 8, "8", "Overheating", 0, 5, 45, null, null, true, false, false),

            // Driver A6 (classified accident): status Collision, positionText 9 -> A1 wins race duel!
            new(1950, 1, "alpha_6", "team_alpha", "15", 9, "9", "Collision", 0, 6, 46, null, null, true, false, false),
        };

        var duels = RatingsModel.ExtractDuels(races, results, 1950, 1950);

        // Verify exclusions:
        // No duels from Indy 500 or shared drives or non-starts
        Assert.DoesNotContain(duels, d => d.Season == 1950 && d.Round == 2);
        Assert.DoesNotContain(duels, d => d.Season == 1950 && d.Round == 3);
        Assert.DoesNotContain(duels, d => d.WinnerDriverId == "alpha_4" || d.LoserDriverId == "alpha_4");

        // Pairs with alpha_1 (Finished):
        // alpha_1 vs alpha_2 (Mechanical DNF): NO race duel. Quali duel: alpha_1 beats alpha_2.
        var a1_a2 = duels.Where(d => (d.WinnerDriverId == "alpha_1" && d.LoserDriverId == "alpha_2") ||
                                     (d.WinnerDriverId == "alpha_2" && d.LoserDriverId == "alpha_1")).ToList();
        Assert.Single(a1_a2);
        Assert.Equal(DuelKind.Qualifying, a1_a2[0].Kind);
        Assert.Equal("alpha_1", a1_a2[0].WinnerDriverId);

        // alpha_1 vs alpha_3 (Accident, grid 0): Race duel: alpha_1 beats alpha_3. NO quali duel (grid 0).
        var a1_a3 = duels.Where(d => (d.WinnerDriverId == "alpha_1" && d.LoserDriverId == "alpha_3") ||
                                     (d.WinnerDriverId == "alpha_3" && d.LoserDriverId == "alpha_1")).ToList();
        Assert.Single(a1_a3);
        Assert.Equal(DuelKind.Race, a1_a3[0].Kind);
        Assert.Equal("alpha_1", a1_a3[0].WinnerDriverId);

        // alpha_1 vs alpha_5 (Classified mechanical): NO race duel. Quali duel: alpha_1 beats alpha_5.
        var a1_a5 = duels.Where(d => (d.WinnerDriverId == "alpha_1" && d.LoserDriverId == "alpha_5") ||
                                     (d.WinnerDriverId == "alpha_5" && d.LoserDriverId == "alpha_1")).ToList();
        Assert.Single(a1_a5);
        Assert.Equal(DuelKind.Qualifying, a1_a5[0].Kind);

        // alpha_1 vs alpha_6 (Classified accident): Race duel: alpha_1 beats alpha_6. Quali duel: alpha_1 beats alpha_6.
        var a1_a6 = duels.Where(d => (d.WinnerDriverId == "alpha_1" && d.LoserDriverId == "alpha_6") ||
                                     (d.WinnerDriverId == "alpha_6" && d.LoserDriverId == "alpha_1")).ToList();
        Assert.Equal(2, a1_a6.Count);
        Assert.Contains(a1_a6, d => d.Kind == DuelKind.Race && d.WinnerDriverId == "alpha_1");
        Assert.Contains(a1_a6, d => d.Kind == DuelKind.Qualifying && d.WinnerDriverId == "alpha_1");
    }

    [Fact]
    public void GapScaledPenalty_DriverWith5YearBreakIsAllowedBiggerJumpThanWith1Year()
    {
        // Driver 1: active in 1960 and 1961 (gap = 1 year)
        // Driver 2: active in 1960 and 1965 (gap = 5 years)
        // In 1960, both lose to reference partner (driving skill negative)
        // In their second season, both win against reference partner (driving skill positive)
        var races = new List<HistoricalRace>();
        var results = new List<HistoricalResult>();

        void AddRace(int season, int round, string driver, string bench, bool driverWins)
        {
            races.Add(new HistoricalRace(season, round, $"GP {season}/{round}", "track", $"{season}-06-01", null, string.Empty, false));

            var posD = driverWins ? 1 : 2;
            var posB = driverWins ? 2 : 1;

            results.Add(new HistoricalResult(season, round, driver, "team", "1", posD, posD.ToString(CultureInfo.InvariantCulture), "Finished", 10, posD, 50, null, null, true, false, false));
            results.Add(new HistoricalResult(season, round, bench, "team", "2", posB, posB.ToString(CultureInfo.InvariantCulture), "Finished", 6, posB, 50, null, null, true, false, false));
        }

        for (var r = 1; r <= 15; r++)
        {
            // 1960: D1 and D2 both lose to benchmark
            AddRace(1960, r, "d1", "bench1", driverWins: false);
            AddRace(1960, 20 + r, "d2", "bench2", driverWins: false);

            // 1961: D1 wins against benchmark (gap = 1)
            AddRace(1961, r, "d1", "bench1", driverWins: true);

            // 1965: D2 wins against benchmark (gap = 5)
            AddRace(1965, r, "d2", "bench2", driverWins: true);
        }

        var drivers = new[] { "d1", "d2", "bench1", "bench2" }
            .Select(id => new HistoricalDriver(id, id, id, null, null, null, null, null))
            .ToList();

        var report = RatingsCommand.BuildReport(races, results, drivers, referenceRankings: [], 1960, 1965);

        // Find parameters from fitted report
        // D1 has seasons 1960 and 1961
        // D2 has seasons 1960 and 1965
        var duels = RatingsModel.ExtractDuels(races, results, 1960, 1965);
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
        var grouped = parameters.GroupBy(p => p.DriverId).ToDictionary(g => g.Key, g => g.OrderBy(p => p.Season).ToList());
        foreach (var (_, list) in grouped)
        {
            for (var i = 1; i < list.Count; i++)
            {
                timeLinks.Add(new TimeLink(list[i].Index, list[i - 1].Index, list[i].Season - list[i - 1].Season));
            }
        }

        var fit = RatingsModel.Fit(duels, parameters, timeLinks);
        var metrics = RatingsModel.CalculateDriverMetrics(duels, parameters, fit);

        var s_d1_1960 = metrics["d1"].Seasons.First(s => s.Season == 1960).Skill;
        var s_d1_1961 = metrics["d1"].Seasons.First(s => s.Season == 1961).Skill;
        var jump1Year = Math.Abs(s_d1_1961 - s_d1_1960);

        var s_d2_1960 = metrics["d2"].Seasons.First(s => s.Season == 1960).Skill;
        var s_d2_1965 = metrics["d2"].Seasons.First(s => s.Season == 1965).Skill;
        var jump5Years = Math.Abs(s_d2_1965 - s_d2_1960);

        Assert.True(
            jump5Years > jump1Year,
            $"Expected 5-year break jump ({jump5Years:F4}) to be strictly greater than 1-year jump ({jump1Year:F4})");
    }

    [Fact]
    public void Determinism_TwoRunsProduceByteIdenticalJson()
    {
        var rng = Xoshiro256StarStar.FromSeed(999UL);
        var races = new List<HistoricalRace>();
        var results = new List<HistoricalResult>();
        var drivers = new List<HistoricalDriver>();

        for (var y = 1970; y <= 1974; y++)
        {
            for (var r = 1; r <= 8; r++)
            {
                races.Add(new HistoricalRace(y, r, $"GP {y}/{r}", "track", $"{y}-05-01", null, string.Empty, false));
                for (var t = 0; t < 3; t++)
                {
                    var dA = $"d_{t * 2}";
                    var dB = $"d_{t * 2 + 1}";
                    var aWins = rng.NextDouble() > 0.5;

                    results.Add(new HistoricalResult(y, r, dA, $"team_{t}", "1", aWins ? 1 : 2, (aWins ? 1 : 2).ToString(CultureInfo.InvariantCulture), "Finished", 10, aWins ? 1 : 2, 50, null, null, true, false, false));
                    results.Add(new HistoricalResult(y, r, dB, $"team_{t}", "2", aWins ? 2 : 1, (aWins ? 2 : 1).ToString(CultureInfo.InvariantCulture), "Finished", 6, aWins ? 2 : 1, 50, null, null, true, false, false));
                }
            }
        }

        for (var i = 0; i < 6; i++)
        {
            drivers.Add(new HistoricalDriver($"d_{i}", "Driver", $"{i}", null, null, null, null, null));
        }

        var report1 = RatingsCommand.BuildReport(races, results, drivers, [], 1970, 1974);
        var report2 = RatingsCommand.BuildReport(races, results, drivers, [], 1970, 1974);

        var json1 = JsonSerializer.Serialize(report1, HistoricalJson.Options);
        var json2 = JsonSerializer.Serialize(report2, HistoricalJson.Options);

        Assert.Equal(json1, json2);
    }

    [Fact]
    public void Components_TwoDisconnectedGroupsGiveTwoComponentsAndOnlyLargerIsRanked()
    {
        // Group A: 3 drivers (A1, A2, A3) racing against each other
        // Group B: 2 drivers (B1, B2) racing against each other
        // No duels connecting Group A and Group B
        var races = new List<HistoricalRace>();
        var results = new List<HistoricalResult>();

        for (var r = 1; r <= 25; r++)
        {
            races.Add(new HistoricalRace(1980, r, $"GP {r}", "track", "1980-05-01", null, string.Empty, false));

            // Group A (team Alpha: 3 drivers)
            results.Add(new HistoricalResult(1980, r, "A1", "team_alpha", "1", 1, "1", "Finished", 10, 1, 50, null, null, true, false, false));
            results.Add(new HistoricalResult(1980, r, "A2", "team_alpha", "2", 2, "2", "Finished", 6, 2, 50, null, null, true, false, false));
            results.Add(new HistoricalResult(1980, r, "A3", "team_alpha", "3", 3, "3", "Finished", 4, 3, 50, null, null, true, false, false));

            // Group B (team Beta: 2 drivers)
            results.Add(new HistoricalResult(1980, r, "B1", "team_beta", "4", 1, "1", "Finished", 10, 1, 50, null, null, true, false, false));
            results.Add(new HistoricalResult(1980, r, "B2", "team_beta", "5", 2, "2", "Finished", 6, 2, 50, null, null, true, false, false));
        }

        var drivers = new[] { "A1", "A2", "A3", "B1", "B2" }
            .Select(id => new HistoricalDriver(id, id, id, null, null, null, null, null))
            .ToList();

        var report = RatingsCommand.BuildReport(races, results, drivers, [], 1980, 1980);

        Assert.Equal(2, report.FitSummary.ComponentsCount);
        Assert.Equal(3, report.FitSummary.LargestComponentSize);

        // Only group A is ranked in all-time rankings
        Assert.Equal(3, report.AllTimeTop50.Count);
        Assert.All(report.AllTimeTop50, d => Assert.StartsWith("A", d.DriverId));

        // Group B drivers are listed separately as disconnected
        Assert.Equal(2, report.DisconnectedDrivers.Count);
        Assert.All(report.DisconnectedDrivers, d => Assert.StartsWith("B", d.DriverId));
    }

    [Fact]
    public async Task RatingsCommand_ValidatesArguments_ReturnsErrorOnInvalidFlags()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var invalidYear = await PipelineCommands.ExecuteAsync(["ratings", "--from", "2020", "--to", "2010"], stdout, stderr);
        Assert.Equal(1, invalidYear);
        Assert.Contains("--from must not be greater than --to", stderr.ToString());

        stderr.GetStringBuilder().Clear();
        var invalidWeight = await PipelineCommands.ExecuteAsync(["ratings", "--w-race", "-1.0"], stdout, stderr);
        Assert.Equal(1, invalidWeight);
        Assert.Contains("must be positive", stderr.ToString());

        stderr.GetStringBuilder().Clear();
        var invalidLambda = await PipelineCommands.ExecuteAsync(["ratings", "--lambda-0", "0.0"], stdout, stderr);
        Assert.Equal(1, invalidLambda);
        Assert.Contains("strictly positive", stderr.ToString());
    }

    [Fact]
    public async Task RatingsCommand_ExecutesAndWritesReport_OnNormalizedData()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "paddock-ratings-test-" + Guid.NewGuid().ToString("N"));
        var jolpicaCache = Path.Combine(tempRoot, "jolpica");
        var normalizedDir = Path.Combine(jolpicaCache, "normalized");
        Directory.CreateDirectory(normalizedDir);

        try
        {
            var racesDoc = new HistoricalRacesDocument(1,
            [
                new HistoricalRace(1950, 1, "British GP", "silverstone", "1950-05-13", null, string.Empty, false),
            ]);

            var resultsDoc = new HistoricalResultsDocument(1,
            [
                new HistoricalResult(1950, 1, "farina", "alfa", "1", 1, "1", "Finished", 9, 1, 70, null, null, true, false, false),
                new HistoricalResult(1950, 1, "fagioli", "alfa", "2", 2, "2", "Finished", 6, 2, 70, null, null, true, false, false),
            ]);

            var driversDoc = new HistoricalDriversDocument(1,
            [
                new HistoricalDriver("farina", "Nino", "Farina", null, null, null, null, null),
                new HistoricalDriver("fagioli", "Luigi", "Fagioli", null, null, null, null, null),
            ]);

            File.WriteAllText(Path.Combine(normalizedDir, "races.json"), JsonSerializer.Serialize(racesDoc, HistoricalJson.Options));
            File.WriteAllText(Path.Combine(normalizedDir, "results.json"), JsonSerializer.Serialize(resultsDoc, HistoricalJson.Options));
            File.WriteAllText(Path.Combine(normalizedDir, "drivers.json"), JsonSerializer.Serialize(driversDoc, HistoricalJson.Options));

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var code = await PipelineCommands.ExecuteAsync(
                ["ratings", "--cache", jolpicaCache, "--from", "1950", "--to", "1950"],
                stdout,
                stderr);

            Assert.Equal(0, code);
            Assert.Equal(string.Empty, stderr.ToString());

            var outText = stdout.ToString();
            Assert.Contains("ratings fit 1950–1950", outText);
            Assert.Contains("wrote ", outText);

            var mdPath = Path.Combine(tempRoot, "reports", "ratings.md");
            var jsonPath = Path.Combine(tempRoot, "reports", "ratings.json");
            Assert.True(File.Exists(mdPath));
            Assert.True(File.Exists(jsonPath));

            var report = JsonSerializer.Deserialize<RatingsReportDocument>(File.ReadAllText(jsonPath), HistoricalJson.Options);
            Assert.NotNull(report);
            Assert.Equal(1, report.FitSummary.RacesCount);
            Assert.Equal(2, report.FitSummary.TotalDuels); // 1 race + 1 quali
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static void Shuffle<T>(List<T> list, Xoshiro256StarStar rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = (int)(rng.NextDouble() * (i + 1));
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private sealed class SyntheticDriver(string driverId, int startSeason, int careerLength, double initialSkill)
    {
        public string DriverId { get; } = driverId;
        public int StartSeason { get; } = startSeason;
        public int CareerLength { get; } = careerLength;
        public double CurrentSkill { get; set; } = initialSkill;
    }
}
