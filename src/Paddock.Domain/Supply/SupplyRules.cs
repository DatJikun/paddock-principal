using Paddock.Domain.Cars;
using Paddock.Domain.Contracts;
using Paddock.Domain.Finance;
using Paddock.Domain.World;

namespace Paddock.Domain.Supply;

/// <summary>Prices in cents (T37 money). Every number behind them is an ESTIMATE (<see cref="SupplyEstimates"/>).</summary>
public static class SupplyPricing
{
    /// <summary>A typical team's era budget in cents: the yardstick every price is a share of.</summary>
    public static long ReferenceBudgetCents(IReadOnlyList<RulePeriod> periods, int season) =>
        Money.FromDollars(EraFinance.ForYear(periods, season).TypicalDollars).Cents;

    /// <summary>The yearly price of a kind of deal with a one-season, non-exclusive term. Zero for a works deal.</summary>
    public static long ReferenceCents(SupplyItem item, SupplyKind kind, long referenceBudgetCents) =>
        referenceBudgetCents * SupplyEstimates.ReferenceBudgetMilli(item) / 1000 * SupplyEstimates.PriceMilli(kind) / 1000;

    /// <summary>
    /// The least a supplier asks for a yearly price: the reference price, plus the exclusivity markup, minus the long-deal discount.
    /// Rounded up, and never below one cent for a priced kind.
    /// </summary>
    public static long FloorCents(SupplyItem item, SupplyKind kind, SupplyTerms shape, long referenceBudgetCents)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var reference = ReferenceCents(item, kind, referenceBudgetCents);
        if (reference <= 0)
        {
            return 0;
        }

        var milli = 1000
            + (shape.Exclusive ? SupplyEstimates.ExclusiveMarkupMilli : 0)
            - Math.Min(SupplyEstimates.YearsDiscountCapMilli, (shape.Seasons - 1) * SupplyEstimates.YearsDiscountMilli);
        return Math.Max(1, CeilDiv(reference * milli, 1000));
    }

    public static long CeilDiv(long numerator, long denominator) => (numerator + denominator - 1) / denominator;
}

/// <summary>What a supplier knows when it answers: how many it already serves, and whether someone holds it exclusively.</summary>
public sealed record SupplierSituation(long ReferenceBudgetCents, int ActiveCustomers, bool ExclusiveToOther);

/// <summary>What the supplier does with an offer.</summary>
public enum SupplyAnswerKind
{
    Accept = 0,
    Counter = 1,
    Refuse = 2,
}

/// <summary>
/// The supplier's answer, with what a decision trace needs. <see cref="Reasons"/> are never empty for a counter or a refusal;
/// <see cref="Floor"/> and <see cref="AcceptCents"/> are the supplier's own numbers (truth, never shown to a manager).
/// </summary>
public sealed record SupplyAnswer(SupplyAnswerKind Kind, SupplyTerms? Counter, IReadOnlyList<string> Reasons, long FloorCents, long AcceptCents);

/// <summary>
/// How a supplier answers an offer. A pure function (INV-005): no RNG. Structural limits first (capacity, exclusivity), then the
/// price: accepted when it reaches the floor plus a markup for lost interest (the same <see cref="NegotiationEstimates.InterestMargin"/>
/// the T39 core uses), otherwise countered at that price while rounds remain, otherwise refused.
/// </summary>
public static class SupplierResponder
{
    public static SupplyAnswer Respond(SupplyNegotiation negotiation, SupplierSituation situation)
    {
        ArgumentNullException.ThrowIfNull(negotiation);
        ArgumentNullException.ThrowIfNull(situation);
        var offer = negotiation.Offer;
        var floor = SupplyPricing.FloorCents(negotiation.Item, negotiation.Kind, offer, situation.ReferenceBudgetCents);
        var lostMilli = (long)Math.Round(
            (NegotiationEstimates.InterestStart - negotiation.Interest) * NegotiationEstimates.InterestMargin,
            MidpointRounding.AwayFromZero);
        var accept = SupplyPricing.CeilDiv(floor * (1000 + lostMilli), 1000);

        if (situation.ExclusiveToOther)
        {
            return Refuse(floor, accept, SupplyReasons.ExclusiveTaken);
        }

        if (offer.Exclusive && situation.ActiveCustomers > 0)
        {
            return Refuse(floor, accept, SupplyReasons.ExclusiveUnavailable);
        }

        if (situation.ActiveCustomers >= SupplyEstimates.MaxCustomersPerSupplier)
        {
            return Refuse(floor, accept, SupplyReasons.NoCapacity);
        }

        if (negotiation.Interest <= 0)
        {
            return Refuse(floor, accept, NegotiationReasons.NoRealChange, NegotiationReasons.PatienceLost);
        }

        if (offer.AnnualPriceCents >= accept)
        {
            return new SupplyAnswer(SupplyAnswerKind.Accept, null, [], floor, accept);
        }

        var reasons = new List<string> { SupplyReasons.PriceTooLow };
        if (negotiation.Interest < NegotiationEstimates.InterestStart)
        {
            // Interest only falls on a nudge, so the supplier says why it lost interest, as in T39.
            reasons.Insert(0, NegotiationReasons.NoRealChange);
        }

        if (negotiation.RoundsLeft > 0)
        {
            return new SupplyAnswer(
                SupplyAnswerKind.Counter,
                new SupplyTerms(Math.Max(accept, 1), offer.Seasons, offer.Exclusive),
                reasons,
                floor,
                accept);
        }

        reasons.Add(NegotiationReasons.PatienceLost);
        return new SupplyAnswer(SupplyAnswerKind.Refuse, null, reasons, floor, accept);
    }

    private static SupplyAnswer Refuse(long floor, long accept, params string[] reasons) =>
        new(SupplyAnswerKind.Refuse, null, reasons, floor, accept);
}

/// <summary>Maps the authored <c>supply_types</c> of <c>engines.json</c> to a deal kind. The default mapping is open question 1 of the issue.</summary>
public static class SupplyKindMap
{
    /// <summary>
    /// <c>works</c> is a works deal, <c>partner</c> a partner deal, <c>customer</c> a customer deal. <c>badged</c> (the entered name is not the
    /// designer) and <c>unknown</c> (supplier established, type not) both count as customer deals: the supplier organization is the
    /// designer, and nothing says the team had a factory deal. Any other text is refused so a new value in the data is noticed.
    /// </summary>
    public static SupplyKind FromAuthored(string supplyType)
    {
        ArgumentNullException.ThrowIfNull(supplyType);
        return supplyType switch
        {
            "works" => SupplyKind.Works,
            "partner" => SupplyKind.Partner,
            "customer" or "badged" or "unknown" => SupplyKind.Customer,
            _ => throw new ArgumentException("Unknown authored supply type '" + supplyType + "'.", nameof(supplyType)),
        };
    }
}
