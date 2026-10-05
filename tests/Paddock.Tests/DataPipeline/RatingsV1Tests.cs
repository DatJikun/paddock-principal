using System.Globalization;
using System.Text.Json;
using Paddock.Data.Historical;
using Paddock.DataPipeline;
using Paddock.Domain.Random;
using Xunit.Abstractions;

namespace Paddock.Tests.DataPipeline;

public class RatingsV1Tests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Slow")]
    public void SyntheticWithCarEffects_RecoversDriverPeakAndCarEffect_AndBeatsOrMatchesV0()
    {
        var world = BuildWorld(seed: 7UL, seasons: 30, carBaseSd: 1.2);

        var run = RatingsCommand.RunModel(world.Races, world.Results, 1, 30);
        output.WriteLine($"v1 fit: {run.Fit.Iterations} iterations, converged={run.Fit.Converged}, duels={run.Duels.Count}");

        // Same ranked set as the report: >= 20 teammate duels; v1 peak = smoothed peak (>= 4 seasons) else v0 peak.
        var ranked = run.DriverMetrics.Values
            .Where(m => m.TotalDuels >= 20)
            .OrderBy(m => m.DriverId, StringComparer.Ordinal)
            .ToList();
        Assert.True(ranked.Count >= 20);

        var truePeaks = new List<double>();
        var v1Peaks = new List<double>();
        foreach (var m in ranked)
        {
            var seasons = world.TrueSkill
                .Where(kvp => kvp.Key.Id == m.DriverId)
                .OrderBy(kvp => kvp.Key.Season)
                .Select(kvp => (kvp.Key.Season, Skill: kvp.Value, Se: 0.0))
                .ToList();
            truePeaks.Add(RatingsModel.CalculatePeak(seasons).Peak);
            v1Peaks.Add(RatingsCurveModel.Build(m.Seasons, null, RatingsModel.DefaultLambdaCurve)?.PeakValue ?? m.CareerPeak);
        }

        var v1Rho = RatingsModel.SpearmanCorrelation(truePeaks, v1Peaks);

        // v0 on the very same data: teammate duels only, no car effect.
        var v0Peaks = FitV0Peaks(world, ranked.Select(r => r.DriverId).ToList());
        var v0Rho = RatingsModel.SpearmanCorrelation(truePeaks, v0Peaks);

        var trueCars = new List<double>();
        var fittedCars = new List<double>();
        foreach (var effect in run.CarEffects)
        {
            trueCars.Add(world.TrueCar[(effect.Key, effect.Season)]);
            fittedCars.Add(effect.Effect);
        }

        var carRho = RatingsModel.SpearmanCorrelation(trueCars, fittedCars);

        output.WriteLine($"driver peak Spearman: v1={v1Rho:F3}, v0={v0Rho:F3}; car effect Spearman v1={carRho:F3}");

        Assert.True(v1Rho >= 0.85, $"v1 driver Spearman {v1Rho:F3} < 0.85");
        Assert.True(carRho >= 0.8, $"car effect Spearman {carRho:F3} < 0.8");
        Assert.True(v1Rho >= v0Rho - 0.02, $"v1 {v1Rho:F3} must be >= v0 {v0Rho:F3} - 0.02");
    }

    [Fact]
    public void CrossDuels_HaveFieldSizeWeightAndRespectFilters()
    {
        var races = new List<HistoricalRace>
        {
            new(1960, 1, "GP", "c", "1960-05-01", null, string.Empty, false),
            new(1960, 2, "Indy", "indy", "1960-05-30", null, string.Empty, true),
        };

        HistoricalResult R(int round, string driver, string team, int pos, bool shared = false, string status = "Finished") =>
            new(1960, round, driver, team, "1", pos, pos.ToString(CultureInfo.InvariantCulture), status, 0, pos, 50, null, null, true, false, shared);

        var results = new List<HistoricalResult>
        {
            R(1, "a1", "alpha", 1),
            R(1, "a2", "alpha", 2),
            R(1, "b1", "beta", 3),
            R(1, "c1", "gamma", 4),
            R(1, "shared", "delta", 5, shared: true),
            R(1, "dns", "delta", 0, status: "Did not start"),
            R(2, "i1", "alpha", 1),
            R(2, "i2", "beta", 2),
        };

        // Field size 4 after filters (a1, a2, b1, c1): weight 1/3 for cross duels.
        var duels = RatingsModel.ExtractDuels(races, results, 1960, 1960, includeCross: true);
        var cross = duels.Where(d => d.LoserConstructorId is not null).ToList();
        var teammate = duels.Where(d => d.LoserConstructorId is null).ToList();

        // Pairs from different constructors: a1-b1, a1-c1, a2-b1, a2-c1, b1-c1 = 5, each race + grid.
        Assert.Equal(10, cross.Count);
        Assert.All(cross, d => Assert.Equal(1.0 / 3.0, d.Weight, 12));
        Assert.All(cross, d => Assert.NotEqual(d.ConstructorId, d.LoserConstructorId));
        Assert.DoesNotContain(duels, d => d.Round == 2);
        Assert.DoesNotContain(duels, d => d.WinnerDriverId is "shared" or "dns" || d.LoserDriverId is "shared" or "dns");

        // Teammate duels keep the v0 weight.
        Assert.Equal(2, teammate.Count);
        Assert.All(teammate, d => Assert.Equal(1.0, d.Weight));

        var overridden = RatingsModel.ExtractDuels(races, results, 1960, 1960, includeCross: true, wCross: 0.25);
        Assert.All(overridden.Where(d => d.LoserConstructorId is not null), d => Assert.Equal(0.25, d.Weight));

        // v0 behaviour is unchanged when cross duels are off.
        Assert.Equal(2, RatingsModel.ExtractDuels(races, results, 1960, 1960).Count);
    }

    [Fact]
    public void CareerCurve_RecoversPlantedRisePeakDecline()
    {
        var rng = Xoshiro256StarStar.FromSeed(2024UL);
        var seasons = new List<(int Season, double Skill, double Se)>();
        for (var t = 1; t <= 13; t++)
        {
            var shape = t <= 7 ? -0.12 * (7 - t) * (7 - t) : -0.05 * (t - 7) * (t - 7);
            var noise = (rng.NextDouble() - 0.5) * 0.4;
            seasons.Add((1970 + t, shape + noise, 0.2));
        }

        var curve = RatingsCurveModel.Build(seasons, birthYear: 1940, lambda: RatingsModel.DefaultLambdaCurve);

        Assert.NotNull(curve);
        Assert.InRange(curve.PeakSeason, 1970 + 6, 1970 + 8);
        Assert.Equal(curve.PeakSeason - 1940, curve.AgeAtPeak);
        Assert.True(curve.DeclineRate < 0, $"decline rate {curve.DeclineRate:F3}");
        Assert.True(curve.Ceiling > curve.PeakValue);
        Assert.True(curve.CeilingIsEstimate);

        var noBirth = RatingsCurveModel.Build(seasons, birthYear: null, lambda: 3.0);
        Assert.Null(noBirth!.AgeAtPeak);
    }

    [Fact]
    public void CareerCurve_ShortCareersHaveNoCurve_AndGapsAreInterpolated()
    {
        var three = new List<(int, double, double)> { (1, 0.0, 0.1), (2, 1.0, 0.1), (3, 0.5, 0.1) };
        Assert.Null(RatingsCurveModel.Build(three, null, 3.0));

        // A missing season in the middle of a straight line is interpolated onto the line.
        var gap = new List<(int, double, double)> { (1, 0.0, 0.1), (2, 1.0, 0.1), (4, 3.0, 0.1), (5, 4.0, 0.1) };
        var smoothed = RatingsCurveModel.Smooth(gap, 3.0);
        Assert.Equal(5, smoothed.Length);
        Assert.Equal(2.0, smoothed[2], 6);
    }

    [Fact]
    public void Mapping_IsMonotoneWithDocumentedAnchors_AndStarBoundaries()
    {
        var lastOverall = 0;
        var lastStars = -1.0;
        for (var i = 0; i <= 1000; i++)
        {
            var p = i / 1000.0;
            var overall = RatingsMapping.Overall(p);
            var stars = RatingsMapping.Stars(p);
            Assert.InRange(overall, 1, 100);
            Assert.InRange(stars, 0.0, 5.0);
            Assert.Equal(0.0, stars * 2 % 1);
            Assert.True(overall >= lastOverall, $"overall decreased at {p}");
            Assert.True(stars >= lastStars, $"stars decreased at {p}");
            lastOverall = overall;
            lastStars = stars;
        }

        Assert.Equal(60, RatingsMapping.Overall(0.50));
        Assert.Equal(95, RatingsMapping.Overall(0.99));
        Assert.Equal(100, RatingsMapping.Overall(1.0));

        Assert.Equal(0.0, RatingsMapping.Stars(0.0));
        Assert.Equal(0.0, RatingsMapping.Stars(0.0399));
        Assert.Equal(0.5, RatingsMapping.Stars(0.04));
        Assert.Equal(4.5, RatingsMapping.Stars(0.9699));
        Assert.Equal(5.0, RatingsMapping.Stars(0.97));
        Assert.Equal(5.0, RatingsMapping.Stars(1.0));

        Assert.Equal(0.5, RatingsMapping.Percentile(5.0, [5.0]));
        Assert.Equal(0.875, RatingsMapping.Percentile(4.0, [1.0, 2.0, 3.0, 4.0]), 12);
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void Lineage_LinksCarEffectAcrossARename()
    {
        const int seasons = 6;
        var world = BuildWorld(seed: 11UL, seasons: seasons, carBaseSd: 1.0, racesPerSeason: 10, renameSeason: seasons);

        var lineage = new ConstructorLineageMap(
        [
            new LineageEntry("lin_zero", "team_0", 1, seasons - 1),
            new LineageEntry("lin_zero", "team_0_new", seasons, null),
        ]);

        Assert.Equal("lin_zero", lineage.Resolve("team_0_new", seasons));
        Assert.Equal("lin_zero", lineage.Resolve("team_0", 2));
        Assert.Equal("team_0", lineage.Resolve("team_0", seasons));
        Assert.Equal("team_1", lineage.Resolve("team_1", 3));

        var linked = RatingsCommand.RunModel(world.Races, world.Results, 1, seasons, lambdaCTime: 1000.0, lineage: lineage);
        var unlinked = RatingsCommand.RunModel(world.Races, world.Results, 1, seasons, lambdaCTime: 1000.0);

        Assert.DoesNotContain(linked.CarEffects, c => c.Key is "team_0" or "team_0_new");
        Assert.Equal(seasons, linked.CarEffects.Count(c => c.Key == "lin_zero"));
        Assert.Contains(unlinked.CarEffects, c => c.Key == "team_0_new" && c.Season == seasons);
        Assert.Equal(1, unlinked.CarEffects.Count(c => c.Key == "team_0_new"));

        // With a very strong time penalty the renamed team's last season follows the previous one.
        var before = linked.CarEffects.Single(c => c.Key == "lin_zero" && c.Season == seasons - 1).Effect;
        var after = linked.CarEffects.Single(c => c.Key == "lin_zero" && c.Season == seasons).Effect;
        Assert.True(Math.Abs(after - before) < 0.05, $"|{after:F3} - {before:F3}| too large");

        var parsed = ConstructorLineageMap.Parse("""
            {"lineages":[{"lineage_id":"x","entries":[
              {"constructorId":"a","from":1950,"to":1960},
              {"constructorId":"b","from":1961,"to":null}]}]}
            """);
        Assert.Equal("x", parsed.Resolve("a", 1955));
        Assert.Equal("a", parsed.Resolve("a", 1970));
        Assert.Equal("x", parsed.Resolve("b", 2020));
    }

    [Fact]
    public void PlantedStrongDriverInWeakCar_IsRankedAboveWeakDriverInBestCar()
    {
        const int seasons = 10;
        var world = BuildWorld(seed: 21UL, seasons: seasons, carBaseSd: 0.5, planted: true);

        // The raw results mislead: the payer in the +2.0 car finishes ahead of the star in the -2.0 car.
        double MeanFinish(string id) => world.Results.Where(r => r.DriverId == id).Average(r => (double)r.Position);
        var starRaw = MeanFinish(PlantedStarId);
        var payerRaw = MeanFinish(PlantedPayerId);
        Assert.True(payerRaw < starRaw, $"scenario broken: payer {payerRaw:F1} should finish ahead of star {starRaw:F1} on raw results");

        var run = RatingsCommand.RunModel(world.Races, world.Results, 1, seasons);

        double Peak(string id) => RatingsCurveModel.Build(run.DriverMetrics[id].Seasons, null, RatingsModel.DefaultLambdaCurve)?.PeakValue
            ?? run.DriverMetrics[id].CareerPeak;

        var starPeak = Peak(PlantedStarId);
        var payerPeak = Peak(PlantedPayerId);
        var rankOfStar = run.DriverMetrics.Keys.Count(id => Peak(id) > starPeak) + 1;
        output.WriteLine($"star fitted peak {starPeak:F2} (rank {rankOfStar}), payer {payerPeak:F2}; raw mean finish star {starRaw:F1} vs payer {payerRaw:F1}");

        Assert.True(starPeak > payerPeak + 1.0, $"star {starPeak:F2} should clearly beat payer {payerPeak:F2}");
        Assert.True(rankOfStar <= 3, $"star ranked {rankOfStar}, expected top 3");

        // And the model attributes the gap to the cars.
        double MeanCar(string key) => run.CarEffects.Where(c => c.Key == key).Average(c => c.Effect);
        var weak = MeanCar("team_9");
        var strong = MeanCar("team_0");
        output.WriteLine($"fitted car effect team_9 {weak:F2}, team_0 {strong:F2}");
        Assert.True(strong - weak > 2.0, $"car gap {strong - weak:F2} too small");
        var others = Enumerable.Range(1, 8).Select(k => MeanCar($"team_{k}")).ToList();
        Assert.True(weak < others.Min(), "weakest car must be team_9");
        Assert.True(strong > others.Max(), "best car must be team_0");
    }

    [Fact]
    public void LineageFile_ParsesAndLinksARealRename()
    {
        var path = Path.Combine(JolpicaCache.FindRepoRoot(), "data", "authored", "teams", "lineage.json");
        var map = ConstructorLineageMap.Parse(File.ReadAllText(path));

        // tyrrell 1970-1998 resolves to a lineage key (same key for every season of that entry).
        var key = map.Resolve("tyrrell", 1970);
        Assert.Equal(key, map.Resolve("tyrrell", 1998));
        Assert.NotEqual("tyrrell", key);
    }

    [Fact]
    public void Determinism_V1ReportIsByteIdentical()
    {
        var world = BuildWorld(seed: 5UL, seasons: 8, carBaseSd: 1.0, racesPerSeason: 6);
        var lineage = new ConstructorLineageMap([new LineageEntry("l", "team_0", 1, null)]);

        var json1 = JsonSerializer.Serialize(
            RatingsCommand.BuildReport(world.Races, world.Results, world.Drivers, [], 1, 8, lineage: lineage),
            HistoricalJson.Options);
        var json2 = JsonSerializer.Serialize(
            RatingsCommand.BuildReport(world.Races, world.Results, world.Drivers, [], 1, 8, lineage: lineage),
            HistoricalJson.Options);

        Assert.Equal(json1, json2);

        var report = JsonSerializer.Deserialize<RatingsReportDocument>(json1, HistoricalJson.Options)!;
        Assert.NotEmpty(report.CarEffectsByDecade);
        Assert.NotEmpty(report.DriverRatings);
        Assert.All(report.AllRankedDrivers, r => Assert.InRange(r.Overall, 1, 100));
        Assert.Contains("Car effect per decade", RatingsMarkdown.Format(report));
    }

    [Fact]
    public async Task RatingsCommand_ValidatesV1Flags()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        Assert.Equal(1, await PipelineCommands.ExecuteAsync(["ratings", "--lambda-c0", "0"], stdout, stderr));
        Assert.Contains("--lambda-c0", stderr.ToString());

        stderr.GetStringBuilder().Clear();
        Assert.Equal(1, await PipelineCommands.ExecuteAsync(["ratings", "--w-cross", "-1"], stdout, stderr));
        Assert.Contains("must be positive", stderr.ToString());

        stderr.GetStringBuilder().Clear();
        Assert.Equal(1, await PipelineCommands.ExecuteAsync(["ratings", "--lambda-curve", "abc"], stdout, stderr));
        Assert.Contains("Invalid --lambda-curve", stderr.ToString());
    }

    private static List<double> FitV0Peaks(World world, IReadOnlyList<string> driverIds)
    {
        var duels = RatingsModel.ExtractDuels(world.Races, world.Results, 1, int.MaxValue);
        var distinct = duels
            .Select(d => (d.WinnerDriverId, d.Season))
            .Concat(duels.Select(d => (d.LoserDriverId, d.Season)))
            .Distinct()
            .OrderBy(p => p.Season)
            .ThenBy(p => p.Item1, StringComparer.Ordinal)
            .ToList();
        var parameters = distinct.Select((p, i) => new DriverSeasonParam(p.Item1, p.Season, i)).ToList();
        var links = new List<TimeLink>();
        foreach (var g in parameters.GroupBy(p => p.DriverId))
        {
            var list = g.OrderBy(p => p.Season).ToList();
            for (var i = 1; i < list.Count; i++)
            {
                links.Add(new TimeLink(list[i].Index, list[i - 1].Index, list[i].Season - list[i - 1].Season));
            }
        }

        var fit = RatingsModel.Fit(duels, parameters, links);
        var metrics = RatingsModel.CalculateDriverMetrics(duels, parameters, fit);
        return driverIds.Select(id => metrics[id].CareerPeak).ToList();
    }

    private const string PlantedStarId = "planted_star";
    private const string PlantedPayerId = "planted_payer";

    private sealed record World(
        List<HistoricalRace> Races,
        List<HistoricalResult> Results,
        List<HistoricalDriver> Drivers,
        Dictionary<(string Id, int Season), double> TrueSkill,
        Dictionary<(string Key, int Season), double> TrueCar);

    private static double Gumbel(Xoshiro256StarStar rng)
    {
        var u = Math.Clamp(rng.NextDouble(), 1e-12, 1.0 - 1e-12);
        return -Math.Log(-Math.Log(u));
    }

    /// <summary>
    /// Synthetic world: 10 teams x 2 seats, drivers with 3-10 season careers who are reshuffled between teams
    /// every season, a persistent per-team car effect with a slow drift, and full-field races/grids drawn from
    /// utility = skill + car + Gumbel noise. With <paramref name="renameSeason"/> &gt; 0, team_0 is called
    /// team_0_new from that season on.
    /// </summary>
    private static World BuildWorld(ulong seed, int seasons, double carBaseSd, int racesPerSeason = 16, int renameSeason = 0, bool planted = false)
    {
        var rng = Xoshiro256StarStar.FromSeed(seed);
        const int teams = 10;

        var carBase = new double[teams];
        for (var k = 0; k < teams; k++)
        {
            // Sum of uniforms: roughly normal, deterministic.
            var n = 0.0;
            for (var i = 0; i < 12; i++)
            {
                n += rng.NextDouble();
            }

            carBase[k] = (n - 6.0) * carBaseSd;
        }

        if (planted)
        {
            carBase[0] = 2.0;
            carBase[teams - 1] = -2.0;
        }

        var races = new List<HistoricalRace>();
        var results = new List<HistoricalResult>();
        var trueSkill = new Dictionary<(string, int), double>();
        var trueCar = new Dictionary<(string, int), double>();
        var pool = new List<(string Id, int Start, int Length, double[] Skill)>();
        var active = new List<int>();
        var counter = 0;

        int Spawn(int season)
        {
            var career = 3 + (int)(rng.NextDouble() * 8);
            var skill = new double[career];
            skill[0] = (rng.NextDouble() * 3.0) - 1.5;
            for (var i = 1; i < career; i++)
            {
                skill[i] = skill[i - 1] + (rng.NextDouble() * 0.1) - 0.05;
            }

            pool.Add(($"driver_{++counter}", season, career, skill));
            return pool.Count - 1;
        }

        var starIndex = -1;
        var payerIndex = -1;
        if (planted)
        {
            // Strong driver (skill +2.0) pinned to the weakest car; weak driver (skill -1.0) pinned to the best car.
            pool.Add((PlantedStarId, 1, seasons, Enumerable.Repeat(2.0, seasons).ToArray()));
            starIndex = pool.Count - 1;
            pool.Add((PlantedPayerId, 1, seasons, Enumerable.Repeat(-1.0, seasons).ToArray()));
            payerIndex = pool.Count - 1;
        }

        for (var season = 1; season <= seasons; season++)
        {
            active.RemoveAll(i => pool[i].Start + pool[i].Length <= season);
            if (planted && season == 1)
            {
                active.Add(starIndex);
                active.Add(payerIndex);
            }

            while (active.Count < teams * 2)
            {
                active.Add(Spawn(season));
            }

            var carNow = new double[teams];
            for (var k = 0; k < teams; k++)
            {
                carNow[k] = carBase[k] + (rng.NextDouble() - 0.5) * 0.1 * season / 10.0;
                trueCar[($"team_{k}", season)] = carNow[k];
            }

            if (season > 1)
            {
                for (var i = active.Count - 1; i > 0; i--)
                {
                    var j = (int)(rng.NextDouble() * (i + 1));
                    (active[i], active[j]) = (active[j], active[i]);
                }
            }

            if (planted)
            {
                // Seats 2k and 2k+1 share team k: star in the last team's first seat, payer in team 0's first seat.
                var starSeat = active.IndexOf(starIndex);
                (active[starSeat], active[(teams * 2) - 2]) = (active[(teams * 2) - 2], active[starSeat]);
                var payerSeat = active.IndexOf(payerIndex);
                (active[payerSeat], active[0]) = (active[0], active[payerSeat]);
            }

            foreach (var idx in active)
            {
                trueSkill[(pool[idx].Id, season)] = pool[idx].Skill[season - pool[idx].Start];
            }

            for (var round = 1; round <= racesPerSeason; round++)
            {
                races.Add(new HistoricalRace(season, round, $"GP {season}/{round}", "circuit", $"{season:D4}-05-01", null, string.Empty, false));

                var seats = Enumerable.Range(0, teams * 2).ToList();
                var racePerf = seats.Select(s =>
                    trueSkill[(pool[active[s]].Id, season)] + carNow[s / 2] + Gumbel(rng)).ToList();
                var gridPerf = seats.Select(s =>
                    trueSkill[(pool[active[s]].Id, season)] + carNow[s / 2] + Gumbel(rng)).ToList();

                var racePos = Rank(racePerf);
                var gridPos = Rank(gridPerf);

                foreach (var s in seats)
                {
                    var team = s / 2;
                    var constructor = team == 0 && renameSeason > 0 && season >= renameSeason ? "team_0_new" : $"team_{team}";
                    results.Add(new HistoricalResult(
                        season, round, pool[active[s]].Id, constructor, "1",
                        racePos[s], racePos[s].ToString(CultureInfo.InvariantCulture), "Finished", 0,
                        gridPos[s], 50, null, null, true, false, false));
                }
            }
        }

        var drivers = pool
            .Select(p => new HistoricalDriver(p.Id, "Driver", p.Id, null, null, null, null, null))
            .ToList();

        return new World(races, results, drivers, trueSkill, trueCar);
    }

    private static int[] Rank(IReadOnlyList<double> perf)
    {
        var order = Enumerable.Range(0, perf.Count).OrderByDescending(i => perf[i]).ThenBy(i => i).ToArray();
        var pos = new int[perf.Count];
        for (var rank = 0; rank < order.Length; rank++)
        {
            pos[order[rank]] = rank + 1;
        }

        return pos;
    }
}
