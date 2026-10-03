using Paddock.Simulation.Racing.Reliability;
using static Paddock.Tests.Racing.Reliability.ReliabilityTestKit;

namespace Paddock.Tests.Racing.Reliability;

/// <summary>All targets and constants under test are ESTIMATE (see ReliabilityConstants).</summary>
public class ReliabilityEraProfileTests
{
    [Fact]
    public void ComponentSharesSumToOne()
    {
        var sum = Enum.GetValues<MechanicalComponent>().Sum(ReliabilityConstants.ComponentShare);
        Assert.Equal(1.0, sum, 12);
    }

    [Fact]
    public void EraTargets_FallInTheDocumentedBands()
    {
        for (var season = 1950; season <= 1959; season++)
        {
            Assert.InRange(ReliabilityEraProfile.MechanicalRetirementRate(season), 0.40, 0.50);
        }

        for (var season = 1990; season <= 1999; season++)
        {
            Assert.InRange(ReliabilityEraProfile.MechanicalRetirementRate(season), 0.20, 0.25);
        }

        for (var season = 2010; season <= 2019; season++)
        {
            Assert.InRange(ReliabilityEraProfile.MechanicalRetirementRate(season), 0.05, 0.08);
        }
    }

    [Fact]
    public void RetirementRate_FallsSmoothlyAndNeverRises()
    {
        var previous = ReliabilityEraProfile.MechanicalRetirementRate(1940);
        for (var season = 1941; season <= 2040; season++)
        {
            var rate = ReliabilityEraProfile.MechanicalRetirementRate(season);
            Assert.True(rate <= previous + 1e-12, $"Rate rose at {season}.");
            Assert.True(previous - rate < 0.03, $"Rate jumped by {previous - rate} at {season}.");
            previous = rate;
        }
    }

    [Fact]
    public void FinishRate_IsTheComplementOfTheRetirementRate()
    {
        Assert.Equal(1.0, ReliabilityEraProfile.ExpectedFinishRate(1985) + ReliabilityEraProfile.MechanicalRetirementRate(1985), 12);
    }

    [Theory]
    [InlineData(1955)]
    [InlineData(1985)]
    [InlineData(2015)]
    public void Solver_ReferenceCarHitsTheEraTargetAtTheTypicalDistance(int season)
    {
        var laps = (int)Math.Round(ReliabilityEraProfile.TypicalRaceLaps(season));

        var probability = ReliabilityEraProfile.ReferenceRetirementProbability(season, laps);

        Assert.Equal(ReliabilityEraProfile.MechanicalRetirementRate(season), probability, 9);
    }

    [Theory]
    [InlineData(1955)]
    [InlineData(1985)]
    [InlineData(2015)]
    public void Solver_SampledFieldOfReferenceCarsHitsTheEraTarget(int season)
    {
        const int cars = 20_000;
        var laps = (int)Math.Round(ReliabilityEraProfile.TypicalRaceLaps(season));

        var retired = SampleField(cars, laps, ReferenceComponents(), ReferenceInputs(season))
            .Count(sample => sample.Retirement is not null);

        // Standard error is at most 0.0036 for 20 000 cars; the bound is about four of them.
        Assert.Equal(ReliabilityEraProfile.MechanicalRetirementRate(season), retired / (double)cars, 0.015);
    }

    [Fact]
    public void SolveBaseHazardScale_RoundTripsAndValidates()
    {
        var scale = ReliabilityEraProfile.SolveBaseHazardScale(0.3, 60);
        Assert.True(scale > 0);
        Assert.True(ReliabilityEraProfile.SolveBaseHazardScale(0.6, 60) > scale);
        Assert.True(ReliabilityEraProfile.SolveBaseHazardScale(0.3, 30) > scale);

        Assert.Throws<ArgumentOutOfRangeException>(() => ReliabilityEraProfile.SolveBaseHazardScale(0, 60));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReliabilityEraProfile.SolveBaseHazardScale(1, 60));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReliabilityEraProfile.SolveBaseHazardScale(0.3, 0));
    }

    [Fact]
    public void LapHazard_GrowsWithStressAndMileage_AndShrinksWithRating()
    {
        const MechanicalComponent engine = MechanicalComponent.Engine;
        var baseline = ReliabilityEraProfile.LapHazard(1955, engine, 0.5, 0.5, 0);

        Assert.True(ReliabilityEraProfile.LapHazard(1955, engine, 0.9, 0.5, 0) > baseline);
        Assert.True(ReliabilityEraProfile.LapHazard(1955, engine, 0.5, 0.5, 500) > baseline);
        Assert.True(ReliabilityEraProfile.LapHazard(1955, engine, 0.5, 0.9, 0) < baseline);
        Assert.True(ReliabilityEraProfile.LapHazard(2015, engine, 0.5, 0.5, 0) < baseline);
    }

    [Fact]
    public void LapHazard_MatchesTheDocumentedFormula()
    {
        const MechanicalComponent gearbox = MechanicalComponent.Gearbox;
        var expected = ReliabilityEraProfile.BaseHazard(1970, gearbox)
            * (1 + 0.4)
            * (1 - 0.7 * ReliabilityConstants.RatingEffect)
            * (1 + ReliabilityConstants.AgeSlope * 250 / ReliabilityConstants.ComponentLifeLaps(gearbox));

        Assert.Equal(expected, ReliabilityEraProfile.LapHazard(1970, gearbox, 0.4, 0.7, 250), 12);
    }

    [Fact]
    public void FailureInputs_StressStaysInTheUnitRange()
    {
        Assert.Equal(0.0, new FailureInputs(1955, 1, 0, 0).Stress, 12);
        Assert.Equal(1.0, new FailureInputs(1955, 0, 1, 1).Stress, 12);
        Assert.True(new FailureInputs(1955, 0.2, 0.5, 0.5).Stress > new FailureInputs(1955, 0.8, 0.5, 0.5).Stress);
    }
}
