using System.Collections.Immutable;
using System.Reflection;
using Paddock.Domain.Random;
using Paddock.Domain.Spy;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Tyres;
using Paddock.Simulation.Racing.Weather;

namespace Paddock.Tests.Racing.Pits;

/// <summary>Decisions of the rule-based strategist. Its numbers are ESTIMATE; the assertions are about direction and rules, not exact values.</summary>
public class StrategistTests
{
    private static RuleBasedStrategist Strategist(int season, int skill = 100, ulong seed = PitTestKit.Seed, StrategistOptions? options = null) =>
        new(skill, PitTestKit.T30Calculators(season), RuleBasedStrategist.DeriveRaceStream(seed, season, 1), options);

    private sealed record Traced(StrategyDecision Decision, DecisionTrace Trace);

    /// <summary>A strategist wired to a <see cref="MemorySink"/>, to read back the trace of a decision.</summary>
    private sealed class Rig
    {
        public Rig(int season, int skill = 100, ulong seed = PitTestKit.Seed, StrategistOptions? options = null)
        {
            Strategist = new RuleBasedStrategist(
                skill,
                PitTestKit.T30Calculators(season),
                RuleBasedStrategist.DeriveRaceStream(seed, season, 1),
                options,
                new StrategistTracing(Sink, new WeekendKey(season, 1), "strategist-1"));
        }

        public MemorySink Sink { get; } = new();

        public RuleBasedStrategist Strategist { get; }

        public Traced Decide(KnowledgeSnapshot snapshot)
        {
            var decision = Strategist.Decide(snapshot);
            return new Traced(decision, Sink.Traces[^1]);
        }
    }

    // ---- INV-003: the snapshot holds knowledge only ----

    private static readonly HashSet<string> AllowedSnapshotMembers =
    [
        nameof(KnowledgeSnapshot.Season),
        nameof(KnowledgeSnapshot.Lap),
        nameof(KnowledgeSnapshot.TotalLaps),
        nameof(KnowledgeSnapshot.ReferenceLapSeconds),
        nameof(KnowledgeSnapshot.RaceMinute),
        nameof(KnowledgeSnapshot.Rules),
        nameof(KnowledgeSnapshot.PitLaneLossSeconds),
        nameof(KnowledgeSnapshot.TankCapacityKg),
        nameof(KnowledgeSnapshot.Compounds),
        nameof(KnowledgeSnapshot.Car),
        nameof(KnowledgeSnapshot.Gaps),
        nameof(KnowledgeSnapshot.Forecast),
    ];

