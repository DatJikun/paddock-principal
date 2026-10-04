using Paddock.Domain.People;
using Paddock.Domain.Pool;
using Paddock.Domain.Random;
using Paddock.Domain.World;

namespace Paddock.Tests.Pool;

/// <summary>
/// Scouting bands (PP-013, DESIGN 10). The persons and ratings are synthetic. The statistical tests use fixed seeds, so they
/// are exact and repeatable; the sample sizes are test choices, not game numbers.
/// </summary>
public class ScoutingModelTests
{
    private static readonly PersonId Subject = PersonId.Real("subject");

    [Fact]
    public void BandsNarrowMonotonicallyWithObservationAndNestInsideEarlierBands()
    {
        foreach (var judgement in new[] { 1, 8, 14, 20 })
        {
            for (var person = 0; person < 25; person++)
            {
                var truth = PoolKit.Truth(1 + (person % 18), 1 + (person % 4));
                var scout = new ScoutProfile(judgement, 10);
                PersonKnowledge? before = null;
                long milli = 0;
                for (var month = 1; month <= 60; month++)
                {
                    milli += 1000;
                    var next = ScoutingModel.Observe(PoolKit.Alpha, Subject, truth, scout, milli, Rng(person));
                    if (before is not null)
                    {
                        foreach (var attribute in next.Attributes)
                        {
                            var earlier = before.Attributes.Single(known => known.Key == attribute.Key).Band;
                            Assert.True(
                                attribute.Band.Low >= earlier.Low && attribute.Band.High <= earlier.High,
                                $"{attribute.Key} widened for judgement {judgement}, person {person}, month {month}");
                        }

                        Assert.True(next.Potential!.Value.Low >= before.Potential!.Value.Low);
                        Assert.True(next.Potential!.Value.High <= before.Potential!.Value.High);
                    }

                    before = next;
                }
            }
        }
    }

    [Fact]
    public void MoreObservationGivesAnAtLeastAsNarrowBandInTotal()
    {
        var truth = PoolKit.Truth(12, 4);
        var scout = new ScoutProfile(15, 12);
        var previousSpan = long.MaxValue;
        foreach (var milli in new long[] { 1000, 3000, 6000, 12_000, 30_000, 120_000 })
        {
            var belief = ScoutingModel.Observe(PoolKit.Alpha, Subject, truth, scout, milli, Rng(1));
            var span = belief.Attributes.Sum(known => (long)(known.Band.High - known.Band.Low));
            Assert.True(span <= previousSpan, $"{milli} points gave a wider band than fewer points");
            previousSpan = span;
        }
    }

    [Fact]
    public void APerfectScoutNeverExcludesTheTruth()
    {
        var misses = 0;
        var checks = 0;
        for (var person = 0; person < 300; person++)
        {
            var truth = PoolKit.Truth(1 + (person % 20), 1 + (person % 5));
            var scout = new ScoutProfile(20, 1 + (person % 20));
            long milli = 0;
            for (var month = 1; month <= 48; month++)
            {
                milli += 700 + ((person * 37) % 900);
                var belief = ScoutingModel.Observe(PoolKit.Alpha, Subject, truth, scout, milli, Rng(person));
                foreach (var attribute in truth.Attributes)
                {
                    var band = belief.Attributes.Single(known => known.Key == attribute.Key).Band;
                    checks++;
                    misses += attribute.Value < band.Low || attribute.Value > band.High ? 1 : 0;
                }

                var potential = ScoutingModel.MeanPotential(truth);
                checks++;
                misses += potential < belief.Potential!.Value.Low || potential > belief.Potential!.Value.High ? 1 : 0;
            }
        }

        Assert.True(checks > 100_000);
        Assert.Equal(0, misses);
    }

