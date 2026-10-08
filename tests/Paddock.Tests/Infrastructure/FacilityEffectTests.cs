using Paddock.Application.Access;
using Paddock.Application.Infrastructure;
using Paddock.Domain.Finance;
using Paddock.Domain.Infrastructure;
using AccessManagerId = Paddock.Application.Access.ManagerId;

namespace Paddock.Tests.Infrastructure;

/// <summary>
/// What a facility gives, in plain numbers (#268), and what a trip costs next to the weekend it is made for. Every share is an ESTIMATE; these
/// tests pin that the screen cannot say something the development does not do, and the order of the transport costs.
/// </summary>
public sealed class FacilityEffectTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.37)]
    [InlineData(1.0)]
    public void AFactorysEffectsAreExactlyWhatTheDevelopmentReads(double relative)
    {
        var effects = InfrastructureMath.Effects(FacilityKind.Factory, relative, relative).ToDictionary(effect => effect.Key);

        Assert.Equal(InfrastructureMath.ExecutionQualityScale(relative, 0d, 0d), effects[FacilityEffectKeys.Execution].Now);
        Assert.Equal(InfrastructureMath.DurationScale(relative), effects[FacilityEffectKeys.Duration].Now);
        Assert.Equal(InfrastructureMath.UnderstandingScale(relative, 0d), effects[FacilityEffectKeys.Understanding].Now);
        Assert.All(effects.Values, effect => Assert.Equal(FacilityEffectMode.Scale, effect.Mode));
    }

    [Fact]
    public void ABetterFactoryMakesPartsBetterAndFasterAndNeverTheOtherWayRound()
    {
        var none = InfrastructureMath.Effects(FacilityKind.Factory, 0d, 0d).ToDictionary(effect => effect.Key);
        var full = InfrastructureMath.Effects(FacilityKind.Factory, 1d, 1d).ToDictionary(effect => effect.Key);

        Assert.True(full[FacilityEffectKeys.Execution].Now > none[FacilityEffectKeys.Execution].Now);
        Assert.True(full[FacilityEffectKeys.Understanding].Now > none[FacilityEffectKeys.Understanding].Now);
        Assert.True(full[FacilityEffectKeys.Duration].Now < none[FacilityEffectKeys.Duration].Now);
        Assert.Equal(none[FacilityEffectKeys.Execution].Now, none[FacilityEffectKeys.Execution].AtZero);
        Assert.Equal(full[FacilityEffectKeys.Execution].Now, full[FacilityEffectKeys.Execution].AtFull);
    }

    [Fact]
    public void TheTunnelAndCfdAddToExecutionAndTheSimulatorToLearningAsASharePerLevel()
    {
        var tunnel = Assert.Single(InfrastructureMath.Effects(FacilityKind.WindTunnel, 0.5, 0.75));
        var cfd = Assert.Single(InfrastructureMath.Effects(FacilityKind.Cfd, 1d, 1d));
        var simulator = Assert.Single(InfrastructureMath.Effects(FacilityKind.Simulator, 0d, 1d));

        Assert.Equal((FacilityEffectKeys.Execution, FacilityEffectMode.Bonus), (tunnel.Key, tunnel.Mode));
        Assert.Equal(InfrastructureEstimates.ExecutionTunnelSpan * 0.5, tunnel.Now, 3);
        Assert.Equal(InfrastructureEstimates.ExecutionTunnelSpan * 0.75, tunnel.AfterUpgrade, 3);
        Assert.Equal(InfrastructureEstimates.ExecutionTunnelSpan, tunnel.AtFull);
        Assert.Equal(InfrastructureEstimates.ExecutionCfdSpan, cfd.Now, 3);
        Assert.Equal((FacilityEffectKeys.Understanding, 0d, InfrastructureEstimates.UnderstandingSimulatorSpan), (simulator.Key, simulator.Now, simulator.AfterUpgrade));
    }

    [Fact]
    public void ARelativeQualityAboveTheStateOfTheArtDoesNotGiveMoreThanTheCeiling()
    {
        var effect = InfrastructureMath.Effects(FacilityKind.WindTunnel, 1.4, 1.6).Single();

        Assert.Equal(InfrastructureEstimates.ExecutionTunnelSpan, effect.Now, 3);
        Assert.Equal(InfrastructureEstimates.ExecutionTunnelSpan, effect.AfterUpgrade, 3);
    }

    [Fact]
    public void TheViewCarriesTheEffectsAndWhatTheNextUpgradeWouldGive()
    {
        var kit = new InfrastructureKit();
        var view = new InfrastructureQuery(kit.Book, kit.Environment).View(AccessContext.ForManager(new AccessManagerId(InfrastructureKit.Anna.Value)));

        var own = Assert.Single(view.Own);
        var factory = own.Facilities.Single(row => row.Kind == FacilityKindIds.Factory);
        Assert.True(factory.RelativeAfterUpgrade > factory.RelativeQuality);
        Assert.Equal(3, factory.Effects.Count);
        var execution = factory.Effects.Single(effect => effect.Key == FacilityEffectKeys.Execution);
        Assert.Equal("scale", execution.Mode);
        Assert.True(execution.AfterUpgrade > execution.Now);
        Assert.InRange(execution.Now, execution.AtZero, execution.AtFull);
        var tunnel = own.Facilities.Single(row => row.Kind == FacilityKindIds.WindTunnel);
        Assert.False(tunnel.Unlocked);
        Assert.Equal(1968, tunnel.UnlockYear);
    }

    [Fact]
    public void ATripCostsMoreTheFartherItGoesAndAShipIsMoreThanTheWholeWeekend()
    {
        const int rounds = 8;
        var weekend = FinanceEstimates.RaceRunningShare / rounds;

        Assert.True(InfrastructureEstimates.LogisticsHomeShare < InfrastructureEstimates.LogisticsLorryShare);
        Assert.True(InfrastructureEstimates.LogisticsLorryShare < InfrastructureEstimates.LogisticsShipShare);
        Assert.True(InfrastructureEstimates.LogisticsShipShare > weekend, "A ship to another continent costs more than the weekend itself.");
        Assert.InRange(InfrastructureEstimates.LogisticsLorryShare / weekend, 0.3, 0.6);
        Assert.True(InfrastructureEstimates.LogisticsHomeShare / weekend < 0.25);
    }

    [Fact]
    public void TheFirstRoundOf1955IsNoLongerTwoHundredDollars()
    {
        var typical = Money.FromDollars(60_000).Cents;

        var ship = LogisticsMath.Quote("ITA", LogisticsMath.Argentina, typical);

        Assert.True(new Money(ship.CostCents).WholeDollars >= 1_000, "The owner found 210 dollars far too cheap.");
        Assert.Equal((long)Math.Round(typical * InfrastructureEstimates.LogisticsShipShare), ship.CostCents);
    }
}
