using Paddock.Domain.Development;
using Paddock.Domain.Finance;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Development;

/// <summary>What committing a concept would take: days of building and the money for it. Numbers are ESTIMATES.</summary>
public sealed record ProductionPlan(int Days, long CostCents);

/// <summary>
/// Production of a committed concept (T42c, PP-043). Duration grows with the car complexity of the era and shrinks with the
/// team's headcount; it is never lowered by quality and never raises quality (DESIGN §5.3). Pure: no RNG, no state, so the query
/// can show the same numbers the command will use (INV-005).
/// </summary>
public static class ConceptProduction
{
    public static ProductionPlan Plan(WorldState world, FinanceSection finance, OrganizationId organization, DevProject concept, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(finance);
        ArgumentNullException.ThrowIfNull(concept);
        var annual = (long)DevelopmentMath.AnnualBudgetCents(finance.TypicalCents);
        var balance = finance.HasBook(organization) ? finance.BalanceOf(organization) : 0L;
        var capacity = EngineeringCapacity.Derive(
            EngineerRoster.Of(world, organization, today),
            today.Year,
            EngineerRoster.ChairsIn(today.Year),
            balance,
            annual);
        return new ProductionPlan(
            capacity.DurationDays(DevelopmentMath.ConceptProductionDays(today.Year)),
            DevelopmentMath.ProductionCostCents(concept.CostCents));
    }
}

/// <summary>The first race of a team on or after a day, so the player can see when a concept would meet the track. A host supplies it; none means "unknown".</summary>
public interface INextRaceSource
{
    GameDate? NextRaceOnOrAfter(OrganizationId organization, GameDate day);
}