    [Fact]
    public void AScoutOfJudgementElevenOrBetterNeverMissesHoweverMuchHeObserves()
    {
        for (var judgement = 11; judgement <= 20; judgement++)
        {
            for (var person = 0; person < 80; person++)
            {
                var truth = PoolKit.Truth(1 + (person % 20), 2);
                foreach (var milli in new long[] { 1000, 20_000, 400_000, 100_000_000 })
                {
                    var belief = ScoutingModel.Observe(PoolKit.Alpha, Subject, truth, new ScoutProfile(judgement, 20), milli, Rng(person));
                    Assert.All(truth.Attributes, attribute =>
                    {
                        var band = belief.Attributes.Single(known => known.Key == attribute.Key).Band;
                        Assert.InRange(attribute.Value, band.Low, band.High);
                    });
                }
            }
        }
    }

    [Fact]
    public void PoorerScoutsMissMoreOftenAndTheWorstScoutDoesMiss()
    {
        int Misses(int judgement)
        {
            var misses = 0;
            for (var person = 0; person < 200; person++)
            {
                var truth = PoolKit.Truth(5 + (person % 10), 3);
                var belief = ScoutingModel.Observe(PoolKit.Alpha, Subject, truth, new ScoutProfile(judgement, 10), 100_000_000, Rng(person));
                misses += truth.Attributes.Count(attribute =>
                {
                    var band = belief.Attributes.Single(known => known.Key == attribute.Key).Band;
                    return attribute.Value < band.Low || attribute.Value > band.High;
                });
            }

            return misses;
        }

        Assert.True(Misses(1) > Misses(6));
        Assert.True(Misses(6) > Misses(10));
        Assert.True(Misses(10) >= Misses(11));
        Assert.Equal(0, Misses(20));
        Assert.True(Misses(1) > 0);
    }

    [Fact]
    public void ObservationAloneNeverMakesAValueExact()
    {
        var truth = PoolKit.Truth(14, 3);
        foreach (var judgement in new[] { 1, 10, 20 })
        {
            var belief = ScoutingModel.Observe(PoolKit.Alpha, Subject, truth, new ScoutProfile(judgement, 20), 100_000_000, Rng(3));
            Assert.All(belief.Attributes, known => Assert.True(known.Band.High > known.Band.Low));
            Assert.True(belief.Potential!.Value.High > belief.Potential!.Value.Low);
        }

        var edge = ScoutingModel.Observe(PoolKit.Alpha, Subject, PoolKit.Truth(1, 0), new ScoutProfile(1, 20), 100_000_000, Rng(4));
        Assert.All(edge.Attributes, known => Assert.True(known.Band.High > known.Band.Low));
        var top = ScoutingModel.Observe(PoolKit.Alpha, Subject, PoolKit.Truth(20, 0), new ScoutProfile(1, 20), 100_000_000, Rng(5));
        Assert.All(top.Attributes, known => Assert.True(known.Band.High > known.Band.Low));
    }

    [Fact]
    public void ABetterJudgeHasNarrowerAndMoreAccurateBandsOnAverage()
    {
        (double Span, double Error) Measure(int judgement)
        {
            double span = 0;
            double error = 0;
            for (var person = 0; person < 300; person++)
            {
                var truth = PoolKit.Truth(6 + (person % 10), 3);
                var belief = ScoutingModel.Observe(PoolKit.Alpha, Subject, truth, new ScoutProfile(judgement, 10), 6000, Rng(person));
                span += belief.Attributes.Sum(known => known.Band.High - known.Band.Low);
                error += belief.Attributes.Sum(known => Math.Abs(((known.Band.Low + known.Band.High) / 2.0) - truth.Value(known.Key)));
            }

            return (span, error);
        }

        var poor = Measure(1);
        var middling = Measure(10);
        var best = Measure(20);
        Assert.True(best.Span < middling.Span && middling.Span < poor.Span);
        Assert.True(best.Error < poor.Error);
    }

