using Paddock.Domain.World;
using Paddock.Simulation.Racing.Tyres;
using Paddock.Tests.Racing.Points;

namespace Paddock.Tests.Racing.Tyres;

/// <summary>Reads the real regulation data for the rules; every number the model adds is ESTIMATE.</summary>
public class FuelModelTests
{
    private static readonly FuelModel Real = FuelModel.FromRuleSets(PointsTestKit.Real.RuleSetFor);

    [Fact]
    public void Refuelling_FollowsTheRealData()
    {
        for (var season = 1950; season <= 1983; season++)
        {
            Assert.True(Real.RefuellingAllowed(season), season.ToString());
        }

        for (var season = 1984; season <= 1993; season++)
        {
            Assert.False(Real.RefuellingAllowed(season), season.ToString());
        }

        for (var season = 1994; season <= 2009; season++)
        {
            Assert.True(Real.RefuellingAllowed(season), season.ToString());
        }

        for (var season = 2010; season <= 2026; season++)
        {
            Assert.False(Real.RefuellingAllowed(season), season.ToString());
        }
    }

    [Fact]
    public void RaceFuelCaps_ComeFromTheDataAllowance()
    {
        Assert.Equal(220 * TyreFuelConstants.FuelDensityKgPerLitre, Real.RaceFuelCapKg(1984, FuelEngineType.Turbo));
        Assert.Equal(195 * TyreFuelConstants.FuelDensityKgPerLitre, Real.RaceFuelCapKg(1986, FuelEngineType.NaturallyAspirated));
        Assert.NotNull(Real.RaceFuelCapKg(1987, FuelEngineType.Turbo));
        Assert.Null(Real.RaceFuelCapKg(1987, FuelEngineType.NaturallyAspirated));
        Assert.Equal(150 * TyreFuelConstants.FuelDensityKgPerLitre, Real.RaceFuelCapKg(1988, FuelEngineType.Turbo));
        Assert.Null(Real.RaceFuelCapKg(1988, FuelEngineType.NaturallyAspirated));
        Assert.Null(Real.RaceFuelCapKg(2000, FuelEngineType.NaturallyAspirated));
        Assert.Equal(105, Real.RaceFuelCapKg(2018, FuelEngineType.Hybrid));
        Assert.Equal(110, Real.RaceFuelCapKg(2022, FuelEngineType.Hybrid));
    }

    [Fact]
    public void UnknownAllowance_GivesHybridsTheEstimateCap()
    {
        Assert.Equal(TyreFuelConstants.HybridUnknownAllowanceCapKg, Real.RaceFuelCapKg(2015, FuelEngineType.Hybrid));
        Assert.Null(Real.RaceFuelCapKg(2015, FuelEngineType.NaturallyAspirated));
    }

    [Fact]
    public void MissingDimensions_BecomeParameters_NotErrors()
    {
        var rules = RuleSet.For(1990, ["other"], [new RulePeriod("other", "x", 1950, null)]);

        Assert.Throws<InvalidOperationException>(() => FuelRegime.FromRules(rules));

        var regime = FuelRegime.FromRules(rules, refuellingFallback: true);
        Assert.True(regime.RefuellingAllowed);
        Assert.True(regime.AllowanceUnknown);

        var model = FuelModel.FromRuleSets(_ => rules, refuellingFallback: false);
        Assert.False(model.RefuellingAllowed(1990));
    }

    [Fact]
    public void ArbitraryRegime_CanBePassedWithoutData()
    {
        var model = new FuelModel(_ => new FuelRegime(true, null, null, false));
        Assert.True(model.RefuellingAllowed(2030));
    }

    [Fact]
    public void FuelMass_FallsOverAStint_AndSoDoesItsPenalty()
    {
        const int laps = 61;
        var burn = Real.BurnPerLapKg(2020, FuelEngineType.Hybrid, 5.0, laps);
        var start = Real.StartLoadKg(2020, FuelEngineType.Hybrid, 5.0, laps);
        var previousFuel = double.PositiveInfinity;
        var previousPenalty = double.PositiveInfinity;
        var previousWeight = double.PositiveInfinity;
        for (var lap = 0; lap <= laps; lap++)
        {
            var fuel = FuelModel.FuelAfterLapsKg(start, burn, lap);
            Assert.True(fuel < previousFuel || fuel == 0);
            Assert.True(FuelModel.MassPenaltySeconds(fuel) <= previousPenalty);
            Assert.True(FuelModel.CarWeightFactor(fuel) <= previousWeight);
            previousFuel = fuel;
            previousPenalty = FuelModel.MassPenaltySeconds(fuel);
            previousWeight = FuelModel.CarWeightFactor(fuel);
        }

        Assert.True(FuelModel.FuelAfterLapsKg(start, burn, laps) >= 0);
        Assert.Equal(0, FuelModel.FuelAfterLapsKg(10, 5, 100));
    }

    [Fact]
    public void MassPenalty_IsLinearInKg()
    {
        Assert.Equal(0, FuelModel.MassPenaltySeconds(0));
        Assert.Equal(3.5, FuelModel.MassPenaltySeconds(100), 9);
        Assert.Equal(2 * FuelModel.MassPenaltySeconds(30), FuelModel.MassPenaltySeconds(60), 9);
        Assert.Throws<ArgumentOutOfRangeException>(() => FuelModel.MassPenaltySeconds(-1));
    }

