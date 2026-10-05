using System.Globalization;

namespace Paddock.Simulation.Ai;

/// <summary>A supplier's public offer for one kind of deal: its list price and how well its customers do.</summary>
/// <param name="SupplierId">The supplier organization.</param>
/// <param name="Kind">The kind of deal: <c>Partner</c>, <c>Customer</c> or <c>LastYearEngine</c>.</param>
/// <param name="ListCents">The yearly list price of a one-season deal, in cents.</param>
/// <param name="LagSeasons">How many seasons behind the supplier's newest product this kind of deal is.</param>
/// <param name="FormScore">How well the supplier's customers do in the standings, from -1 (bottom) to 1 (top), or null when nobody knows.</param>
public sealed record SupplyQuote(string SupplierId, string Kind, long ListCents, int LagSeasons, double? FormScore);

/// <summary>Something the team has to supply for a season: it holds no deal that covers it.</summary>
/// <param name="Item">What (<c>Engine</c>).</param>
/// <param name="FirstSeason">The season the deal would start.</param>
/// <param name="CurrentSupplier">The supplier of the deal that is ending, or null.</param>
/// <param name="Quotes">The offers on the market.</param>
public sealed record SupplyNeed(string Item, int FirstSeason, string? CurrentSupplier, IReadOnlyList<SupplyQuote> Quotes);

/// <summary>Where one supply talk stands.</summary>
public enum SupplyTalkState
{
    Awaiting = 0,
    Countered = 1,
}

/// <summary>One of the team's supply talks.</summary>
public sealed record SupplyTalk(
    string NegotiationId,
    string SupplierId,
    string Item,
    string Kind,
    int FirstSeason,
    SupplyTalkState State,
    long OfferCents,
    long CounterCents,
    int CounterSeasons,
    int RoundsLeft,
    int LagSeasons,
    double? FormScore,
    string? CurrentSupplier);

/// <summary>What the supply decider reads.</summary>
/// <param name="Today">The day.</param>
/// <param name="AnnualBudgetCents">The era's typical annual budget in cents (the yardstick).</param>
/// <param name="HeadroomCents">Money available for new commitments in the rest of the season, in cents.</param>
/// <param name="MultiYearDiscountMilli">The discount per extra season of a long deal, in thousandths (public price rule).</param>
/// <param name="MultiYearDiscountCapMilli">The most that discount can reach.</param>
/// <param name="Needs">What is missing.</param>
/// <param name="Talks">The open talks.</param>
public sealed record SupplyInput(
    DateOnly Today,
    long AnnualBudgetCents,
    long HeadroomCents,
    int MultiYearDiscountMilli,
    int MultiYearDiscountCapMilli,
    IReadOnlyList<SupplyNeed> Needs,
    IReadOnlyList<SupplyTalk> Talks);

/// <summary>What a supply review asks the host to do.</summary>
public abstract record SupplyAction
{
    private SupplyAction()
    {
    }

    /// <summary>Propose a deal, or revise the offer of the named talk when <see cref="NegotiationId"/> is not empty.</summary>
    public sealed record Propose(string SupplierId, string Item, string Kind, int FirstSeason, long PriceCents, int Seasons, string NegotiationId) : SupplyAction;

    /// <summary>Accept the supplier's counter, or turn it down.</summary>
    public sealed record Respond(string NegotiationId, bool Accept) : SupplyAction;
}

/// <summary>
/// Supply deals for engines (DESIGN section 5.4, T43): which supplier to approach for the season to come and whether to accept a counter.
/// It weighs the supplier's public list price, the form of its customers (public results) and the cost of changing supplier, with the
/// archetype's weights. It does not read the supplier's own numbers or the true quality of its engine (INV-003).
/// </summary>
public static class SupplyDecider
{
    public const string ChooseKind = "supply.choose";

    public const string CounterKind = "supply.counter";

    /// <summary>ESTIMATE: the share above its price floor a negotiator of each tier opens at (index 0 is tier 1). Tier 4 opens at the floor.</summary>
    public static readonly IReadOnlyList<double> OpeningPremiumByNegotiation = [0.10, 0.05, 0.02, 0.0];

    /// <summary>ESTIMATE: the utility of going without the item altogether (the car cannot race without an engine).</summary>
    public const double NoDealUtility = -2.0;

    /// <summary>ESTIMATE: what changing supplier costs (retooling, a new relationship), before the archetype's stability weight.</summary>
    public const double SwitchCost = 0.25;

    public static IReadOnlyList<SupplyAction> Review(DecisionContext context, SupplyInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);
        var actions = new List<SupplyAction>();
        var talking = new HashSet<string>(StringComparer.Ordinal);
        foreach (var talk in input.Talks.OrderBy(item => item.NegotiationId, StringComparer.Ordinal))
        {
            talking.Add(talk.Item);
            if (talk.State == SupplyTalkState.Countered)
            {
                Counter(context, input, talk, actions);
            }
        }

        foreach (var need in input.Needs.OrderBy(item => item.Item, StringComparer.Ordinal))
        {
            if (!talking.Contains(need.Item))
            {
                Choose(context, input, need, actions);
            }
        }