    private static IEnumerable<Type> ReachableTypes(Type root)
    {
        var seen = new HashSet<Type>();
        var queue = new Queue<Type>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            if (!seen.Add(type))
            {
                continue;
            }

            yield return type;
            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    queue.Enqueue(argument);
                }
            }

            if (type.Namespace is null || !type.Namespace.StartsWith("Paddock", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                queue.Enqueue(property.PropertyType);
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                queue.Enqueue(field.FieldType);
            }
        }
    }

    [Fact]
    public void Snapshot_HasOnlyKnowledgeMembers()
    {
        var members = typeof(KnowledgeSnapshot)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(name => name != "EqualityContract")
            .ToHashSet();

        Assert.Equal(AllowedSnapshotMembers, members);
    }

    [Fact]
    public void Snapshot_ContainsNoTruthTypes_AndNothingNamedLikeTruth()
    {
        var forbiddenNamespaces = new[] { "Paddock.Simulation.Racing.Reliability", "Paddock.Simulation.Racing.Incidents" };
        var forbiddenTypes = new[] { typeof(TruthWeather), typeof(WeatherSample), typeof(RngStream), typeof(Xoshiro256StarStar) };

        foreach (var type in ReachableTypes(typeof(KnowledgeSnapshot)))
        {
            Assert.DoesNotContain(type, forbiddenTypes);
            Assert.DoesNotContain(type.Namespace ?? string.Empty, forbiddenNamespaces);
            if (type.Namespace?.StartsWith("Paddock", StringComparison.Ordinal) == true)
            {
                Assert.DoesNotContain("Truth", type.Name, StringComparison.OrdinalIgnoreCase);
                foreach (var member in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    Assert.DoesNotContain("Truth", member.Name, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Wetness", member.Name, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Seed", member.Name, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    [Fact]
    public void Snapshot_WeatherIsAForecast_NotTheTruth()
    {
        var forecast = typeof(KnowledgeSnapshot).GetProperty(nameof(KnowledgeSnapshot.Forecast))!;
        Assert.Equal(typeof(WeatherForecast), Nullable.GetUnderlyingType(forecast.PropertyType) ?? forecast.PropertyType);
    }

    [Fact]
    public void Strategist_TakesOnlyAKnowledgeSnapshot()
    {
        var decide = typeof(IRaceStrategist).GetMethod(nameof(IRaceStrategist.Decide))!;
        Assert.Equal([typeof(KnowledgeSnapshot)], decide.GetParameters().Select(p => p.ParameterType).ToArray());
        Assert.Equal(typeof(StrategyDecision), decide.ReturnType);
    }

    // ---- Decisions record their options ----

    private static KnowledgeSnapshot MidRaceSnapshot(int season = 2012, int lap = 12, int age = 12, string compound = "C4") =>
        PitTestKit.Snapshot(season, lap, 40, PitTestKit.Car(compound, age, fuel: 60, burn: 1.5, used: [compound]));

    [Fact]
    public void Decision_ListsOptionsWithUtilities_AndChoosesTheBest()
    {
        var record = new Rig(2012, skill: 70).Decide(MidRaceSnapshot()).Trace;

        Assert.True(record.Options.Count >= 4);
        Assert.Equal(record.Options.Count, record.Options.Select(o => o.Id).Distinct().Count());
        Assert.Contains(record.Options, o => o.Id.StartsWith("stay_out/", StringComparison.Ordinal));
        Assert.Contains(record.Options, o => o.Id.StartsWith("pit_now/", StringComparison.Ordinal));
        Assert.All(record.Options, o => Assert.True(double.IsFinite(o.Utility)));

        var best = record.Options.MaxBy(o => o.Utility)!;
        Assert.Equal(best.Id, record.ChosenOptionId);
        Assert.False(string.IsNullOrWhiteSpace(record.Reason));
        Assert.Equal("lap_check:car-1:lap:12", record.Trigger);
        Assert.Equal(70, record.Level);
        Assert.Equal("strategist-1", record.Who);
        Assert.Equal(new WeekendKey(2012, 1), record.Weekend);
        Assert.Empty(record.TruthContext);
        Assert.False(record.IsKeyDecision);
    }

    [Fact]
    public void OptionFactors_SumToTheUtility_AndNameTheirTerms()
    {
        var record = new Rig(2012, skill: 40).Decide(MidRaceSnapshot()).Trace;

        foreach (var option in record.Options)
        {
            Assert.Equal(option.Utility, option.Factors.Sum(f => f.Contribution), 6);
            var names = option.Factors.Select(f => f.Name).ToArray();
            Assert.Contains("tyre_loss", names);
            Assert.Contains("stops", names);
            Assert.Contains("perception_noise", names);
        }

        // Staying out on a set that has a long stint ahead costs tyres; a pit stop costs stop time.
        var stay = record.Options.First(o => o.Id.StartsWith("stay_out/", StringComparison.Ordinal));
        var pit = record.Options.First(o => o.Id.StartsWith("pit_now/", StringComparison.Ordinal));
        Assert.True(pit.Factors.Single(f => f.Name == "stops").Contribution < 0);
        Assert.NotEqual(stay.Id, pit.Id);
    }

    [Fact]
    public void ASkilledStrategist_HasNoPerceptionNoise_ARawOneDoes()
    {
        var snapshot = MidRaceSnapshot();
        var exact = new Rig(2012, skill: 100).Decide(snapshot).Trace;
        var rough = new Rig(2012, skill: 0).Decide(snapshot).Trace;

        Assert.All(exact.Options, o => Assert.Equal(0, o.Factors.Single(f => f.Name == "perception_noise").Contribution));
        Assert.Contains(rough.Options, o => o.Factors.Single(f => f.Name == "perception_noise").Contribution != 0);
    }

    [Fact]
    public void SkillOutsideZeroToHundred_IsRejected()
    {
        var calc = PitTestKit.T30Calculators(2012);
        var stream = RuleBasedStrategist.DeriveRaceStream(1, 2012, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuleBasedStrategist(-1, calc, stream));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuleBasedStrategist(101, calc, stream));
    }

    // ---- Determinism ----

    [Fact]
    public void SameSnapshot_GivesTheSameDecision_AndTheStreamIsNotConsumed()
    {
        var stream = RuleBasedStrategist.DeriveRaceStream(PitTestKit.Seed, 2012, 1);
        var before = stream.State;
        var sink = new MemorySink();
        var strategist = new RuleBasedStrategist(30, PitTestKit.T30Calculators(2012), stream, null, new StrategistTracing(sink, new WeekendKey(2012, 1), "strategist-1"));
        var snapshot = MidRaceSnapshot();

        var first = TracedDecision(strategist, sink, snapshot);
        var second = TracedDecision(strategist, sink, snapshot);
        var other = new Rig(2012, skill: 30).Decide(snapshot);

        Assert.Equal(first.Decision.Summary, second.Decision.Summary);
        Assert.Equal(Describe(first), Describe(second));
        Assert.Equal(Describe(first), Describe(other));
        Assert.Equal(before, stream.State);
    }

    private static Traced TracedDecision(RuleBasedStrategist strategist, MemorySink sink, KnowledgeSnapshot snapshot) =>
        new(strategist.Decide(snapshot), sink.Traces[^1]);

    [Fact]
    public void Tracing_IsPassive_DecisionsAndStreamAreTheSameWithAnySink()
    {
        var snapshots = new[]
        {
            MidRaceSnapshot(),
            MidRaceSnapshot(lap: 30, age: 30),
            PitTestKit.Snapshot(2012, 40, 40, PitTestKit.Car("C4", age: 39, fuel: 5, used: ["C3", "C4"])),
        };

        foreach (var skill in new[] { 0, 35, 100 })
        {
            var calc = PitTestKit.T30Calculators(2012);
            var untracedStream = RuleBasedStrategist.DeriveRaceStream(PitTestKit.Seed, 2012, 1);
            var nullStream = RuleBasedStrategist.DeriveRaceStream(PitTestKit.Seed, 2012, 1);
            var memoryStream = RuleBasedStrategist.DeriveRaceStream(PitTestKit.Seed, 2012, 1);
            var untouched = untracedStream.State;
            var memory = new MemorySink();
            var untraced = new RuleBasedStrategist(skill, calc, untracedStream);
            var withNull = new RuleBasedStrategist(skill, calc, nullStream, null, new StrategistTracing(NullSink.Instance, new WeekendKey(2012, 1), "s"));
            var withMemory = new RuleBasedStrategist(skill, calc, memoryStream, null, new StrategistTracing(memory, new WeekendKey(2012, 1), "s"));

            foreach (var snapshot in snapshots)
            {
                var plain = untraced.Decide(snapshot);
                Assert.Equal(plain, withNull.Decide(snapshot));
                Assert.Equal(plain, withMemory.Decide(snapshot));
            }

            Assert.Equal(snapshots.Length, memory.Traces.Count);
            Assert.Equal(untouched, untracedStream.State);
            Assert.Equal(untouched, nullStream.State);
            Assert.Equal(untouched, memoryStream.State);
        }
    }

    [Fact]
    public void Tracing_IsSkippedForADisabledSink_AndRecordsOneTracePerDecisionOtherwise()
    {
        var disabled = new CountingSink(enabled: false);
        var enabled = new CountingSink(enabled: true);
        var calc = PitTestKit.T30Calculators(2012);
        var snapshot = MidRaceSnapshot();

        new RuleBasedStrategist(50, calc, RuleBasedStrategist.DeriveRaceStream(1, 2012, 1), null, new StrategistTracing(disabled, new WeekendKey(2012, 1), "s")).Decide(snapshot);
        new RuleBasedStrategist(50, calc, RuleBasedStrategist.DeriveRaceStream(1, 2012, 1), null, new StrategistTracing(enabled, new WeekendKey(2012, 1), "s")).Decide(snapshot);

        Assert.Equal(0, disabled.Count);
        Assert.Equal(1, enabled.Count);
    }

    [Fact]
    public void Trace_HidesPerceptionNoiseFromThePlayer_AndGivesNoPlayerText()
    {
        var trace = new Rig(2012, skill: 20).Decide(MidRaceSnapshot()).Trace;

        Assert.Null(trace.PlayerReason);
        Assert.All(trace.Options, o =>
        {
            Assert.True(o.PlayerVisible);
            Assert.False(o.Factors.Single(f => f.Name == "perception_noise").PlayerVisible);
            Assert.All(o.Factors.Where(f => f.Name != "perception_noise"), f => Assert.True(f.PlayerVisible));
        });
    }

    private sealed class CountingSink(bool enabled) : ITraceSink
    {
        public int Count { get; private set; }

        public bool IsEnabled => enabled;

        public void Record(DecisionTrace trace) => Count++;
    }

    [Fact]
    public void DifferentSeeds_GiveDifferentNoise_ForAWeakStrategist()
    {
        var snapshot = MidRaceSnapshot();
        var a = new Rig(2012, skill: 0, seed: 1).Decide(snapshot);
        var b = new Rig(2012, skill: 0, seed: 2).Decide(snapshot);

        Assert.NotEqual(Describe(a), Describe(b));
    }

    [Fact]
    public void TwoCars_HaveIndependentNoise_AndNeitherDependsOnTheOther()
    {
        var rig = new Rig(2012, skill: 10);
        var carA = MidRaceSnapshot();
        var carB = carA with { Car = carA.Car with { CarId = "car-2" } };

        var alone = Describe(rig.Decide(carA));
        _ = rig.Decide(carB);
        Assert.Equal(alone, Describe(rig.Decide(carA)));
        Assert.NotEqual(Describe(rig.Decide(carA)), Describe(rig.Decide(carB)));
    }

    private static string Describe(Traced traced) =>
        traced.Decision.Summary + "|" + string.Join(";", traced.Trace.Options.Select(o => o.Id + "=" + o.Utility.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));

    // ---- Rules ----

    [Fact]
    public void MandatoryMix_ForcesAStop_ByTheLastPossibleLap()
    {
        // 2012: two different dry compounds required; on the lap before the last, with one compound used, the only legal plan is to pit now.
        var snapshot = PitTestKit.Snapshot(2012, 39, 40, PitTestKit.Car("C4", age: 38, fuel: 4, burn: 1.5, used: ["C4"]));
        var decision = Strategist(2012).Decide(snapshot);

        Assert.Equal(StrategyAction.PitNow, decision.Action);
        Assert.NotNull(decision.CompoundId);
        Assert.NotEqual("C4", decision.CompoundId);
        Assert.Equal(0, decision.RefuelKg);
    }

    [Fact]
    public void MandatoryMix_IsAlreadyMet_SoAShortRunInDoesNotPit()
    {
        var snapshot = PitTestKit.Snapshot(2012, 39, 40, PitTestKit.Car("C4", age: 5, fuel: 4, burn: 1.5, used: ["C3", "C4"]));

        Assert.NotEqual(StrategyAction.PitNow, Strategist(2012).Decide(snapshot).Action);
    }

    [Fact]
    public void WhereRefuellingIsBanned_NeverAsksForFuel()
    {
        for (var lap = 1; lap < 40; lap++)
        {
            var decision = Strategist(2012, skill: 30).Decide(PitTestKit.Snapshot(2012, lap, 40, PitTestKit.Car("C4", age: lap - 1, fuel: 80 - (lap * 1.5), burn: 1.5)));
            Assert.Equal(0, decision.RefuelKg);
        }
    }

    [Fact]
    public void RunningOutOfFuel_ForcesAStop_WhereRefuellingIsAllowed()
    {
        // 1996: refuelling allowed. The tank holds one more lap, so only a stop now is feasible.
        var snapshot = PitTestKit.Snapshot(1996, 20, 50, PitTestKit.Car("slick.medium", age: 19, fuel: 1.6, burn: 1.5), tankCapacityKg: 100);
        var decision = Strategist(1996).Decide(snapshot);

        Assert.Equal(StrategyAction.PitNow, decision.Action);
        Assert.True(decision.RefuelKg > 0);
        Assert.True(decision.RefuelKg + 1.6 <= 100 + 1e-9);
    }

    [Fact]
    public void WhereTyresCannotBeChanged_ADecisionNeverNamesACompound()
    {
        for (var lap = 1; lap < 40; lap += 3)
        {
            var traced = new Rig(2005, skill: 20).Decide(PitTestKit.Snapshot(2005, lap, 40, PitTestKit.Car("grooved.medium", age: lap - 1, fuel: 70, burn: 1.5)));
            Assert.Null(traced.Decision.CompoundId);
            Assert.DoesNotContain(traced.Trace.Options, o => o.Id.StartsWith("pit_now/", StringComparison.Ordinal) && !o.Id.StartsWith("pit_now/keep/", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void OnTheLastLap_ThereIsNoStop()
    {
        var traced = new Rig(2012, skill: 0).Decide(PitTestKit.Snapshot(2012, 40, 40, PitTestKit.Car("C4", age: 39, fuel: 5, used: ["C3", "C4"])));

        Assert.NotEqual(StrategyAction.PitNow, traced.Decision.Action);
        Assert.DoesNotContain(traced.Trace.Options, o => o.Id.StartsWith("pit_now/", StringComparison.Ordinal));
    }

    // ---- Driver swap ----

    [Fact]
    public void ATiredDriver_InASharedCar_IsSwapped()
    {
        var calc = PitTestKit.T30Calculators(1955);
        var sink = new MemorySink();
        var strategist = new RuleBasedStrategist(100, calc, RuleBasedStrategist.DeriveRaceStream(1, 1955, 1), null, new StrategistTracing(sink, new WeekendKey(1955, 1), "s"));
        var snapshot = PitTestKit.Snapshot(1955, 61, 100, PitTestKit.Car("treaded.hard", age: 60, fuel: 90, burn: 1.2, canSwap: true, driverStint: 100));

        var decision = strategist.Decide(snapshot);

        Assert.Equal(StrategyAction.PitNow, decision.Action);
        Assert.True(decision.SwapDriver);
        Assert.Contains(sink.Traces[^1].Options, o => o.Id.EndsWith("/swap", StringComparison.Ordinal));
    }

    [Fact]
    public void SwapOptions_AreOnlyOfferedWhereTheRulesAndTheCarAllowThem()
    {
        var modern = new Rig(2012, skill: 100).Decide(PitTestKit.Snapshot(2012, 10, 40, PitTestKit.Car("C4", canSwap: true, driverStint: 50)));
        Assert.DoesNotContain(modern.Trace.Options, o => o.Id.EndsWith("/swap", StringComparison.Ordinal));

        var soloOld = new Rig(1955, skill: 100, seed: 1)
            .Decide(PitTestKit.Snapshot(1955, 10, 100, PitTestKit.Car("treaded.hard", canSwap: false, driverStint: 50)));
        Assert.DoesNotContain(soloOld.Trace.Options, o => o.Id.EndsWith("/swap", StringComparison.Ordinal));
    }

    // ---- Pace mode ----

    [Fact]
    public void WhenFuelIsShort_AndNoStopsAreWorthIt_ItSavesFuel()
    {
        // 2012, no refuelling: 12 kg for 10 laps at 1.5 kg a lap does not fit; fuel saving does (10 percent less burn).
        var snapshot = PitTestKit.Snapshot(2012, 31, 40, PitTestKit.Car("C4", age: 5, fuel: 14, burn: 1.5, used: ["C3", "C4"]));
        var decision = Strategist(2012).Decide(snapshot);

        Assert.Equal(StrategyAction.ChangePace, decision.Action);
        Assert.Equal(PaceMode.Save, decision.Pace);
    }

    // ---- Forecast and gaps ----

    private static WeatherForecast Forecast(double rainProbability, int fromMinute = 0, int steps = 90) =>
        new(
            fromMinute,
            0.5,
            [.. Enumerable.Range(1, steps).Select(h => new ForecastStep(h, rainProbability, rainProbability, 0, 1, 0.1, rainProbability))]);

    [Fact]
    public void ACertainForecastOfRain_PutsWetTyresOn()
    {
        var snapshot = PitTestKit.Snapshot(
            2012,
            6,
            40,
            PitTestKit.Car("C4", age: 5, fuel: 70, burn: 1.5),
            compounds: PitTestKit.Compounds(2012, withWet: true),
            forecast: Forecast(1.0));

        var decision = Strategist(2012).Decide(snapshot);

        Assert.Equal(StrategyAction.PitNow, decision.Action);
        Assert.Equal("wet", decision.CompoundId);
    }

    [Fact]
    public void NoRainInTheForecast_NeverConsidersWetTyres()
    {
        var dry = PitTestKit.Snapshot(2012, 6, 40, PitTestKit.Car("C4", age: 5, fuel: 70), compounds: PitTestKit.Compounds(2012, withWet: true), forecast: Forecast(0.0));
        var none = dry with { Forecast = null };

        Assert.DoesNotContain(new Rig(2012).Decide(dry).Trace.Options, o => o.Id.Contains("/wet/", StringComparison.Ordinal));
        Assert.DoesNotContain(new Rig(2012).Decide(none).Trace.Options, o => o.Id.Contains("/wet/", StringComparison.Ordinal));
    }

    [Fact]
    public void ACarRightBehind_MakesAStopCostAPlace()
    {
        var free = MidRaceSnapshot();
        var pressed = free with { Gaps = new VisibleGaps(5, 20, 3) };

        double PositionRisk(KnowledgeSnapshot s) => new Rig(2012).Decide(s).Trace.Options
            .Where(o => o.Id.StartsWith("pit_now/", StringComparison.Ordinal))
            .Max(o => o.Factors.Single(f => f.Name == "position_risk").Contribution);

        Assert.Equal(0, PositionRisk(free));
        Assert.True(PositionRisk(pressed) < 0);
    }

    [Fact]
    public void BadSnapshots_AreRejected()
    {
        var strategist = Strategist(2012);
        var good = MidRaceSnapshot();

        Assert.Throws<ArgumentException>(() => strategist.Decide(good with { Lap = 0 }));
        Assert.Throws<ArgumentException>(() => strategist.Decide(good with { Lap = 41 }));
        Assert.Throws<ArgumentException>(() => strategist.Decide(good with { Compounds = ImmutableArray<CompoundInfo>.Empty }));
        Assert.Throws<ArgumentNullException>(() => strategist.Decide(null!));
    }
}
