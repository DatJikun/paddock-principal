using Paddock.Simulation.Racing.Weather;

namespace Paddock.Tests.Racing.Weather;

public class ClimateSourceTests
{
    private readonly IClimateSource _source = new DefaultClimateSource();

    [Fact]
    public void BuiltInTableCoversEveryClimateClass()
    {
        var classes = DefaultClimateSource.BuiltInAssignments.Values.Select(a => a.ClimateClass).Distinct().Order().ToList();
        Assert.Equal(Enum.GetValues<ClimateClass>().Order().ToList(), classes);
    }

    [Fact]
    public void EveryClassAndMonthGivesAValidProfile()
    {
        foreach (var circuit in DefaultClimateSource.BuiltInAssignments.Keys)
        {
            for (var month = 1; month <= 12; month++)
            {
                var profile = _source.For(circuit, month);
                Assert.True(profile.AirTempMinC < profile.AirTempMaxC);
                Assert.Equal(ClimateProfile.ProbabilityFor(profile.RainBand), profile.RaceRainProbability);
                Assert.InRange(profile.Variability, 0, 1);
            }
        }
    }

    [Fact]
    public void ClassesDifferInRainAndTemperature()
    {
        Assert.Equal(RainBand.High, _source.For("silverstone", 7).RainBand);
        Assert.Equal(RainBand.Medium, _source.For("hungaroring", 7).RainBand);
        Assert.Equal(RainBand.Low, _source.For("bahrain", 4).RainBand);
        Assert.Equal(ClimateClass.Desert, _source.For("bahrain", 4).ClimateClass);
        Assert.True(_source.For("bahrain", 7).AirTempMinC > _source.For("silverstone", 7).AirTempMaxC);
    }

    [Fact]
    public void SeasonsPeakInSummerAndSouthernCircuitsAreShifted()
    {
        Assert.True(_source.For("silverstone", 7).AirTempMinC > _source.For("silverstone", 1).AirTempMinC);
        Assert.True(_source.For("albert_park", 1).AirTempMinC > _source.For("albert_park", 7).AirTempMinC);
    }

    [Fact]
    public void UnknownCircuitFallsBackToTheDefaultClass()
    {
        Assert.Equal(DefaultClimateSource.FallbackClass, _source.For("not-a-circuit", 6).ClimateClass);
    }

    [Fact]
    public void RejectsBadMonthsAndNames()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _source.For("monaco", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => _source.For("monaco", 13));
        Assert.Throws<ArgumentException>(() => _source.For(" ", 5));
    }

    [Fact]
    public void ProfileValidatesItsNumbers()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClimateProfile(ClimateClass.Desert, RainBand.Low, 1.5, 10, 20, 0.5));
        Assert.Throws<ArgumentException>(() => new ClimateProfile(ClimateClass.Desert, RainBand.Low, 0.1, 20, 10, 0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClimateProfile(ClimateClass.Desert, RainBand.Low, 0.1, 10, 20, -0.1));
    }

    [Fact]
    public void CustomSourceCanReplaceTheBuiltInTable()
    {
        IClimateSource custom = new DefaultClimateSource(new Dictionary<string, CircuitClimateAssignment>
        {
            ["x"] = new(ClimateClass.Tropical),
        });
        Assert.Equal(ClimateClass.Tropical, custom.For("x", 3).ClimateClass);
        Assert.Equal(DefaultClimateSource.FallbackClass, custom.For("silverstone", 3).ClimateClass);
    }

    [Fact]
    public void ClimateFeedsTheGenerator()
    {
        var profile = _source.For("silverstone", 7);
        var truth = RaceWeather.Generate(5, 2001, 10, profile, 90);
        Assert.Equal(91, truth.Samples.Count);
    }
}
