using Paddock.Domain.Contracts;
using Paddock.Domain.Random;
using Paddock.Domain.World;

namespace Paddock.Tests.Contracts;

public class DriverRaiseTests
{
    [Fact]
    public void LoyalHappyDriversAskLessOftenThanDisloyalUnhappyOnes()
    {
        var loyal = Asks(loyalty: 20, morale: 1, seasons: 400);
        var unhappy = Asks(loyalty: 1, morale: 0, seasons: 400);
        Assert.True(unhappy > loyal * 5);
    }

    [Fact]
    public void APeakDriverAsksForLessThanARisingStarAtTheSameStars()
    {
        const long current = 1_000_000;
        const long benchmarkNow = 4_000_000;
        var peak = DriverRaise.ExtraSalary(current, benchmarkNow, benchmarkNow);
        var rising = DriverRaise.ExtraSalary(current, benchmarkNow, benchmarkNow + 500_000);
        Assert.Equal((long)(current * NegotiationEstimates.PeakRaiseShare), peak);
        Assert.True(rising > peak);
    }

    [Fact]
    public void OneSeedAsksOnTheSameSeasons()
    {
        Assert.Equal(Asks(8, 0.4, 80), Asks(8, 0.4, 80));
        Assert.NotEqual(Asks(8, 0.4, 80, seed: 1), Asks(8, 0.4, 80, seed: 2));
    }

    [Fact]
    public void RefusalLowersTrustAndMakesLeavingLessAttractive()
    {
        var person = PersonId.Real("fangio");
        var team = OrganizationId.Real("alfa");
        var asked = RaisesSection.Empty.Consider(person, team, 1955, 120_000);
        var refused = asked.Refuse(person, team);
        Assert.Equal(NegotiationEstimates.RaiseTrustStart - NegotiationEstimates.RaiseTrustHit, refused.Trust(person, team));
        Assert.True(refused.LeaveBias(person, team) > asked.LeaveBias(person, team));
        Assert.Equal(0, refused.Accept(person, team).LeaveBias(person, team));
    }

    [Theory]
    [InlineData(20, 10, 1, 1, 0.65)]
    [InlineData(0, 0, 0, 0, NegotiationEstimates.NeutralMorale)]
    public void MoraleFollowsResults(int own, int mate, int position, int expected, double happiness)
    {
        Assert.Equal(happiness, DriverRaise.Morale(own, mate, position, expected));
    }

    private static int Asks(int loyalty, double morale, int seasons, ulong seed = 7)
    {
        var market = RngStream.Derive(seed, RngStreamName.Market, 1955);
        var chance = DriverRaise.AskChance(loyalty, morale);
        var asks = 0;
        for (var season = 0; season < seasons; season++)
        {
            if (market.DeriveChild("fangio|" + season.ToString(System.Globalization.CultureInfo.InvariantCulture)).NextDouble() < chance)
            {
                asks++;
            }
        }

        return asks;
    }
}
