using System.Collections;
using System.Reflection;
using Paddock.Domain.Random;
using Paddock.Domain.Spy;
using Paddock.Simulation.Racing.Incidents;
using Paddock.Simulation.Racing.Pace;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Reliability;
using Paddock.Simulation.Racing.Weather;
using Paddock.Simulation.Racing.Weekend;
using Paddock.SimRunner;

namespace Paddock.Tests.Racing.Weekend;

/// <summary>INV-003 (truth versus knowledge) and INV-004 (isolated RNG streams) for the orchestrator.</summary>
public class WeekendInvariantTests
{
    // ---- INV-004: what the weather and the lap noise are drawn from -------------------------------------------

    private static RaceEntry Extra(RaceEntry like) => like with
    {
        CarId = "car-extra",
        ConstructorId = "team-extra",
        Drivers = [like.Drivers[0] with { DriverId = "driver-extra" }],
    };

    private static void AssertSameNoise(RaceWeekendResult a, RaceWeekendResult b, IEnumerable<string> carIds)
    {
        var left = a.LapRecords.Where(r => !r.Neutralised).ToDictionary(r => (r.CarId, r.Lap));
        var right = b.LapRecords.Where(r => !r.Neutralised).ToDictionary(r => (r.CarId, r.Lap));
        var compared = 0;
        foreach (var car in carIds)
        {
            foreach (var (key, record) in left.Where(pair => pair.Key.CarId == car))
            {
                if (right.TryGetValue(key, out var other))
                {
                    Assert.Equal(record.NoiseSeconds, other.NoiseSeconds);
                    compared++;
                }
            }
        }

        Assert.True(compared > 200, $"only {compared} laps were comparable");
    }

    private static void AssertSameWeather(RaceWeekendResult a, RaceWeekendResult b)
    {
        Assert.Equal(a.TruthWeather.Samples, b.TruthWeather.Samples);
    }

    [Theory]
    [InlineData(1955)]
    [InlineData(2012)]
    public void AddingOneEntry_ChangesNeitherTheWeatherNorAnyOtherCarsNoise(int season)
    {
        var entries = SyntheticField.For(season);
        var without = WeekendTestKit.Run(WeekendTestKit.Input(season, entries: entries));
        var with = WeekendTestKit.Run(WeekendTestKit.Input(season, entries: entries.Add(Extra(entries[^1]))));

        AssertSameWeather(without, with);
        AssertSameNoise(without, with, entries.Select(e => e.CarId));
        Assert.Contains(with.CarResults, c => c.CarId == "car-extra");
    }

    private sealed class NeverStops : IRaceStrategist
    {
        public StrategyDecision Decide(KnowledgeSnapshot snapshot) =>
            new(StrategyAction.StayOut, null, 0, false, PaceMode.Standard);
    }

    private sealed class StopsOnLapFive : IRaceStrategist
    {
        public StrategyDecision Decide(KnowledgeSnapshot snapshot) => snapshot.Lap == 5
            ? new StrategyDecision(StrategyAction.PitNow, snapshot.Compounds[0].Id, 0, false, PaceMode.Push)
            : new StrategyDecision(StrategyAction.StayOut, null, 0, false, PaceMode.Standard);
    }

    [Theory]
    [InlineData(1955)]
    [InlineData(2012)]
    public void ChangingOneCarsPitDecision_ChangesNeitherTheWeatherNorAnyCarsNoise(int season)
    {
        var input = WeekendTestKit.Input(season);
        var baseline = RaceWeekend.Run(input, NullSink.Instance);
        var changed = RaceWeekend.Run(
            input,
            NullSink.Instance,
            request => request.Entry.CarId == "car-03" ? new StopsOnLapFive() : new RuleBasedStrategist(
                request.Entry.StrategistSkill,
                request.Calculators,
                request.AiDecisionsStream,
                request.Options));

        // The decision really changed that car's race ...
        Assert.DoesNotContain(baseline.PitStops, s => s.CarId == "car-03" && s.Lap == 5);
        Assert.Contains(changed.PitStops, s => s.CarId == "car-03" && s.Lap == 5);

        // ... and moved nobody's draws, its own included.
        AssertSameWeather(baseline, changed);
        AssertSameNoise(baseline, changed, SyntheticField.For(season).Select(e => e.CarId));
    }

    [Fact]
    public void EveryCarsStrategistCanBeReplaced_AndANeverStoppingFieldNeverStops()
    {
        var result = RaceWeekend.Run(WeekendTestKit.Input(2012), NullSink.Instance, _ => new NeverStops());

        Assert.Empty(result.PitStops.Where(s => !s.IsRepair));
        Assert.All(result.CarResults, c => Assert.Single(c.CompoundsUsed));
    }

