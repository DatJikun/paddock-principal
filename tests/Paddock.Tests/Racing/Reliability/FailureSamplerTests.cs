using Paddock.Domain.Random;
using Paddock.Simulation.Racing.Reliability;
using static Paddock.Tests.Racing.Reliability.ReliabilityTestKit;

namespace Paddock.Tests.Racing.Reliability;

public class FailureSamplerTests
{
    private const int Laps = 70;

    [Fact]
    public void SameSeed_GivesTheSameResult_AndTheStreamIsNotAdvanced()
    {
        var inputs = ReferenceInputs(1955);
        var stream = FailureSampler.DeriveRaceStream(Seed, 1955, 3);
        var stateBefore = stream.State;

        var first = FailureSampler.Sample(stream, "car-7", Laps, AllComponents(0.3), inputs);
        var second = FailureSampler.Sample(stream, "car-7", Laps, AllComponents(0.3), inputs);

        Assert.Equal(first, second);
        Assert.Equal(stateBefore, stream.State);
        Assert.Equal(
            SampleField(50, Laps, AllComponents(0.3), inputs, 3).ToList(),
            SampleField(50, Laps, AllComponents(0.3), inputs, 3).ToList());
    }

    [Fact]
    public void DifferentSeedsRoundsAndCars_GiveDifferentFields()
    {
        var inputs = ReferenceInputs(1955);
        var components = AllComponents(0.3);

        var baseField = SampleField(200, Laps, components, inputs, 1).ToList();
        var otherRound = SampleField(200, Laps, components, inputs, 2).ToList();
        var otherSeed = Enumerable.Range(0, 200)
            .Select(i => FailureSampler.Sample(FailureSampler.DeriveRaceStream(Seed + 1, 1955, 1), "car-" + i, Laps, components, inputs))
            .ToList();

        Assert.NotEqual(baseField, otherRound);
        Assert.NotEqual(baseField, otherSeed);
        Assert.True(baseField.Distinct().Count() > 20);
    }

    [Fact]
    public void RaceStream_IsTheFailuresStreamOfTheRaceAndSeason()
    {
        var expected = RngStream.Derive(Seed, RngStreamName.Failures, 1962, 4);

        Assert.Equal(expected.State, FailureSampler.DeriveRaceStream(Seed, 1962, 4).State);
        Assert.Equal(RngStreamName.Failures, FailureSampler.DeriveRaceStream(Seed, 1962, 4).Name);
        Assert.NotEqual(expected.State, FailureSampler.DeriveRaceStream(Seed, 1962, 5).State);
    }

    [Fact]
    public void CarA_DoesNotDependOnWhichOtherCarsAreInTheRace()
    {
        var inputs = ReferenceInputs(1955);
        var stream = FailureSampler.DeriveRaceStream(Seed, 1955, 1);
        var components = AllComponents(0.4);

        var alone = FailureSampler.Sample(stream, "A", Laps, components, inputs);

        // A race with other cars sampled before and after A, in a different order, with different parts.
        FailureSampler.Sample(stream, "B", Laps, AllComponents(0.1), inputs);
        FailureSampler.Sample(stream, "C", Laps, AllComponents(0.9, 300), inputs);
        var amongOthers = FailureSampler.Sample(stream, "A", Laps, components, inputs);
        FailureSampler.Sample(stream, "D", Laps, components, inputs);

        Assert.Equal(alone, amongOthers);
        Assert.NotEqual(
            FailureSampler.Sample(stream, "B", 500, AllComponents(0.0), inputs),
            FailureSampler.Sample(stream, "C", 500, AllComponents(0.0), inputs));
    }

    [Fact]
    public void ACarsDraws_DoNotDependOnWhichPartsItsNeighboursHave()
    {
        var inputs = ReferenceInputs(1955);
        var stream = FailureSampler.DeriveRaceStream(Seed, 1955, 1);
        var engineOnly = new[] { new ComponentState(MechanicalComponent.Engine, 0.4) };
        var full = AllComponents(0.4);
        var compared = 0;

        for (var i = 0; i < 400; i++)
        {
            var fullSample = FailureSampler.Sample(stream, "car-" + i, 500, full, inputs);
            var engineSample = FailureSampler.Sample(stream, "car-" + i, 500, engineOnly, inputs);
            if (fullSample.Retirement is { Component: MechanicalComponent.Engine } retirement)
            {
                // The engine's own draw is the same with or without the other parts.
                Assert.Equal(retirement, engineSample.Retirement);
                compared++;
            }
        }

        Assert.True(compared > 20);
    }

