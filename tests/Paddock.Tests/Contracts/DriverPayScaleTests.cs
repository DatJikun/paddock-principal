using Paddock.Domain.Contracts;
using Paddock.Tests.Career;

namespace Paddock.Tests.Contracts;

/// <summary>
/// The authored pay scale of a driver by era and rank (#325). Both numbers of an era are ESTIMATES (low confidence in the data file);
/// these tests only keep them plausible for the year: a top driver of 2010 earns tens of millions, not a single-digit amount.
/// </summary>
public class DriverPayScaleTests
{
    private static readonly EraPayBenchmark Pay = new(CareerKit.Data.EraSetFor);

    private static long Driver(int season, double stars) => Pay.Reference(season, NegotiationSubject.DriverSeat, stars);

    [Fact]
    public void ATopDriverOf2010EarnsTensOfMillionsAndAMidfieldDriverMillions()
    {
        Assert.InRange(Driver(2010, 5.0), 10_000_000, 100_000_000);
        Assert.InRange(Driver(2010, NegotiationEstimates.MidfieldStars), 1_000_000, 10_000_000);
        Assert.InRange(Driver(2010, 0.0), 100_000, 2_000_000);
    }

    [Fact]
    public void ATopDriverOf1996AlsoEarnsTensOfMillions()
    {
        Assert.InRange(Driver(1996, 5.0), 10_000_000, 100_000_000);
        Assert.InRange(Driver(1996, NegotiationEstimates.MidfieldStars), 500_000, 5_000_000);
    }

    [Fact]
    public void PayRisesWithTheStarsInEverySeasonOfTheCareerAndTheTopNeverFallsBelowTheMiddle()
    {
        for (var season = 1950; season <= 2026; season++)
        {
            long previous = 0;
            foreach (var stars in new[] { 0.0, 1.0, 2.5, 3.5, 5.0 })
            {
                var pay = Driver(season, stars);
                Assert.True(pay >= previous, $"{season}: {stars} stars pays {pay}, less than {previous}");
                previous = pay;
            }

            Assert.True(Driver(season, 5.0) >= Driver(season, NegotiationEstimates.MidfieldStars), season.ToString());
        }
    }
}