    [Fact]
    public void TheNamedStreamsAreTheOnesTheConstantsListed()
    {
        // The orchestrator draws from Weather, LapNoise, Incidents, Failures, PitStops and AiDecisions only (all in the stream list).
        var used = new[]
        {
            RngStreamName.Weather,
            RngStreamName.LapNoise,
            RngStreamName.Incidents,
            RngStreamName.Failures,
            RngStreamName.PitStops,
            RngStreamName.AiDecisions,
        };

        Assert.All(used, name => Assert.Contains(name, RngStreamName.All));
        Assert.Equal(
            RngStream.Derive(5, RngStreamName.Incidents, 1955, 1).State,
            IncidentSampler.DeriveRaceStream(5, 1955, 1).State);
        Assert.Equal(
            RngStream.Derive(5, RngStreamName.Failures, 1955, 1).State,
            FailureSampler.DeriveRaceStream(5, 1955, 1).State);
        Assert.Equal(
            RngStream.Derive(5, RngStreamName.PitStops, 1955, 1).State,
            PitStopModel.DeriveRaceStream(5, 1955, 1).State);
        Assert.Equal(
            RngStream.Derive(5, RngStreamName.AiDecisions, 1955, 1).State,
            RuleBasedStrategist.DeriveRaceStream(5, 1955, 1).State);
    }

    // ---- INV-003: what the strategist is handed ---------------------------------------------------------------

    private sealed class RainyClimate : IClimateSource
    {
        public ClimateProfile For(string circuitId, int month) => new(ClimateClass.Maritime, RainBand.High, 1.0, 8, 14, 0.8);
    }

    private sealed class Recorder(IRaceStrategist inner, List<KnowledgeSnapshot> seen) : IRaceStrategist
    {
        public StrategyDecision Decide(KnowledgeSnapshot snapshot)
        {
            lock (seen)
            {
                seen.Add(snapshot);
            }

            return inner.Decide(snapshot);
        }
    }

    private static readonly Type[] TruthTypes =
    [
        typeof(TruthWeather),
        typeof(WeatherSample),
        typeof(RngStream),
        typeof(Xoshiro256StarStar),
        typeof(IncidentResult),
        typeof(IncidentOutcome),
        typeof(FailureSample),
        typeof(MechanicalFailure),
        typeof(LapInputs),
        typeof(LapTimeBreakdown),
        typeof(RaceWeekendResult),
        typeof(RaceWeekendInput),
        typeof(RaceEntry),
    ];

    private static IEnumerable<object> Walk(object? root, HashSet<object> seen)
    {
        if (root is null || root is string || root.GetType().IsPrimitive || root.GetType().IsEnum || !seen.Add(root))
        {
            yield break;
        }

        yield return root;
        if (root is IEnumerable items)
        {
            foreach (var item in items)
            {
                foreach (var inner in Walk(item, seen))
                {
                    yield return inner;
                }
            }
        }

        var type = root.GetType();
        if (type.Namespace?.StartsWith("Paddock", StringComparison.Ordinal) != true)
        {
            yield break;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        foreach (var field in type.GetFields(flags))
        {
            foreach (var inner in Walk(field.GetValue(root), seen))
            {
                yield return inner;
            }
        }
    }

    [Fact]
    public void EverySnapshotTheOrchestratorHandsOver_HoldsNoTruth_AndItsForecastIsNotTheWeather()
    {
        // A race with rain, so the forecast has something to be wrong about.
        var input = WeekendTestKit.Input(1988) with { Climate = new RainyClimate() };
        var snapshots = new List<KnowledgeSnapshot>();
        var result = RaceWeekend.Run(
            input,
            NullSink.Instance,
            request => new Recorder(
                new RuleBasedStrategist(request.Entry.StrategistSkill, request.Calculators, request.AiDecisionsStream, request.Options),
                snapshots));

        Assert.True(result.TruthWeather.HasRain);
        Assert.NotEmpty(snapshots);
        foreach (var snapshot in snapshots)
        {
            foreach (var node in Walk(snapshot, new HashSet<object>(ReferenceEqualityComparer.Instance)))
            {
                Assert.DoesNotContain(node.GetType(), TruthTypes);
                Assert.False(node is RaceWeekendResult, "the snapshot holds a result");
            }

            // The forecast is a noisy view: not one of its steps equals the true intensity at that minute.
            if (snapshot.Forecast is { } forecast)
            {
                foreach (var step in forecast.Steps)
                {
                    var truth = result.TruthWeather.Samples[forecast.FromMinute + step.HorizonMinutes].RainIntensity;
                    Assert.NotEqual(truth, step.RawIntensityEstimate);
                }
            }
        }

        Assert.Contains(snapshots, s => s.Forecast is { Steps.Length: > 0 });
    }

    [Fact]
    public void TheStrategist_IsOnlyAskedThroughTheSnapshotMethod()
    {
        var decide = typeof(IRaceStrategist).GetMethod(nameof(IRaceStrategist.Decide))!;
        Assert.Equal([typeof(KnowledgeSnapshot)], decide.GetParameters().Select(p => p.ParameterType).ToArray());

        // The only other thing the orchestrator offers a strategist is its construction: entry, calculators, the AiDecisions stream.
        var request = typeof(StrategistRequest).GetProperties().Select(p => p.PropertyType).ToHashSet();
        Assert.DoesNotContain(typeof(TruthWeather), request);
        Assert.DoesNotContain(typeof(RaceWeekendResult), request);
        Assert.Contains(typeof(RngStream), request);
    }

    [Fact]
    public void TheResultsDeveloperOnlyParts_AreNamedForWhatTheyAre()
    {
        var properties = typeof(RaceWeekendResult).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.Contains(nameof(RaceWeekendResult.TruthWeather), properties);
        Assert.Contains(nameof(RaceWeekendResult.LapRecords), properties);
        Assert.DoesNotContain(properties, name => name.Contains("Trace", StringComparison.Ordinal));
    }
}