    [Fact]
    public void ComponentOrderInTheInputList_DoesNotMatter()
    {
        var inputs = ReferenceInputs(1955);
        var stream = FailureSampler.DeriveRaceStream(Seed, 1955, 1);
        var forward = AllComponents(0.4, 100);
        var reversed = forward.Reverse().ToArray();

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(
                FailureSampler.Sample(stream, "car-" + i, Laps, forward, inputs),
                FailureSampler.Sample(stream, "car-" + i, Laps, reversed, inputs));
        }
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void HigherReliabilityRating_MeansFewerFailures()
    {
        const int cars = 10_000;
        var inputs = ReferenceInputs(1965);

        var poor = SampleField(cars, Laps, AllComponents(0.1), inputs).Count(s => s.First is not null);
        var good = SampleField(cars, Laps, AllComponents(0.9), inputs).Count(s => s.First is not null);
        var poorRetired = SampleField(cars, Laps, AllComponents(0.1), inputs).Count(s => s.Retirement is not null);
        var goodRetired = SampleField(cars, Laps, AllComponents(0.9), inputs).Count(s => s.Retirement is not null);

        // ESTIMATE-level expectation is about a factor of three; the bound only asks for a clear gap.
        Assert.True(good < poor * 0.7, $"good={good} poor={poor}");
        Assert.True(goodRetired < poorRetired * 0.7, $"good={goodRetired} poor={poorRetired}");
    }

    [Fact]
    public void MoreMileage_MeansMoreFailures()
    {
        const int cars = 10_000;
        var inputs = ReferenceInputs(1975);

        var fresh = SampleField(cars, Laps, AllComponents(0.5, 0), inputs).Count(s => s.First is not null);
        var worn = SampleField(cars, Laps, AllComponents(0.5, 1500), inputs).Count(s => s.First is not null);

        Assert.True(worn > fresh * 1.2, $"worn={worn} fresh={fresh}");
    }

    [Fact]
    public void HigherStress_MeansMoreFailures()
    {
        const int cars = 10_000;
        var calm = new FailureInputs(1975, DriverSympathy: 1, PaceStress: 0, Heat: 0);
        var hard = new FailureInputs(1975, DriverSympathy: 0, PaceStress: 1, Heat: 1);

        var calmFailures = SampleField(cars, Laps, AllComponents(0.5), calm).Count(s => s.First is not null);
        var hardFailures = SampleField(cars, Laps, AllComponents(0.5), hard).Count(s => s.First is not null);

        Assert.True(hardFailures > calmFailures * 1.3, $"hard={hardFailures} calm={calmFailures}");
    }

    [Fact]
    public void LongerRaces_MeanMoreFailures()
    {
        var inputs = ReferenceInputs(1975);

        var short_ = SampleField(5_000, 20, ReferenceComponents(), inputs).Count(s => s.First is not null);
        var long_ = SampleField(5_000, 120, ReferenceComponents(), inputs).Count(s => s.First is not null);

        Assert.True(long_ > short_);
    }

    [Fact]
    public void Failures_AreInsideTheRace_AndRetirementIsNeverBeforeTheFirstFailure()
    {
        var inputs = ReferenceInputs(1955);
        var failures = 0;

        foreach (var sample in SampleField(3_000, Laps, AllComponents(0.2), inputs))
        {
            if (sample.First is null)
            {
                Assert.Null(sample.Retirement);
                continue;
            }

            failures++;
            Assert.InRange(sample.First.Lap, 1, Laps);
            if (sample.Retirement is not null)
            {
                Assert.True(sample.Retirement.IsRetirement);
                Assert.InRange(sample.Retirement.Lap, sample.First.Lap, Laps);
            }

            if (sample.First.IsRetirement)
            {
                Assert.Same(sample.First, sample.Retirement);
            }
        }

        Assert.True(failures > 500);
    }

