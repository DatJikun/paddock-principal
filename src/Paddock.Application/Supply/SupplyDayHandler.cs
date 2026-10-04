using System.Globalization;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Contracts;
using Paddock.Domain.Finance;
using Paddock.Domain.Inbox;
using Paddock.Domain.Random;
using Paddock.Domain.Spy;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Time;

namespace Paddock.Application.Supply;

/// <summary>
/// Supply for one lived day, in this order:
/// <list type="number">
/// <item>An offer with no answer day gets one, a few days away; the jitter is a child of the <c>Market</c> stream keyed by the
/// negotiation and the round, so an unrelated negotiation never shifts it (INV-004). A supplier whose day has come answers
/// (<see cref="SupplierResponder"/>): accept signs the deal at once, counter waits for the manager, refuse closes the talks.
/// Every answer leaves a decision trace and a notice to the managers of the customer.</item>
/// <item>Negotiations past their deadline lapse.</item>
/// <item>Fees: the first day of each season a deal is in force, the customer pays its yearly price to the ledger (category
/// <c>supply</c>) and a supplier that keeps books books the sale. A works deal is never charged.</item>
/// <item>Deals past their last season end, and the cars are pointed at the engine deal in force.</item>
/// </list>
/// It posts only through <see cref="FinanceSection.Post"/> and changes only the supply, finance and cars sections.
/// </summary>
public sealed class SupplyDayHandler : IDayHandler
{
    /// <summary>ESTIMATE: after sponsors (750), before the finance review (800) so it sees the fee.</summary>
    public const int DefaultOrder = 760;

    /// <summary>ESTIMATE: a supplier answers two to four days after an offer (the T39 delay).</summary>
    private const int DelayMinDays = NegotiationEstimates.ResponseDelayMinDays;

    private readonly SupplyBook _book;
    private readonly SupplyEnvironment _environment;
    private readonly InboxBook? _inbox;
    private readonly ManagerRegistry? _managers;

