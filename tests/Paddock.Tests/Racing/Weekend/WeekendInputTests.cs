using Paddock.Domain.People;
using Paddock.Domain.World;
using Paddock.Simulation.Racing.Reliability;
using Paddock.Simulation.Racing.Weekend;
using Paddock.SimRunner;

namespace Paddock.Tests.Racing.Weekend;

public class RaceInputMappingTests
{
    [Theory]
    [InlineData(1, 5)]
    [InlineData(10, 50)]
    [InlineData(20, 100)]
    public void AnAttributeBecomesAPaceInput_ByTheLinearScale(int attribute, double expected)
    {
        Assert.Equal(expected, RaceInputMapping.Scale(attribute));
        Assert.Equal(5d, WeekendConstants.AttributeToRatingScale);
    }

    [Fact]
    public void ADriverIsMappedFromTheVisibleAttributes()
    {
        var attributes = new DriverAttributes(
            cornering: 18,
            braking: 12,
            smoothness: 14,
            overtaking: 16,
            defending: 8,
            consistency: 15,
            composure: 11,
            adaptability: 10,
            wetWeather: 9,
            fitness: 10,
            feedback: 10);

        var driver = RaceInputMapping.DriverFrom("driver-x", attributes, aggression1To20: 13, affinitySeconds: 0.1);

        Assert.Equal("driver-x", driver.DriverId);
        Assert.Equal(90, driver.Pace.Pace);
        Assert.Equal(75, driver.Pace.Consistency);
        Assert.Equal(55, driver.Pace.Composure);
        Assert.Equal(0.1, driver.Pace.AffinitySeconds);
        Assert.Equal(65, driver.Aggression);
        Assert.Equal(80, driver.Overtaking);
        Assert.Equal(40, driver.Defending);
        Assert.Equal(70, driver.Smoothness);
    }

    [Fact]
    public void UniformComponents_HaveEveryPartAtTheRating()
    {
        var parts = RaceInputMapping.UniformComponents(60, mileageLaps: 30);

        Assert.Equal(Enum.GetValues<MechanicalComponent>().Length, parts.Length);
        Assert.All(parts, p =>
        {
            Assert.Equal(0.6, p.ReliabilityRating);
            Assert.Equal(30, p.MileageLaps);
        });
    }
}

public class RaceDistanceTests
{
    private static TrackLayout Track(string circuit, double km) =>
        new("layout", circuit, km, new Dictionary<string, double>(), []);

    [Fact]
    public void TheRealRules_GiveTheDistanceOfTheEra()
    {
        var data = WeekendTestKit.Data;

        Assert.Equal(WeekendConstants.UnknownDistanceKm, RaceDistance.DistanceKm(data.RuleSetFor(1955), "galvez"));
        Assert.Equal(300, RaceDistance.DistanceKm(data.RuleSetFor(1988), "jacarepagua"));
        Assert.Equal(305, RaceDistance.DistanceKm(data.RuleSetFor(2012), "albert_park"));
    }

    [Fact]
    public void ARaceIsTheLeastWholeNumberOfLapsThatCoversTheDistance()
    {
        var rules = WeekendTestKit.Data.RuleSetFor(2012);

        Assert.Equal(58, RaceDistance.LapsFor(rules, Track("albert_park", 5.303)));
        Assert.Equal(1, RaceDistance.LapsFor(rules, Track("huge", 1000)));
    }

    [Fact]
    public void EveryRuleValueTheDataUses_IsUnderstood_AndAnUnknownOneThrows()
    {
        var data = WeekendTestKit.Data;
        for (var season = 1950; season <= 2026; season++)
        {
            var laps = RaceDistance.LapsFor(data.RuleSetFor(season), Track("monaco", 3.3));
            Assert.InRange(laps, 50, 150);
        }

        var odd = RuleSet.For(2012, [RaceDistance.Dimension], [new RulePeriod(RaceDistance.Dimension, "bogus", 1950, null)]);
        Assert.Throws<InvalidOperationException>(() => RaceDistance.DistanceKm(odd, "monaco"));
    }
}

public class SyntheticFieldTests
{
    [Theory]
    [InlineData(1955, 18, 4)]
    [InlineData(1988, 26, 0)]
    [InlineData(2012, 24, 0)]
    public void TheFixtureField_HasTheSizeOfItsEra(int season, int cars, int shared)
    {
        var field = SyntheticField.For(season);

        Assert.Equal(cars, field.Length);
        Assert.Equal(shared, field.Count(e => e.Drivers.Length == 2));
        Assert.Equal(field.Length, field.Select(e => e.CarId).Distinct().Count());
        Assert.Equal(field.SelectMany(e => e.Drivers).Count(), field.SelectMany(e => e.Drivers).Select(d => d.DriverId).Distinct().Count());
        Assert.Equal(field.Length / 2, field.Select(e => e.ConstructorId).Distinct().Count());
    }

    [Fact]
    public void TheFixtureField_IsSynthetic_AndDoesNotDependOnTheSeed()
    {
        var field = SyntheticField.For(1955);

        Assert.All(field, e =>
        {
            Assert.StartsWith("car-", e.CarId, StringComparison.Ordinal);
            Assert.StartsWith("team-", e.ConstructorId, StringComparison.Ordinal);
            Assert.All(e.Drivers, d => Assert.StartsWith("driver-", d.DriverId, StringComparison.Ordinal));
        });
        var again = SyntheticField.For(1955);
        Assert.Equal(
            field.Select(e => (e.CarId, e.Car, e.Primary, e.StrategistSkill, e.ForecastQuality)),
            again.Select(e => (e.CarId, e.Car, e.Primary, e.StrategistSkill, e.ForecastQuality)));
    }

    [Fact]
    public void TheBestCarIsFirst_AndTheFieldIsGraded()
    {
        var field = SyntheticField.For(1988);

        Assert.True(field[0].Car.Power > field[^1].Car.Power);
        Assert.True(field[0].Primary.Pace.Pace > field[^1].Primary.Pace.Pace);
        Assert.True(field[0].StrategistSkill > field[^1].StrategistSkill);
    }
}