    [Fact]
    public void CarWeightFactor_IsOneAtTheReferenceLoad()
    {
        Assert.Equal(1.0, FuelModel.CarWeightFactor(TyreFuelConstants.ReferenceFuelKg), 12);
        Assert.True(FuelModel.CarWeightFactor(100) > 1);
        Assert.True(FuelModel.CarWeightFactor(0) < 1);
    }

    [Fact]
    public void ModernRace_NeedsAboutHundredKg_UnderTheCap()
    {
        // 305 km at 5.0 km a lap is 61 laps.
        var start = Real.StartLoadKg(2020, FuelEngineType.Hybrid, 5.0, 61);
        Assert.InRange(start, 90, 110);
        Assert.Equal(110, Real.RaceFuelCapKg(2020, FuelEngineType.Hybrid));
    }

    [Fact]
    public void CapThatBinds_ForcesLowerBurn_AndCapsTheStartLoad()
    {
        // 1987 turbo: the cap is below what the engine would burn over the distance.
        var cap = Real.RaceFuelCapKg(1987, FuelEngineType.Turbo)!.Value;
        const int laps = 70;
        var demand = FuelModel.DemandKgPer100Km(1987, FuelEngineType.Turbo) * 4.3 / 100;
        Assert.True(demand * laps > cap);

        var burn = Real.BurnPerLapKg(1987, FuelEngineType.Turbo, 4.3, laps);
        Assert.True(burn < demand);
        Assert.Equal(cap, burn * laps, 9);
        Assert.Equal(cap, Real.StartLoadKg(1987, FuelEngineType.Turbo, 4.3, laps), 9);
    }

    [Fact]
    public void FuelSaving_BurnsLess_ButCostsPace()
    {
        // 2000: no cap, so the saving mode shows in full.
        var normal = Real.BurnPerLapKg(2000, FuelEngineType.NaturallyAspirated, 4.5, 70);
        var saving = Real.BurnPerLapKg(2000, FuelEngineType.NaturallyAspirated, 4.5, 70, fuelSaving: true);
        Assert.Equal(normal * (1 - TyreFuelConstants.FuelSavingBurnReduction), saving, 9);
        Assert.True(FuelModel.FuelSavingPaceLossSeconds > 0);
    }

    [Fact]
    public void WithRefuelling_StartLoadIsSmaller_AndBannedSeasonsRejectStops()
    {
        var oneStop = Real.StartLoadKg(2000, FuelEngineType.NaturallyAspirated, 4.5, 70, plannedFuelStops: 1);
        var none = Real.StartLoadKg(2000, FuelEngineType.NaturallyAspirated, 4.5, 70);
        Assert.True(oneStop < none);
        Assert.InRange(oneStop / none, 0.45, 0.55);

        Assert.Throws<ArgumentException>(() => Real.StartLoadKg(2020, FuelEngineType.Hybrid, 5.0, 61, plannedFuelStops: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Real.StartLoadKg(2000, FuelEngineType.NaturallyAspirated, 4.5, 70, -1));
    }

    [Fact]
    public void RefuelTime_FollowsTheRig_AndIsRefusedWhenBanned()
    {
        Assert.True(FuelModel.RefuelRateKgPerSecond(2000) > FuelModel.RefuelRateKgPerSecond(1960));
        Assert.Equal(60 / FuelModel.RefuelRateKgPerSecond(2000), Real.RefuelSeconds(2000, 60), 9);
        Assert.Equal(0, Real.RefuelSeconds(2020, 0));
        Assert.Throws<InvalidOperationException>(() => Real.RefuelSeconds(2020, 10));
    }

    [Fact]
    public void TyreChangeGetsFasterOverTheEras()
    {
        var previous = double.PositiveInfinity;
        for (var season = 1950; season <= 2026; season++)
        {
            var seconds = FuelModel.TyreChangeSeconds(season);
            Assert.True(seconds <= previous);
            previous = seconds;
        }

        Assert.True(FuelModel.TyreChangeSeconds(1950) > 20);
        Assert.True(FuelModel.TyreChangeSeconds(2020) < 4);
    }

    [Fact]
    public void StationaryTime_IsTheSlowerOfTyresAndFuel()
    {
        var tyres = FuelModel.TyreChangeSeconds(2000);
        var fuel = Real.RefuelSeconds(2000, 80);
        Assert.Equal(Math.Max(tyres, fuel), Real.StationarySeconds(2000, 80, true), 9);
        Assert.Equal(fuel, Real.StationarySeconds(2000, 80, false), 9);
        Assert.Equal(tyres, Real.StationarySeconds(2000, 0, true), 9);
        Assert.Equal(FuelModel.TyreChangeSeconds(2020), Real.StationarySeconds(2020, 0, true), 9);
    }

    [Fact]
    public void Model_IsDeterministic()
    {
        Assert.Equal(
            Real.StartLoadKg(1995, FuelEngineType.NaturallyAspirated, 4.0, 70, 2),
            Real.StartLoadKg(1995, FuelEngineType.NaturallyAspirated, 4.0, 70, 2));
        Assert.Equal(
            Real.BurnPerLapKg(1987, FuelEngineType.Turbo, 4.3, 70, true),
            Real.BurnPerLapKg(1987, FuelEngineType.Turbo, 4.3, 70, true));
    }

    [Fact]
    public void InvalidInput_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Real.BurnPerLapKg(2000, FuelEngineType.NaturallyAspirated, 0, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => Real.BurnPerLapKg(2000, FuelEngineType.NaturallyAspirated, 4, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => FuelModel.FuelAfterLapsKg(10, 1, -1));
    }
}
