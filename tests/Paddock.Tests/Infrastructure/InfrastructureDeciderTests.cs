using Paddock.Domain.Infrastructure;
using Paddock.Domain.Random;
using Paddock.Simulation.Ai;

namespace Paddock.Tests.Infrastructure;

public class InfrastructureDeciderTests
{
    private static DecisionContext Context(PrincipalArchetype archetype) =>
        new("alfa", "ai:alfa", new DateOnly(1955, 1, 1), archetype, new PrincipalSkillset(4, 4, 4, 4), AiRandom.ForSeason(3UL, 1955));

    [Fact]
    public void ABuilderWithCashPicksAnUpgradeOrATest()
    {
        var input = new InfrastructureInput(
            new DateOnly(1955, 1, 1),
            10_000_000,
            [new FacilityCase(FacilityKindIds.Factory, 0.2, 200_000, false, true)],
            new TestRentalCase(50_000, 0, 12, true));
        var decision = InfrastructureDecider.Review(Context(PrincipalArchetype.Builder), input);
        Assert.True(decision.Kind == FacilityKindIds.Factory || decision.BookTest);
    }

    [Fact]
    public void NoCashMeansWait()
    {
        var input = new InfrastructureInput(
            new DateOnly(1955, 1, 1),
            0,
            [new FacilityCase(FacilityKindIds.Factory, 0.2, 200_000, false, true)],
            new TestRentalCase(50_000, 0, 12, true));
        var decision = InfrastructureDecider.Review(Context(PrincipalArchetype.Survivor), input);
        Assert.Null(decision.Kind);
        Assert.False(decision.BookTest);
    }

    [Fact]
    public void ANearFrontierPlantIsLeftAloneEvenWithCash()
    {
        var input = new InfrastructureInput(
            new DateOnly(1955, 1, 1),
            10_000_000,
            [new FacilityCase(FacilityKindIds.Factory, 0.45, 200_000, false, true)],
            new TestRentalCase(50_000, 0, 12, true));
        var decision = InfrastructureDecider.Review(Context(PrincipalArchetype.Builder), input);
        Assert.Null(decision.Kind);
    }
}