    [Fact]
    public void Warning_ComesTheConfiguredNumberOfLapsBeforeTheFailure()
    {
        var inputs = ReferenceInputs(1955);
        var options = new FailureSamplerOptions(WarningLeadLaps: 5);
        var warned = 0;
        var sudden = 0;

        foreach (var sample in SampleField(5_000, Laps, AllComponents(0.2), inputs, options: options))
        {
            foreach (var failure in new[] { sample.First, sample.Retirement }.OfType<MechanicalFailure>())
            {
                if (failure.Sudden)
                {
                    Assert.Null(failure.WarningLap);
                    Assert.Equal(0, failure.DegradedPaceLossFraction);
                    sudden++;
                }
                else if (failure.Lap > 5)
                {
                    Assert.Equal(failure.Lap - 5, failure.WarningLap);
                    Assert.Equal(ReliabilityConstants.DegradedPaceLossFraction, failure.DegradedPaceLossFraction);
                    warned++;
                }
                else
                {
                    Assert.Null(failure.WarningLap);
                    Assert.Equal(0, failure.DegradedPaceLossFraction);
                    sudden++;
                }
            }
        }

        Assert.True(warned > 100);
    }

    [Fact]
    public void AboutAThirdOfFailuresWithRoomForAWarningAreSudden_AndHaveNoDegradedPace()
    {
        var inputs = ReferenceInputs(1955);
        var sudden = 0;
        var eligible = 0;

        foreach (var sample in SampleField(4_000, 80, AllComponents(0.0), inputs))
        {
            var failures = sample.Retirement is not null && !ReferenceEquals(sample.Retirement, sample.First)
                ? new[] { sample.First, sample.Retirement }
                : new[] { sample.First };
            foreach (var failure in failures.OfType<MechanicalFailure>())
            {
                if (failure.Lap <= ReliabilityConstants.DefaultWarningLeadLaps)
                {
                    continue;
                }

                eligible++;
                if (!failure.Sudden)
                {
                    Assert.Equal(failure.Lap - ReliabilityConstants.DefaultWarningLeadLaps, failure.WarningLap);
                    continue;
                }

                sudden++;
                Assert.Null(failure.WarningLap);
                Assert.Equal(0, failure.DegradedPaceLossFraction);
                Assert.Equal(0, failure.DegradedPaceLossOnLap(failure.Lap - 1));
            }
        }

        Assert.True(eligible > 500);
        var share = (double)sudden / eligible;
        Assert.InRange(share, ReliabilityConstants.SuddenFailureShare - 0.05, ReliabilityConstants.SuddenFailureShare + 0.05);
    }

    [Fact]
    public void TheSuddenDrawDoesNotAdvanceTheCarsSequentialDraws()
    {
        var race = FailureSampler.DeriveRaceStream(Seed, 1955, 1);
        var before = NextFour(race.DeriveChild("car:probe"));
        race.DeriveChild("sudden:probe:lap:12").NextDouble();
        var after = NextFour(race.DeriveChild("car:probe"));
        Assert.Equal(before, after);
    }

    private static double[] NextFour(Xoshiro256StarStar rng) =>
        [rng.NextDouble(), rng.NextDouble(), rng.NextDouble(), rng.NextDouble()];

    [Fact]
    public void Warning_UsesTheDefaultLeadWhenNoOptionsAreGiven_AndIsActiveUpToTheFailureLap()
    {
        var failure = SampleField(5_000, Laps, AllComponents(0.0), ReferenceInputs(1955))
            .Select(s => s.First)
            .First(f => f is { Lap: > 10, Sudden: false })!;

        Assert.Equal(failure.Lap - ReliabilityConstants.DefaultWarningLeadLaps, failure.WarningLap);
        Assert.False(failure.WarningActiveOnLap(failure.WarningLap!.Value - 1));
        Assert.True(failure.WarningActiveOnLap(failure.WarningLap.Value));
        Assert.True(failure.WarningActiveOnLap(failure.Lap - 1));
        Assert.False(failure.WarningActiveOnLap(failure.Lap));
        Assert.Equal(ReliabilityConstants.DegradedPaceLossFraction, failure.DegradedPaceLossOnLap(failure.Lap - 1));
        Assert.Equal(0, failure.DegradedPaceLossOnLap(failure.Lap));
    }

