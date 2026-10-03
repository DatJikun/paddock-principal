using System.Collections.Immutable;
using System.Reflection;
using Paddock.Domain.Random;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Tyres;
using Paddock.Simulation.Racing.Weather;

namespace Paddock.Tests.Racing.Pits;

/// <summary>Decisions of the rule-based strategist. Its numbers are ESTIMATE; the assertions are about direction and rules, not exact values.</summary>
public class StrategistTests
{
    private static RuleBasedStrategist Strategist(int season, int skill = 100, ulong seed = PitTestKit.Seed, StrategistOptions? options = null) =>
        new(skill, PitTestKit.T30Calculators(season), RuleBasedStrategist.DeriveRaceStream(seed, season, 1), options);

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
        var decision = Strategist(2012, skill: 70).Decide(MidRaceSnapshot());
        var record = decision.Record;

        Assert.True(record.Options.Length >= 4);
        Assert.Equal(record.Options.Length, record.Options.Select(o => o.Id).Distinct().Count());
        Assert.Contains(record.Options, o => o.Id.StartsWith("stay_out/", StringComparison.Ordinal));
        Assert.Contains(record.Options, o => o.Id.StartsWith("pit_now/", StringComparison.Ordinal));
        Assert.All(record.Options, o => Assert.True(double.IsFinite(o.Utility)));

        var best = record.Options.MaxBy(o => o.Utility)!;
        Assert.Equal(best.Id, record.ChosenOptionId);
        Assert.False(string.IsNullOrWhiteSpace(record.Reason));
        Assert.Equal("car-1", record.CarId);
        Assert.Equal(12, record.Lap);
        Assert.Equal(70, record.Skill);
    }

    [Fact]
    public void OptionFactors_SumToTheUtility_AndNameTheirTerms()
    {
        var record = Strategist(2012, skill: 40).Decide(MidRaceSnapshot()).Record;

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
        var exact = Strategist(2012, skill: 100).Decide(snapshot).Record;
        var rough = Strategist(2012, skill: 0).Decide(snapshot).Record;

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
        var strategist = new RuleBasedStrategist(30, PitTestKit.T30Calculators(2012), stream);
        var snapshot = MidRaceSnapshot();

        var first = strategist.Decide(snapshot);
        var second = strategist.Decide(snapshot);
        var other = new RuleBasedStrategist(30, PitTestKit.T30Calculators(2012), RuleBasedStrategist.DeriveRaceStream(PitTestKit.Seed, 2012, 1)).Decide(snapshot);

        Assert.Equal(first.Summary, second.Summary);
        Assert.Equal(Describe(first), Describe(second));
        Assert.Equal(Describe(first), Describe(other));
        Assert.Equal(before, stream.State);
    }

    [Fact]
    public void DifferentSeeds_GiveDifferentNoise_ForAWeakStrategist()
    {
        var snapshot = MidRaceSnapshot();
        var a = Strategist(2012, skill: 0, seed: 1).Decide(snapshot);
        var b = Strategist(2012, skill: 0, seed: 2).Decide(snapshot);

        Assert.NotEqual(Describe(a), Describe(b));
    }

    [Fact]
    public void TwoCars_HaveIndependentNoise_AndNeitherDependsOnTheOther()
    {
        var strategist = Strategist(2012, skill: 10);
        var carA = MidRaceSnapshot();
        var carB = carA with { Car = carA.Car with { CarId = "car-2" } };

        var alone = Describe(strategist.Decide(carA));
        _ = strategist.Decide(carB);
        Assert.Equal(alone, Describe(strategist.Decide(carA)));
        Assert.NotEqual(Describe(strategist.Decide(carA)), Describe(strategist.Decide(carB)));
    }

    private static string Describe(StrategyDecision decision) =>
        decision.Summary + "|" + string.Join(";", decision.Record.Options.Select(o => o.Id + "=" + o.Utility.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));

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
            var decision = Strategist(2005, skill: 20).Decide(PitTestKit.Snapshot(2005, lap, 40, PitTestKit.Car("grooved.medium", age: lap - 1, fuel: 70, burn: 1.5)));
            Assert.Null(decision.CompoundId);
            Assert.DoesNotContain(decision.Record.Options, o => o.Id.StartsWith("pit_now/", StringComparison.Ordinal) && !o.Id.StartsWith("pit_now/keep/", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void OnTheLastLap_ThereIsNoStop()
    {
        var decision = Strategist(2012, skill: 0).Decide(PitTestKit.Snapshot(2012, 40, 40, PitTestKit.Car("C4", age: 39, fuel: 5, used: ["C3", "C4"])));

        Assert.NotEqual(StrategyAction.PitNow, decision.Action);
        Assert.DoesNotContain(decision.Record.Options, o => o.Id.StartsWith("pit_now/", StringComparison.Ordinal));
    }

    // ---- Driver swap ----

    [Fact]
    public void ATiredDriver_InASharedCar_IsSwapped()
    {
        var calc = PitTestKit.T30Calculators(1955);
        var strategist = new RuleBasedStrategist(100, calc, RuleBasedStrategist.DeriveRaceStream(1, 1955, 1));
        var snapshot = PitTestKit.Snapshot(1955, 61, 100, PitTestKit.Car("treaded.hard", age: 60, fuel: 90, burn: 1.2, canSwap: true, driverStint: 100));

        var decision = strategist.Decide(snapshot);

        Assert.Equal(StrategyAction.PitNow, decision.Action);
        Assert.True(decision.SwapDriver);
        Assert.Contains(decision.Record.Options, o => o.Id.EndsWith("/swap", StringComparison.Ordinal));
    }

    [Fact]
    public void SwapOptions_AreOnlyOfferedWhereTheRulesAndTheCarAllowThem()
    {
        var modern = Strategist(2012, skill: 100).Decide(PitTestKit.Snapshot(2012, 10, 40, PitTestKit.Car("C4", canSwap: true, driverStint: 50)));
        Assert.DoesNotContain(modern.Record.Options, o => o.Id.EndsWith("/swap", StringComparison.Ordinal));

        var soloOld = new RuleBasedStrategist(100, PitTestKit.T30Calculators(1955), RuleBasedStrategist.DeriveRaceStream(1, 1955, 1))
            .Decide(PitTestKit.Snapshot(1955, 10, 100, PitTestKit.Car("treaded.hard", canSwap: false, driverStint: 50)));
        Assert.DoesNotContain(soloOld.Record.Options, o => o.Id.EndsWith("/swap", StringComparison.Ordinal));
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

        Assert.DoesNotContain(Strategist(2012).Decide(dry).Record.Options, o => o.Id.Contains("/wet/", StringComparison.Ordinal));
        Assert.DoesNotContain(Strategist(2012).Decide(none).Record.Options, o => o.Id.Contains("/wet/", StringComparison.Ordinal));
    }

    [Fact]
    public void ACarRightBehind_MakesAStopCostAPlace()
    {
        var free = MidRaceSnapshot();
        var pressed = free with { Gaps = new VisibleGaps(5, 20, 3) };

        double PositionRisk(KnowledgeSnapshot s) => Strategist(2012).Decide(s).Record.Options
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