        return actions;
    }

    private static List<FactorDraft> Terms(DecisionContext context, SupplyInput input, SupplyQuote quote, long priceCents, string? currentSupplier)
    {
        var profile = context.Profile;
        var factors = new List<FactorDraft>
        {
            new(AiTextKeys.FactorSupplierForm, profile.Now * AiEstimates.SupplyFormScale * (quote.FormScore ?? 0.0)),
            new(AiTextKeys.FactorSupplyLag, -profile.Now * 0.1 * quote.LagSeasons),
            new(AiTextKeys.FactorSupplyPrice, -profile.Thrift * AiEstimates.SupplyPriceScale * priceCents / Math.Max(1L, input.AnnualBudgetCents)),
        };
        if (currentSupplier is not null && string.Equals(currentSupplier, quote.SupplierId, StringComparison.Ordinal))
        {
            factors.Add(new FactorDraft(AiTextKeys.FactorContinuity, profile.Stability * SwitchCost));
        }

        return factors;
    }

    /// <summary>The price the principal opens at: the list price less the long-deal discount (a public rule), plus its tier's premium.</summary>
    internal static long OpeningCents(DecisionContext context, SupplyInput input, long listCents, int seasons)
    {
        var discount = Math.Min(input.MultiYearDiscountCapMilli, (seasons - 1) * input.MultiYearDiscountMilli);
        var floor = listCents * (1000 - discount) / 1000.0;
        var premium = OpeningPremiumByNegotiation[context.Skills.For(DecisionFacet.Supply) - 1];
        return Math.Max(1L, (long)Math.Ceiling(floor * (1.0 + premium)));
    }

    private static void Choose(DecisionContext context, SupplyInput input, SupplyNeed need, List<SupplyAction> actions)
    {
        var seasons = context.Level >= AiEstimates.PlanningLevel ? context.Profile.SupplySeasons : 1;
        var options = new List<OptionDraft>();
        var offers = new Dictionary<string, (SupplyQuote Quote, long Price)>(StringComparer.Ordinal);
        foreach (var quote in need.Quotes.OrderBy(item => item.SupplierId, StringComparer.Ordinal).ThenBy(item => item.Kind, StringComparer.Ordinal))
        {
            var price = OpeningCents(context, input, quote.ListCents, seasons);
            if (price > input.HeadroomCents)
            {
                continue;
            }

            var id = "supplier/" + quote.SupplierId + "/" + quote.Kind;
            offers[id] = (quote, price);
            options.Add(new OptionDraft(id, Terms(context, input, quote, price, need.CurrentSupplier)));
        }

        options.Add(new OptionDraft(AiTextKeys.OptionNoDeal, [new FactorDraft(AiTextKeys.FactorNoDeal, NoDealUtility)]));
        var note = new TraceNote(
            ChooseKind,
            need.Item + "/" + need.FirstSeason.ToString(CultureInfo.InvariantCulture),
            need.Item + "-" + need.FirstSeason.ToString(CultureInfo.InvariantCulture),
            "Looks for a supplier of " + need.Item + " from " + need.FirstSeason.ToString(CultureInfo.InvariantCulture) + ".",
            null,
            IsKeyDecision: true);
        var chosen = UtilityChooser.Choose(context, DecisionFacet.Supply, note, options, id => id == AiTextKeys.OptionNoDeal ? AiTextKeys.ReasonNoSupplier : AiTextKeys.ReasonSupplier);
        if (offers.TryGetValue(chosen.Id, out var picked))
        {
            actions.Add(new SupplyAction.Propose(picked.Quote.SupplierId, need.Item, picked.Quote.Kind, need.FirstSeason, picked.Price, seasons, string.Empty));
        }
    }

    private static void Counter(DecisionContext context, SupplyInput input, SupplyTalk talk, List<SupplyAction> actions)
    {
        var quote = new SupplyQuote(talk.SupplierId, talk.Kind, talk.CounterCents, talk.LagSeasons, talk.FormScore);
        var options = new List<OptionDraft>();
        if (talk.CounterCents <= input.HeadroomCents)
        {
            var terms = Terms(context, input, quote, talk.CounterCents, talk.CurrentSupplier);
            terms.Add(new FactorDraft(AiTextKeys.FactorSupplyNeed, 0.5));
            options.Add(new OptionDraft(AiTextKeys.OptionAccept, terms));
        }

        options.Add(new OptionDraft(AiTextKeys.OptionDecline, [new FactorDraft(AiTextKeys.FactorNoDeal, NoDealUtility + 1.0)]));
        var note = new TraceNote(
            CounterKind,
            talk.NegotiationId,
            talk.NegotiationId,
            "Weighs the supplier's counter of " + talk.CounterCents.ToString(CultureInfo.InvariantCulture) + " cents.",
            null,
            IsKeyDecision: true);
        var chosen = UtilityChooser.Choose(context, DecisionFacet.Supply, note, options, id => id == AiTextKeys.OptionAccept ? AiTextKeys.ReasonAccepted : AiTextKeys.ReasonDeclined);
        actions.Add(new SupplyAction.Respond(talk.NegotiationId, chosen.Id == AiTextKeys.OptionAccept));
    }
}
