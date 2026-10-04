using System.Globalization;
using Paddock.Domain.Contracts;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Supply;

/// <summary>
/// The supply deals and negotiations of the world, section <see cref="SectionName"/>. Immutable. Numbers only move forward (INV-009).
/// Truth lives here; a manager reads a query. Canonical text (schema 1), after the section header:
/// <code>
/// next-deal &lt;n&gt;
/// next-negotiation &lt;n&gt;
/// deals &lt;count&gt;
/// deal &lt;number&gt; &lt;Item&gt; &lt;Kind&gt; &lt;Status&gt; &lt;firstSeason&gt; &lt;seasons&gt; &lt;price&gt; &lt;exclusive 0|1&gt; &lt;paidSeason&gt;
///   (then lines supplier, customer, signed, ended, engine-name)
/// negotiations &lt;count&gt;
/// negotiation &lt;number&gt; ... (then lines)
/// </code>
/// </summary>
public sealed class SupplySection : IWorldSection
{
    public const string SectionName = "supply";

    private readonly SortedDictionary<long, SupplyDeal> _deals;
    private readonly SortedDictionary<long, SupplyNegotiation> _negotiations;

    private SupplySection(
        long nextDeal,
        long nextNegotiation,
        SortedDictionary<long, SupplyDeal> deals,
        SortedDictionary<long, SupplyNegotiation> negotiations)
    {
        NextDeal = nextDeal;
        NextNegotiation = nextNegotiation;
        _deals = deals;
        _negotiations = negotiations;
    }

    public static SupplySection Empty { get; } = new(1, 1, [], []);

    public string Name => SectionName;

    public int SchemaVersion => 1;

    public long NextDeal { get; }

    public long NextNegotiation { get; }

    public bool IsEmpty => _deals.Count == 0 && _negotiations.Count == 0 && NextDeal == 1 && NextNegotiation == 1;

    public IReadOnlyList<SupplyDeal> Deals => _deals.Values.ToArray();

    public IReadOnlyList<SupplyNegotiation> Negotiations => _negotiations.Values.ToArray();

    public SupplyDeal? FindDeal(string id) => SupplyIds.TryParseDeal(id, out var number) && _deals.TryGetValue(number, out var deal) ? deal : null;

    public SupplyNegotiation? FindNegotiation(string id) =>
        SupplyIds.TryParseNegotiation(id, out var number) && _negotiations.TryGetValue(number, out var negotiation) ? negotiation : null;

    public IReadOnlyList<SupplyDeal> DealsOf(OrganizationId customer) => _deals.Values.Where(deal => deal.Customer == customer).ToArray();

    public IReadOnlyList<SupplyNegotiation> Active() => _negotiations.Values.Where(negotiation => negotiation.IsActive).ToArray();

    /// <summary>The deal that supplies <paramref name="item"/> to <paramref name="customer"/> on <paramref name="date"/>, or null.</summary>
    public SupplyDeal? InForce(OrganizationId customer, SupplyItem item, GameDate date) =>
        _deals.Values.FirstOrDefault(deal => deal.Customer == customer && deal.Item == item && deal.IsInForceOn(date));

    /// <summary>The customers a supplier serves with an item on <paramref name="date"/>, each counted once.</summary>
    public IReadOnlyList<SupplyDeal> ServedBy(OrganizationId supplier, SupplyItem item, GameDate date) =>
        _deals.Values.Where(deal => deal.Supplier == supplier && deal.Item == item && deal.Customer != supplier && deal.IsInForceOn(date)).ToArray();

    public static SupplySection Restore(
        long nextDeal,
        long nextNegotiation,
        IEnumerable<SupplyDeal> deals,
        IEnumerable<SupplyNegotiation> negotiations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nextDeal, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(nextNegotiation, 1);
        ArgumentNullException.ThrowIfNull(deals);
        ArgumentNullException.ThrowIfNull(negotiations);
        var dealMap = new SortedDictionary<long, SupplyDeal>();
        foreach (var deal in deals)
        {
            ArgumentNullException.ThrowIfNull(deal);
            if (deal.Number >= nextDeal || !dealMap.TryAdd(deal.Number, deal))
            {
                throw new InvalidOperationException($"Deal '{deal.Id}' is not below the counter or appears twice.");
            }
        }

        var negotiationMap = new SortedDictionary<long, SupplyNegotiation>();
        foreach (var negotiation in negotiations)
        {
            ArgumentNullException.ThrowIfNull(negotiation);
            if (negotiation.Number >= nextNegotiation || !negotiationMap.TryAdd(negotiation.Number, negotiation))
            {
                throw new InvalidOperationException($"Negotiation '{negotiation.Id}' is not below the counter or appears twice.");
            }

            if (negotiation.SignedDeal is long signed && !dealMap.ContainsKey(signed))
            {
                throw new InvalidOperationException($"Negotiation '{negotiation.Id}' names a deal that does not exist.");
            }
        }

        return new SupplySection(nextDeal, nextNegotiation, dealMap, negotiationMap);
    }