    [Fact]
    public void ContactsAndSharedRacesSpeedUpObservation()
    {
        var weak = ScoutingModel.MonthlyMilli(ScoutFocusKind.Person, new ScoutProfile(10, 1), 0);
        var strong = ScoutingModel.MonthlyMilli(ScoutFocusKind.Person, new ScoutProfile(10, 20), 0);
        var withRaces = ScoutingModel.MonthlyMilli(ScoutFocusKind.Person, new ScoutProfile(10, 20), 2);
        Assert.True(strong > weak);
        Assert.Equal(strong + (2 * PoolEstimates.SharedRaceMilli), withRaces);
        Assert.True(ScoutingModel.MonthlyMilli(ScoutFocusKind.Person, new ScoutProfile(10, 10), 0)
            > ScoutingModel.MonthlyMilli(ScoutFocusKind.Pool, new ScoutProfile(10, 10), 0));
    }

    [Fact]
    public void ObservationDrawsTheSameNumberOfValuesWhateverTheResult()
    {
        var truth = PoolKit.Truth(10, 4);
        var one = Rng(5);
        var two = Rng(5);
        _ = ScoutingModel.Observe(PoolKit.Alpha, Subject, truth, new ScoutProfile(20, 20), 100_000, one);
        _ = ScoutingModel.Observe(PoolKit.Alpha, Subject, truth, new ScoutProfile(1, 1), 1000, two);
        Assert.Equal(one.State, two.State);
        Assert.Equal(truth.Attributes.Count + 1, DrawsUsed(Rng(5), one));
    }

    [Fact]
    public void ObservationIsAFunctionOfItsInputs()
    {
        var truth = PoolKit.Truth(9, 6);
        var scout = new ScoutProfile(12, 9);
        var left = ScoutingModel.Observe(PoolKit.Alpha, Subject, truth, scout, 4000, Rng(9));
        var right = ScoutingModel.Observe(PoolKit.Alpha, Subject, truth, scout, 4000, Rng(9));
        Assert.Equal(left.Attributes.Select(known => known.Key + known.Band), right.Attributes.Select(known => known.Key + known.Band));
        Assert.Equal(left.Potential, right.Potential);
        Assert.NotEqual(
            left.Attributes.Select(known => known.Key + known.Band),
            ScoutingModel.Observe(PoolKit.Alpha, Subject, truth, scout, 4000, Rng(10)).Attributes.Select(known => known.Key + known.Band));
    }

    [Fact]
    public void StaleBeliefWidensUpwardByAFixedAmountOnly()
    {
        var truth = PoolKit.Truth(10, 4);
        var belief = ScoutingModel.Observe(PoolKit.Alpha, Subject, truth, new ScoutProfile(20, 10), 3000, Rng(2));
        var stale = ScoutingModel.Stale(belief);
        for (var i = 0; i < belief.Attributes.Count; i++)
        {
            var before = belief.Attributes[i].Band;
            var after = stale.Attributes[i].Band;
            Assert.Equal(before.Low, after.Low);
            Assert.Equal(Math.Min(20, before.High + PoolEstimates.MaxAnnualStep), after.High);
        }

        Assert.Equal(belief.Potential, stale.Potential);
    }

    [Fact]
    public void ScoutProfileComesFromScoutAttributesOnly()
    {
        var attributes = new[] { new NamedAttribute("talent_judgement", 17), new NamedAttribute("contact_network", 4) };
        Assert.Equal(new ScoutProfile(17, 4), ScoutProfile.From(new PersonTruth(attributes, attributes)));
        Assert.Null(ScoutProfile.From(PoolKit.Truth(10, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScoutProfile(0, 5));
    }

    /// <summary>The generator a handler would pass: a child of the Scouting stream tagged with the person.</summary>
    private static Xoshiro256StarStar Rng(int person) =>
        new RngStream(RngStreamName.Scouting, RngStreams.Derive(77, RngStreamName.Scouting, 1950).State)
            .DeriveChild("test-bias:" + person);

    private static int DrawsUsed(Xoshiro256StarStar start, Xoshiro256StarStar end)
    {
        for (var draws = 0; draws < 100; draws++)
        {
            if (start.State == end.State)
            {
                return draws;
            }

            start.NextULong();
        }

        throw new InvalidOperationException("The generators never met.");
    }
}
