using Paddock.Simulation.Racing.Tyres;

namespace Paddock.Tests.Racing.Tyres;

/// <summary>All numbers under test are ESTIMATE (see TyreFuelConstants).</summary>
public class TyreWearTests
{
    private static TyreCompound C3 => TyreCompoundCatalog.Default.AvailableCompounds(2020).Single(c => c.Id == "C3");

    public static TheoryData<double, double, double> ConditionGrid()
    {
        var data = new TheoryData<double, double, double>();
        foreach (var abrasiveness in new[] { 0.0, 0.5, 1.0 })
        {
            foreach (var smoothness in new[] { 0.0, 0.5, 1.0 })
            {
                foreach (var weight in new[] { 0.9, 1.0, 1.3 })
                {
                    data.Add(abrasiveness, smoothness, weight);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ConditionGrid))]
    public void LossIsMonotonicInAge_ForEveryCompoundOfEveryEra(double abrasiveness, double smoothness, double weight)
    {
        for (var season = 1950; season <= 2026; season += 4)
        {
            foreach (var compound in TyreCompoundCatalog.Default.AvailableCompounds(season))
            {
                var previous = double.NegativeInfinity;
                for (var age = 0; age <= 120; age++)
                {
                    var loss = TyreWear.LossAtAge(compound, age, abrasiveness, smoothness, weight);
                    Assert.True(loss > previous, $"{compound.Id} age {age}: {loss} is not above {previous}");
                    previous = loss;
                }
            }
        }
    }

    [Fact]
    public void FreshSet_LosesExactlyItsGripBase()
    {
        Assert.Equal(C3.GripBase, TyreWear.LossAtAge(C3, 0, 0.5, 0.5, 1.0), 12);
    }

    [Fact]
    public void Cliff_DropsSuddenly_ThenWearsSteeply()
    {
        var c = TyreConditions.Reference; // multiplier 1, so effective age equals age
        var cliff = (int)C3.CliffLap;
        double Loss(int age) => TyreWear.LossAtAge(C3, age, c);

        var beforeStep = Loss(cliff) - Loss(cliff - 1);
        var cliffStep = Loss(cliff + 1) - Loss(cliff);
        var afterStep = Loss(cliff + 3) - Loss(cliff + 2);

        Assert.True(cliffStep >= TyreFuelConstants.CliffDropSeconds, $"the step over the cliff was only {cliffStep}");
        Assert.True(cliffStep > 5 * beforeStep, "the cliff step should dwarf an ordinary lap of wear");
        Assert.True(afterStep > 2 * beforeStep, "wear after the cliff should be steeper than before it");
    }

    [Fact]
    public void ConstantConditions_AgeAndAccumulatedWearAgree()
    {
        var conditions = new TyreConditions(0.8, 0.3, 1.1);
        var set = TyreSet.New(C3);
        for (var age = 0; age <= 40; age++)
        {
            Assert.Equal(age, set.AgeLaps);
            Assert.Equal(TyreWear.LossAtAge(C3, age, conditions), set.CurrentLossSeconds, 9);
            set = set.AfterLap(conditions);
        }
    }

    [Fact]
    public void AbrasiveTrack_HarshDriver_AndHeavyCar_AllWearFaster()
    {
        double Loss(double abrasiveness, double smoothness, double weight) =>
            TyreWear.LossAtAge(C3, 15, abrasiveness, smoothness, weight);

        var typical = Loss(0.5, 0.5, 1.0);
        Assert.True(Loss(0.9, 0.5, 1.0) > typical);
        Assert.True(Loss(0.1, 0.5, 1.0) < typical);
        Assert.True(Loss(0.5, 0.1, 1.0) > typical);
        Assert.True(Loss(0.5, 0.9, 1.0) < typical);
        Assert.True(Loss(0.5, 0.5, 1.2) > typical);
        Assert.True(Loss(0.5, 0.5, 0.9) < typical);
    }

    [Fact]
    public void WarmUp_PenaltyIsPeakWhenFresh_AndGoneAfterWarmUpLaps()
    {
        Assert.Equal(TyreFuelConstants.WarmUpPeakLossSeconds, TyreWear.WarmUpLoss(C3, 0), 12);
        Assert.True(TyreWear.WarmUpLoss(C3, 1) < TyreWear.WarmUpLoss(C3, 0));
        Assert.Equal(0, TyreWear.WarmUpLoss(C3, (int)Math.Ceiling(C3.WarmUpLaps)));
        var noWarmUp = TyreCompoundCatalog.Default.AvailableCompounds(1955).Single();
        Assert.Equal(0, TyreWear.WarmUpLoss(noWarmUp, 0));
    }

    [Fact]
    public void LapLoss_IsLossPlusWarmUp()
    {
        var c = TyreConditions.Reference;
        Assert.Equal(TyreWear.LossAtAge(C3, 0, c) + TyreWear.WarmUpLoss(C3, 0), TyreWear.LapLoss(C3, 0, c), 12);
        Assert.Equal(TyreWear.LossAtAge(C3, 5, c), TyreWear.LapLoss(C3, 5, c), 12);
    }

    [Fact]
    public void TyreSet_ReportsTheCliff()
    {
        var set = TyreSet.New(C3);
        Assert.False(set.PastCliff);
        for (var lap = 0; lap < 31; lap++)
        {
            set = set.AfterLap(TyreConditions.Reference);
        }

        Assert.True(set.PastCliff);
    }

    [Fact]
    public void Functions_AreDeterministic_AndRepeatable()
    {
        double Run()
        {
            var set = TyreSet.New(C3);
            var total = 0.0;
            for (var lap = 0; lap < 35; lap++)
            {
                var conditions = new TyreConditions(0.6, 0.4, FuelModel.CarWeightFactor(100 - 1.7 * lap));
                total += set.CurrentLapLossSeconds;
                set = set.AfterLap(conditions);
            }

            return total;
        }

        Assert.Equal(Run(), Run());
        Assert.Equal(
            TyreWear.LossAtAge(C3, 12, 0.3, 0.7, 1.05),
            TyreWear.LossAtAge(C3, 12, 0.3, 0.7, 1.05));
    }

    [Fact]
    public void InvalidInput_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TyreWear.LossAtAge(C3, -1, 0.5, 0.5, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TyreWear.LossAtAge(C3, 1, 1.5, 0.5, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TyreWear.LossAtAge(C3, 1, 0.5, -0.1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TyreWear.LossAtAge(C3, 1, 0.5, 0.5, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TyreCompound("x", TyreCompoundKind.Dry, 0, 0, 0, 10, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TyreCompound("x", TyreCompoundKind.Dry, 0, 0, 0.1, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TyreCompound("x", TyreCompoundKind.Dry, 0, 0, 0.1, 10, -1));
    }

    [Fact]
    public void Supplier_GrippyIsFasterWhenFresh_ButSlowerWhenWorn()
    {
        var grippy = new TyreSupplierProfile("grippy", 1, 0);
        var durable = new TyreSupplierProfile("durable", -1, 0);
        var neutral = TyreSupplierProfile.Neutral;
        var c = TyreConditions.Reference;

        double Loss(TyreSupplierProfile supplier, int age) => TyreWear.LossAtAge(supplier.Apply(C3, false), age, c);

        Assert.True(Loss(grippy, 0) < Loss(neutral, 0));
        Assert.True(Loss(neutral, 0) < Loss(durable, 0));
        Assert.True(Loss(grippy, 29) > Loss(neutral, 29));
        Assert.True(Loss(neutral, 29) > Loss(durable, 29));
        Assert.Equal(Loss(neutral, 10), TyreWear.LossAtAge(C3, 10, c), 12);
    }

    [Fact]
    public void Supplier_PartnerTunedTyreIsBetterInGripAndWear()
    {
        var supplier = new TyreSupplierProfile("s", 0.2, 0.8);
        var c = TyreConditions.Reference;
        var partner = supplier.Apply(C3, true);
        var customer = supplier.Apply(C3, false);

        Assert.True(partner.GripBase < customer.GripBase);
        Assert.True(partner.WearRate < customer.WearRate);
        Assert.True(TyreWear.LossAtAge(partner, 20, c) < TyreWear.LossAtAge(customer, 20, c));
        Assert.Equal(C3.CliffLap, partner.CliffLap);
        Assert.Equal(C3.Id, partner.Id);
    }

    [Fact]
    public void Supplier_RejectsOutOfRangeTraits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TyreSupplierProfile("s", 1.1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TyreSupplierProfile("s", 0, -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TyreSupplierProfile("s", 0, 1.1));
    }
}