    [Fact]
    public void Effects_CoverAllCategories_WithMagnitudesInRange()
    {
        var retire = 0;
        var power = 0;
        var repair = 0;

        foreach (var sample in SampleField(5_000, 200, AllComponents(0.0), ReferenceInputs(1955)).Where(s => s.First is not null))
        {
            switch (sample.First!.Effect)
            {
                case FailureEffect.Retire:
                    retire++;
                    break;
                case FailureEffect.LosePower lose:
                    power++;
                    Assert.InRange(lose.Percent, ReliabilityConstants.PowerLossMinPercent, ReliabilityConstants.PowerLossMaxPercent);
                    break;
                case FailureEffect.PitForRepair fix:
                    repair++;
                    Assert.InRange(fix.TimeCostSeconds, ReliabilityConstants.RepairMinSeconds, ReliabilityConstants.RepairMaxSeconds);
                    break;
                default:
                    Assert.Fail("Unknown effect.");
                    break;
            }
        }

        Assert.True(retire > power && power > 0 && repair > 0, $"retire={retire} power={power} repair={repair}");
    }

    [Fact]
    public void AnEngineNeverNeedsAPitRepair()
    {
        var engineOnly = new[] { new ComponentState(MechanicalComponent.Engine, 0.0) };

        var failures = SampleField(3_000, 300, engineOnly, ReferenceInputs(1955)).Where(s => s.First is not null).Select(s => s.First!).ToList();

        Assert.All(failures, f => Assert.Equal(MechanicalComponent.Engine, f.Component));
        Assert.DoesNotContain(failures, f => f.Effect is FailureEffect.PitForRepair);
        Assert.Contains(failures, f => f.Effect is FailureEffect.LosePower);
    }

    [Fact]
    public void ACarWithNoParts_NeverFails()
    {
        var samples = SampleField(100, 500, [], ReferenceInputs(1955)).ToList();

        Assert.All(samples, s => Assert.Equal(FailureSample.None, s));
    }

    [Fact]
    public void ModernEra_FailsFarLessThanThe1950s()
    {
        const int cars = 5_000;

        var fifties = SampleField(cars, 70, ReferenceComponents(), ReferenceInputs(1955)).Count(s => s.Retirement is not null);
        var twentyTens = SampleField(cars, 58, ReferenceComponents(), ReferenceInputs(2015)).Count(s => s.Retirement is not null);

        Assert.True(fifties > twentyTens * 3);
    }

    [Fact]
    public void BadArguments_AreRejected()
    {
        var stream = FailureSampler.DeriveRaceStream(Seed, 1955, 1);
        var inputs = ReferenceInputs(1955);
        var parts = AllComponents(0.5);

        Assert.Throws<ArgumentOutOfRangeException>(() => FailureSampler.Sample(stream, "a", 0, parts, inputs));
        Assert.Throws<ArgumentException>(() => FailureSampler.Sample(stream, " ", 10, parts, inputs));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FailureSampler.Sample(stream, "a", 10, parts, inputs, new FailureSamplerOptions(0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FailureSampler.Sample(stream, "a", 10, parts, inputs with { Heat = 1.5 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FailureSampler.Sample(stream, "a", 10, [new ComponentState(MechanicalComponent.Engine, 1.2)], inputs));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FailureSampler.Sample(stream, "a", 10, [new ComponentState(MechanicalComponent.Engine, 0.5, -1)], inputs));
        Assert.Throws<ArgumentException>(() =>
            FailureSampler.Sample(
                stream,
                "a",
                10,
                [new ComponentState(MechanicalComponent.Engine, 0.5), new ComponentState(MechanicalComponent.Engine, 0.5)],
                inputs));
    }
}