    /// <summary>Adds a deal and gives it the next number. The deal's own number is ignored.</summary>
    public (SupplySection Section, SupplyDeal Deal) AddDeal(SupplyDeal draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var deal = draft with { Number = NextDeal };
        var deals = new SortedDictionary<long, SupplyDeal>(_deals) { [deal.Number] = deal };
        return (new SupplySection(NextDeal + 1, NextNegotiation, deals, _negotiations), deal);
    }

    public SupplySection ReplaceDeal(SupplyDeal deal)
    {
        ArgumentNullException.ThrowIfNull(deal);
        if (!_deals.ContainsKey(deal.Number))
        {
            throw new InvalidOperationException($"Deal '{deal.Id}' is not in the section.");
        }

        var deals = new SortedDictionary<long, SupplyDeal>(_deals) { [deal.Number] = deal };
        return new SupplySection(NextDeal, NextNegotiation, deals, _negotiations);
    }

    public (SupplySection Section, SupplyNegotiation Negotiation) AddNegotiation(SupplyNegotiation draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var negotiation = draft with { Number = NextNegotiation };
        var map = new SortedDictionary<long, SupplyNegotiation>(_negotiations) { [negotiation.Number] = negotiation };
        return (new SupplySection(NextDeal, NextNegotiation + 1, _deals, map), negotiation);
    }

    public SupplySection ReplaceNegotiation(SupplyNegotiation negotiation)
    {
        ArgumentNullException.ThrowIfNull(negotiation);
        if (!_negotiations.ContainsKey(negotiation.Number))
        {
            throw new InvalidOperationException($"Negotiation '{negotiation.Id}' is not in the section.");
        }

        var map = new SortedDictionary<long, SupplyNegotiation>(_negotiations) { [negotiation.Number] = negotiation };
        return new SupplySection(NextDeal, NextNegotiation, _deals, map);
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Number("next-deal", NextDeal);
        writer.Number("next-negotiation", NextNegotiation);
        writer.Count("deals", _deals.Count);
        foreach (var deal in _deals.Values)
        {
            writer.Line(
                "deal " + Num(deal.Number) + " " + deal.Item + " " + deal.Kind + " " + deal.Status + " " + Num(deal.FirstSeason) + " "
                + Num(deal.Terms.Seasons) + " " + Num(deal.AnnualPriceCents) + " " + (deal.Terms.Exclusive ? "1" : "0") + " " + Num(deal.PaidSeason));
            writer.TextLine("supplier", deal.Supplier.Value);
            writer.TextLine("customer", deal.Customer.Value);
            writer.TextLine("signed", deal.Signed.ToString());
            writer.TextLine("ended", deal.EndedOn?.ToString() ?? "-");
            writer.TextLine("engine-name", deal.EngineName ?? "-");
        }

        writer.Count("negotiations", _negotiations.Count);
        foreach (var negotiation in _negotiations.Values)
        {
            writer.Line(
                "negotiation " + Num(negotiation.Number) + " " + negotiation.Item + " " + negotiation.Kind + " " + negotiation.Status + " "
                + Num(negotiation.FirstSeason) + " " + Num(negotiation.MaxRounds) + " " + Num(negotiation.RoundsUsed) + " " + Num(negotiation.Interest));
            writer.TextLine("manager", negotiation.ManagerId);
            writer.TextLine("customer", negotiation.Customer.Value);
            writer.TextLine("supplier", negotiation.Supplier.Value);
            writer.TextLine("opened", negotiation.Opened.ToString());
            writer.TextLine("deadline", negotiation.Deadline.ToString());
            writer.Line("offer " + Terms(negotiation.Offer));
            writer.Line("counter " + (negotiation.Counter is { } counter ? Terms(counter) : "-"));
            writer.TextLine("respond-on", negotiation.RespondOn?.ToString() ?? "-");
            writer.TextLine("closed", negotiation.ClosedOn?.ToString() ?? "-");
            writer.TextLine("signed-deal", negotiation.SignedDeal is long deal ? SupplyIds.Deal(deal) : "-");
            writer.Count("reasons", negotiation.Reasons.Count);
            foreach (var reason in negotiation.Reasons)
            {
                writer.TextLine("reason", reason);
            }
        }
    }

    private static string Terms(SupplyTerms terms) => Num(terms.AnnualPriceCents) + " " + Num(terms.Seasons) + " " + (terms.Exclusive ? "1" : "0");

    private static string Num(long value) => value.ToString(CultureInfo.InvariantCulture);
}
