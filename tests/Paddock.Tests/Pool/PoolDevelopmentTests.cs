using Paddock.Domain.Pool;
using Paddock.Domain.Random;

namespace Paddock.Tests.Pool;

/// <summary>Development in the pool (DESIGN 2.1). The truths are synthetic; rates under test are the ESTIMATES of the game.</summary>
public class PoolDevelopmentTests
{
    [Fact]
    public void AttributesNeverDropAndNeverPassTheirCeiling()
    {
        var truth = PoolKit.Truth(6, 9);
        for (var season = 0; season < 20; season++)
        {
            var next = PoolDevelopment.Develop(truth, 100, Rng(season));
            for (var i = 0; i < truth.Attributes.Count; i++)
            {
                Assert.True(next.Attributes[i].Value >= truth.Attributes[i].Value);
                Assert.True(next.Attributes[i].Value <= truth.Potential[i].Value);
                Assert.True(next.Attributes[i].Value - truth.Attributes[i].Value <= PoolEstimates.MaxAnnualStep);
            }

            Assert.Equal(truth.Potential, next.Potential);
            truth = next;
        }

        Assert.All(truth.Attributes.Zip(truth.Potential), pair => Assert.True(pair.First.Value <= pair.Second.Value));
    }

    [Fact]
    public void AMemberAtHisCeilingStaysThereAndStillDrawsTheSameNumbers()
    {
        var capped = PoolKit.Truth(14, 0);
        var one = Rng(1);
        var two = Rng(1);
        Assert.Equal(capped.Attributes, PoolDevelopment.Develop(capped, 200, one).Attributes);
        _ = PoolDevelopment.Develop(PoolKit.Truth(3, 15), 100, two);
        Assert.Equal(one.State, two.State);
    }

    [Fact]
    public void ADevelopingMemberClosesMostOfTheGapInTheEnd()
    {
        var truth = PoolKit.Truth(5, 10);
        for (var season = 0; season < 12; season++)
        {
            truth = PoolDevelopment.Develop(truth, 100, Rng(season));
        }

        Assert.True(truth.Attributes.Average(attribute => attribute.Value) >= 13);
    }

    [Fact]
    public void FundedSeasonsDevelopFasterOnAverageAndTheFastProgrammeFastest()
    {
        double Mean(int speed)
        {
            double total = 0;
            for (var person = 0; person < 400; person++)
            {
                var next = PoolDevelopment.Develop(PoolKit.Truth(5, 6), speed, Rng(person));
                total += next.Attributes.Sum(attribute => attribute.Value);
            }

            return total;
        }

        var unfunded = Mean(100);
        var cheap = Mean(PoolEstimates.SpeedPercent(JuniorProgramme.CheapSlow));
        var fast = Mean(PoolEstimates.SpeedPercent(JuniorProgramme.ExpensiveFast));
        Assert.True(unfunded < cheap);
        Assert.True(cheap < fast);
    }

    [Fact]
    public void LuckVariesBetweenSeasonsAndTheSameDrawsGiveTheSameResult()
    {
        var truth = PoolKit.Truth(5, 12);
        var results = Enumerable.Range(0, 40).Select(i => PoolDevelopment.Develop(truth, 100, Rng(i)).Attributes.Sum(a => a.Value)).Distinct().Count();
        Assert.True(results > 3);
        Assert.Equal(
            PoolDevelopment.Develop(truth, 100, Rng(9)).Attributes,
            PoolDevelopment.Develop(truth, 100, Rng(9)).Attributes);
    }

    [Fact]
    public void ProgrammesHaveAPriceAndAFasterOneCostsMore()
    {
        Assert.True(PoolEstimates.CostOf(JuniorProgramme.ExpensiveFast) > PoolEstimates.CostOf(JuniorProgramme.CheapSlow));
        Assert.True(PoolEstimates.SpeedPercent(JuniorProgramme.ExpensiveFast) > PoolEstimates.SpeedPercent(JuniorProgramme.CheapSlow));
        Assert.True(PoolEstimates.SpeedPercent(JuniorProgramme.CheapSlow) > 100);
        Assert.Throws<ArgumentOutOfRangeException>(() => PoolEstimates.CostOf((JuniorProgramme)9));
    }

    private static Xoshiro256StarStar Rng(int salt) =>
        new RngStream(RngStreamName.People, RngStreams.Derive(5, RngStreamName.People, 1950).State)
            .DeriveChild("dev-test:" + salt);
}