    public SupplyDayHandler(
        SupplyBook book,
        SupplyEnvironment environment,
        InboxBook? inbox = null,
        ManagerRegistry? managers = null,
        int order = DefaultOrder)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
        _inbox = inbox;
        _managers = managers;
        Order = order;
    }

    public int Order { get; }

    public void OnDay(DayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var today = context.Today;
        var start = _book.World;
        var supply = start.Section<SupplySection>(SupplySection.SectionName) ?? SupplySection.Empty;
        if (supply.Deals.Count == 0 && supply.Negotiations.Count == 0)
        {
            return;
        }

        var world = start;
        var market = new Lazy<RngStream>(() => new RngStream(RngStreamName.Market, context.Stream(RngStreamName.Market).State));
        foreach (var id in supply.Active().Select(negotiation => negotiation.Id).ToArray())
        {
            world = Answer(world, id, market, today);
        }

        supply = SupplyOf(world);
        foreach (var negotiation in supply.Active().Where(negotiation => negotiation.Deadline < today).ToArray())
        {
            var lapsed = negotiation.WithLapse([NegotiationReasons.DeadlinePassed], today);
            supply = supply.ReplaceNegotiation(lapsed);
            Notice(negotiation.Customer, SupplyKeys.InboxLapsedSubject, negotiation, today);
        }

        var financeBefore = world.Section<FinanceSection>(FinanceSection.SectionName) ?? FinanceSection.Empty;
        var finance = financeBefore;
        foreach (var deal in supply.Deals.Where(deal => deal.IsInForceOn(today) && deal.Kind != SupplyKind.Works && deal.PaidSeason < today.Year))
        {
            if (!finance.HasBook(deal.Customer))
            {
                continue;
            }

            finance = finance.Post(deal.Customer, today, LedgerCategories.Supply, deal.Supplier.Value, -deal.AnnualPriceCents, SupplyReasons.Fee);
            if (finance.HasBook(deal.Supplier))
            {
                finance = finance.Post(deal.Supplier, today, LedgerCategories.Supply, deal.Customer.Value, deal.AnnualPriceCents, SupplyReasons.Sale);
            }

            supply = supply.ReplaceDeal(deal.WithPaidSeason(today.Year));
        }

        foreach (var deal in supply.Deals.Where(deal => deal.IsActive && today.Year > deal.LastSeason).ToArray())
        {
            supply = supply.ReplaceDeal(deal.Ended(today));
            Notice(deal.Customer, SupplyKeys.InboxEndedSubject, deal, today);
        }

        if (!ReferenceEquals(supply, SupplyOf(start)))
        {
            world = world.WithSection(supply);
        }

        if (!ReferenceEquals(finance, financeBefore))
        {
            world = world.WithSection(finance);
        }

        world = SupplyCars.Sync(world, supply, today);
        if (!ReferenceEquals(world, start))
        {
            _book.Update(world);
        }
    }

    private static SupplySection SupplyOf(WorldState world) => world.Section<SupplySection>(SupplySection.SectionName) ?? SupplySection.Empty;

    private WorldState Answer(WorldState world, string negotiationId, Lazy<RngStream> market, GameDate today)
    {
        var supply = SupplyOf(world);
        var negotiation = supply.FindNegotiation(negotiationId)!;
        if (negotiation.Status != NegotiationStatus.AwaitingResponse || negotiation.Deadline < today)
        {
            return world;
        }

        if (negotiation.RespondOn is null)
        {
            if (negotiation.Deadline > today)
            {
                var tag = string.Create(CultureInfo.InvariantCulture, $"supply-delay:{negotiation.Id}:{negotiation.RoundsUsed}");
                var jitter = market.Value.DeriveChild(tag).NextInt(0, NegotiationEstimates.ResponseDelayJitterDays + 1);
                var respondOn = today.AddDays(DelayMinDays + jitter);
                if (respondOn > negotiation.Deadline)
                {
                    respondOn = negotiation.Deadline;
                }

                return world.WithSection(supply.ReplaceNegotiation(negotiation.WithResponseScheduled(respondOn)));
            }

            negotiation = negotiation.WithResponseScheduled(today);
        }

        if (negotiation.RespondOn > today)
        {
            return world;
        }

        var situation = SupplyRules.SituationOf(supply, _environment, negotiation, today);
        var answer = SupplierResponder.Respond(negotiation, situation);
        if (_environment.Trace.IsEnabled)
        {
            _environment.Trace.Record(Trace(negotiation, answer, situation, today));
        }

        switch (answer.Kind)
        {
            case SupplyAnswerKind.Accept:
                var signed = SupplyRules.Sign(world, negotiation, negotiation.Offer, today);
                if (signed is null)
                {
                    var lapsed = negotiation.WithRefusal([NegotiationReasons.SignedElsewhere], today);
                    Notice(negotiation.Customer, SupplyKeys.InboxRefusedSubject, lapsed, today);
                    return world.WithSection(supply.ReplaceNegotiation(lapsed));
                }

                Notice(negotiation.Customer, SupplyKeys.InboxSignedSubject, negotiation, today);
                return signed.Value.World;
            case SupplyAnswerKind.Counter:
                var countered = negotiation.WithCounter(answer.Counter!, answer.Reasons, today);
                Notice(negotiation.Customer, SupplyKeys.InboxCounteredSubject, countered, today);
                return world.WithSection(supply.ReplaceNegotiation(countered));
            default:
                var refused = negotiation.WithRefusal(answer.Reasons, today);
                Notice(negotiation.Customer, SupplyKeys.InboxRefusedSubject, refused, today);
                return world.WithSection(supply.ReplaceNegotiation(refused));
        }
    }

    /// <summary>
    /// The trace of a supplier's answer (TECH section 7). Only the stated reasons are player-visible; the floor and the price the
    /// supplier would accept are truth context. Building it reads what the decision already computed: no RNG, no state (INV-005, INV-006).
    /// </summary>
    private static DecisionTrace Trace(SupplyNegotiation negotiation, SupplyAnswer answer, SupplierSituation situation, GameDate today)
    {
        var chosen = answer.Kind switch
        {
            SupplyAnswerKind.Accept => "accept",
            SupplyAnswerKind.Counter => "counter",
            _ => "refuse",
        };
        var ratio = answer.AcceptCents == 0 ? 0.0 : (double)negotiation.Offer.AnnualPriceCents / answer.AcceptCents;
        return new DecisionTrace(
            new WeekendKey(today.Season, 0),
            negotiation.Supplier.Value,
            NegotiationEstimates.PersonDeciderLevel,
            "supply.response:" + negotiation.Id,
            [
                new TraceOption("accept", ratio, [new TraceFactor("price_ratio", ratio, answer.Reasons.Contains(SupplyReasons.PriceTooLow))], chosen == "accept"),
                new TraceOption("counter", 0, [], chosen == "counter"),
                new TraceOption("refuse", 0, [], chosen == "refuse"),
            ],
            chosen,
            "offer " + negotiation.Offer.AnnualPriceCents.ToString(CultureInfo.InvariantCulture) + " against the price accepted "
            + answer.AcceptCents.ToString(CultureInfo.InvariantCulture),
            answer.Reasons.Count == 0 ? null : string.Join(",", answer.Reasons),
            chosen == "accept",
            new Dictionary<string, string>
            {
                ["negotiation"] = negotiation.Id,
                ["floorCents"] = answer.FloorCents.ToString(CultureInfo.InvariantCulture),
                ["acceptCents"] = answer.AcceptCents.ToString(CultureInfo.InvariantCulture),
                ["interest"] = negotiation.Interest.ToString(CultureInfo.InvariantCulture),
                ["activeCustomers"] = situation.ActiveCustomers.ToString(CultureInfo.InvariantCulture),
                ["exclusiveToOther"] = situation.ExclusiveToOther ? "1" : "0",
            });
    }

    private void Notice(OrganizationId customer, string subjectKey, SupplyNegotiation negotiation, GameDate today) =>
        Post(customer, subjectKey, today, negotiation.Supplier, negotiation.Item, negotiation.Id);

    private void Notice(OrganizationId customer, string subjectKey, SupplyDeal deal, GameDate today) =>
        Post(customer, subjectKey, today, deal.Supplier, deal.Item, deal.Id);

    private void Post(OrganizationId customer, string subjectKey, GameDate today, OrganizationId supplier, SupplyItem item, string reference)
    {
        if (_inbox is null || _managers is null)
        {
            return;
        }

        var name = _book.World.Organizations.FirstOrDefault(organization => organization.Id == supplier)?.NameOn(today) ?? supplier.Value;
        foreach (var manager in _environment.Control.ManagersOf(customer))
        {
            var draft = new InboxItemDraft(
                SupplyKeys.InboxKind,
                subjectKey,
                [
                    new KeyValuePair<string, string>("supplier", name),
                    new KeyValuePair<string, string>("item", item.ToString()),
                    new KeyValuePair<string, string>("reference", reference),
                ],
                null,
                null,
                null);
            _inbox.Post(_managers, manager, draft, today);
        }
    }
}
