using Paddock.Application.Access;
using Paddock.Application.Commands;
using Paddock.Application.Finance;
using Paddock.Application.Objectives;
using Paddock.Domain.Objectives;
using Paddock.Domain.Sponsors;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Sponsors;

/// <summary>
/// One cell of the table of terms: what a sponsor would pay a year for this many years and this condition, the condition itself and the bonus for
/// meeting it (#268). <paramref name="Condition"/> is null for a sponsor that asks nothing of the team's results. <paramref name="CapCents"/> is the
/// most waiting can reach in open talks and zero everywhere else. The numbers come from the same functions the commands sign with.
/// </summary>
public sealed record SponsorQuoteView(int Years, string Ambition, long AnnualCents, long CapCents, TranslationMessage? Condition, long BonusCents);

/// <summary>
/// A sponsor's wish for a driver of one nationality (#268). A bonus only. <paramref name="Met"/> is null before a deal exists, otherwise whether
/// the team has such a driver in the right place today.
/// </summary>
public sealed record SponsorWishView(string Nationality, bool RaceSeat, int BonusMilli, bool? Met);

/// <summary>What the sponsor's industry adds: goods with every instalment (<c>inKind</c>) or a sum with the first one (<c>signing</c>), in thousandths of the annual amount.</summary>
public sealed record SponsorIndustryBonusView(string Kind, int Milli);

public sealed record SponsorDealView(
    string Id,
    string SponsorId,
    string SponsorName,
    TranslationMessage Slot,
    TranslationMessage Industry,
    long AnnualCents,
    DateOnly Start,
    DateOnly End,
    int Trust,
    string? ObjectiveId,
    int Years,
    string Ambition,
    SponsorWishView? Wish,
    SponsorIndustryBonusView? IndustryBonus);

/// <summary>
/// One open negotiation. <paramref name="RivalKnown"/> is null unless the negotiator can read the market (INV-003): then it says
/// whether a rival is also talking. The hidden truth is never returned to anyone who cannot see it.
/// </summary>
public sealed record SponsorTalkView(
    string Id,
    string SponsorId,
    string SponsorName,
    TranslationMessage Slot,
    long CurrentAnnualCents,
    long CappedAnnualCents,
    bool? RivalKnown,
    TranslationMessage Note,
    TranslationMessage? Objective,
    int Years,
    string Ambition,
    bool AmbitionOpen,
    IReadOnlyList<SponsorQuoteView> Quotes,
    SponsorWishView? Wish,
    SponsorIndustryBonusView? IndustryBonus);

/// <summary>A renewal the sponsor offers. The player can answer it, or ask other terms while <paramref name="RoundsLeft"/> is above zero.</summary>
public sealed record SponsorOfferView(
    string Id,
    string SponsorName,
    long AnnualCents,
    DateOnly ValidUntil,
    int Years,
    string Ambition,
    bool AmbitionOpen,
    int RoundsLeft,
    long PreviousAnnualCents,
    IReadOnlyList<SponsorQuoteView> Quotes,
    SponsorWishView? Wish,
    SponsorIndustryBonusView? IndustryBonus);

/// <summary>A sponsor that could fill a slot, and the reason it cannot yet, if there is one.</summary>
public sealed record SponsorCandidateView(string SponsorId, string SponsorName, TranslationMessage Industry, long IndicativeAnnualCents, TranslationMessage? Blocked);

/// <summary>
/// One row of the sponsor market (#268): a sponsor the team can approach, in the first free slot it fits, with the table of terms. The market lists
/// each sponsor once, so the player picks a sponsor and the slot follows from it.
/// </summary>
public sealed record SponsorMarketRow(
    string SponsorId,
    string SponsorName,
    TranslationMessage Industry,
    int Slot,
    TranslationMessage Kind,
    long IndicativeAnnualCents,
    TranslationMessage? Blocked,
    bool AmbitionOpen,
    IReadOnlyList<SponsorQuoteView> Quotes,
    SponsorWishView? Wish,
    SponsorIndustryBonusView? IndustryBonus);

public sealed record SponsorSlotView(int Slot, TranslationMessage Kind, string? DealId, string? TalkId, IReadOnlyList<SponsorCandidateView> Candidates);

public abstract record SponsorView
{
    private SponsorView()
    {
    }

    public sealed record Own(
        IReadOnlyList<SponsorSlotView> Slots,
        IReadOnlyList<SponsorDealView> Deals,
        IReadOnlyList<SponsorTalkView> Talks,
        IReadOnlyList<SponsorOfferView> Offers,
        IReadOnlyList<ObjectiveItemView> Objectives,
        IReadOnlyList<SponsorMarketRow> Market) : SponsorView;

    public sealed record Unknown(TranslationMessage Reason) : SponsorView;
}

/// <summary>
/// What a viewer may see of sponsors (INV-003): the organization's own slots, deals, talks, offers and the STATE/WHY/FORECAST
/// of the objectives of its own deals (through <see cref="ObjectiveQuery"/>). Anyone else's organization is
/// <see cref="SponsorView.Unknown"/>. Prices are indicative ESTIMATES. A read does not change state and does not draw a random
/// number (INV-005).
/// </summary>
public static class SponsorQuery
{
    public static SponsorView Read(
        AccessContext access,
        OrganizationId subject,
        SponsorBook book,
        SponsorEnvironment environment,
        ObjectiveQuery objectives,
        GameDate today)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(objectives);
        var allowed = access.Kind == AccessKind.Developer
            || (access.Manager is { } manager && environment.Control.Controls(new Paddock.Application.Managers.ManagerId(manager.Value), subject));
        if (!allowed)
        {
            return new SponsorView.Unknown(TranslationMessage.Of(SponsorKeys.ViewUnknown));
        }

        var section = book.Section;
        var catalog = environment.Catalog;
        var era = environment.Eras.Era(today.Year);
        var skill = environment.Negotiators.Skill(book.World, subject, today);
        var deals = section.ActiveDealsOf(subject);
        var talks = section.OpenTalksOf(subject);
        var typical = environment.Finance.Facts(today.Year).TypicalDollars;
        var shared = CandidateFacts.Collect(section, catalog, environment, subject, today, skill, deals, talks, typical);

        var playerTeam = environment.IsPlayerTeam(subject);
        var slots = new List<SponsorSlotView>();
        var market = new List<SponsorMarketRow>();

        // A sponsor the team is already talking to or under contract with is not on the market again.
        var listed = new HashSet<string>(deals.Select(deal => deal.SponsorId).Concat(talks.Select(talk => talk.SponsorId)), StringComparer.Ordinal);
        for (var slot = 1; slot <= SponsorEstimates.SlotsPerTeam; slot++)
        {
            var kind = era.Slots[slot - 1];
            var deal = deals.FirstOrDefault(candidate => candidate.Slot == slot && candidate.IsActiveOn(today));
            var talk = talks.FirstOrDefault(candidate => candidate.Slot == slot);
            var candidates = deal is not null || talk is not null
                ? []
                : shared.List(book, catalog, era, kind, slot, today, subject, playerTeam);
            slots.Add(new SponsorSlotView(slot, TranslationMessage.Of(SponsorKeys.SlotName(kind)), deal?.Id, talk?.Id, candidates));
            if (deal is not null || talk is not null)
            {
                continue;
            }

            foreach (var candidate in candidates)
            {
                if (listed.Add(candidate.SponsorId) && catalog.Find(candidate.SponsorId) is { } sponsor)
                {
                    market.Add(MarketRow(book, environment, subject, sponsor, candidate, slot, kind, today));
                }
            }
        }

        var dealViews = deals.Select(deal =>
        {
            var sponsor = catalog.Find(deal.SponsorId);
            return new SponsorDealView(
                deal.Id,
                deal.SponsorId,
                sponsor?.Name ?? deal.SponsorId,
                TranslationMessage.Of(SponsorKeys.SlotName(deal.Kind)),
                TranslationMessage.Of(SponsorKeys.IndustryName(sponsor?.Industry ?? SponsorIndustries.Patron)),
                deal.AnnualCents,
                Day(deal.Start),
                Day(deal.End),
                section.TrustOf(deal.SponsorId, subject),
                deal.ObjectiveId,
                deal.Years,
                SponsorAmbitions.KeyOf(deal.Ambition),
                deal.WishNationality is { } nationality
                    ? new SponsorWishView(
                        nationality,
                        deal.WishRaceSeat,
                        SponsorEstimates.WishBonusMilli,
                        SponsorWishes.Satisfied(book.World, subject, new SponsorWish(nationality, deal.WishRaceSeat), today))
                    : null,
                IndustryBonusOf(sponsor));
        }).ToArray();

        var insight = skill >= SponsorEstimates.RivalInsightSkill;
        var talkViews = talks.Select(talk =>
        {
            var sponsor = catalog.Find(talk.SponsorId);
            return new SponsorTalkView(
                talk.Id,
                talk.SponsorId,
                sponsor?.Name ?? talk.SponsorId,
                TranslationMessage.Of(SponsorKeys.SlotName(talk.Kind)),
                talk.AnnualCentsOn(today),
                talk.CappedAnnualCents,
                insight && talk.Rival != RivalState.Undecided ? talk.Rival == RivalState.Present : null,
                TranslationMessage.Of(insight && talk.Rival == RivalState.Present ? SponsorKeys.ViewRivalKnown : SponsorKeys.ViewTermsEstimate),
                ObjectiveOf(sponsor, talk.Organization, environment, today, talk.Ambition),
                talk.Years,
                SponsorAmbitions.KeyOf(talk.Ambition),
                sponsor is not null && SponsorRules.AmbitionOpen(sponsor),
                sponsor is null ? [] : Quotes(sponsor, subject, environment, today, terms => talk.AnnualCentsOn(today, terms), terms => talk.CappedAnnualCentsFor(terms)),
                sponsor is null ? null : WishView(book, sponsor, today),
                IndustryBonusOf(sponsor));
        }).ToArray();

        var offerViews = section.OpenOffersOf(subject).Select(offer =>
        {
            var sponsor = catalog.Find(offer.SponsorId);
            var deal = section.FindDeal(SponsorsSection.DealIdOf(offer.DealNumber));
            return new SponsorOfferView(
                offer.Id,
                sponsor?.Name ?? offer.SponsorId,
                offer.AnnualCents,
                Day(offer.ValidUntil),
                offer.Years,
                SponsorAmbitions.KeyOf(offer.Ambition),
                sponsor is not null && SponsorRules.AmbitionOpen(sponsor),
                Math.Max(0, SponsorEstimates.MaxCounterRounds - offer.Rounds),
                deal?.AnnualCents ?? 0,
                sponsor is null || deal is null
                    ? []
                    : Quotes(sponsor, subject, environment, today, terms => SponsorRules.RenewalCents(book, environment, deal, sponsor, terms), null),
                sponsor is null ? null : WishView(book, sponsor, today),
                IndustryBonusOf(sponsor));
        }).ToArray();

        var objectiveIds = deals.Where(deal => deal.ObjectiveId is not null).Select(deal => deal.ObjectiveId!).ToHashSet(StringComparer.Ordinal);
        var items = objectiveIds.Count == 0
            ? []
            : objectives.View(access, book.Objectives, today).Items.Where(item => objectiveIds.Contains(item.Id)).ToArray();
        return new SponsorView.Own(slots, dealViews, talkViews, offerViews, items, market);
    }

    private static SponsorMarketRow MarketRow(
        SponsorBook book,
        SponsorEnvironment environment,
        OrganizationId subject,
        SponsorDefinition sponsor,
        SponsorCandidateView candidate,
        int slot,
        SlotKind kind,
        GameDate today) =>
        new(
            sponsor.Id,
            sponsor.Name,
            candidate.Industry,
            slot,
            TranslationMessage.Of(SponsorKeys.SlotName(kind)),
            candidate.IndicativeAnnualCents,
            candidate.Blocked,
            SponsorRules.AmbitionOpen(sponsor),
            Quotes(sponsor, subject, environment, today, terms => terms.AnnualCents(candidate.IndicativeAnnualCents), null),
            WishView(book, sponsor, today),
            IndustryBonusOf(sponsor));

    /// <summary>
    /// The table of terms of one sponsor: every length from one to three years, and every ambition when the sponsor has a condition to change.
    /// <paramref name="annual"/> says what a year pays on given terms; the condition and its bonus come from the rules the deal is signed with.
    /// </summary>
    private static List<SponsorQuoteView> Quotes(
        SponsorDefinition sponsor,
        OrganizationId subject,
        SponsorEnvironment environment,
        GameDate today,
        Func<SponsorTerms, long> annual,
        Func<SponsorTerms, long>? cap)
    {
        var ambitions = SponsorRules.AmbitionOpen(sponsor) ? SponsorAmbitions.All : [SponsorAmbition.Standard];
        var quotes = new List<SponsorQuoteView>();
        for (var years = SponsorEstimates.MinYears; years <= SponsorEstimates.MaxYears; years++)
        {
            foreach (var ambition in ambitions)
            {
                var terms = new SponsorTerms(years, ambition);
                var cents = annual(terms);
                quotes.Add(new SponsorQuoteView(
                    years,
                    SponsorAmbitions.KeyOf(ambition),
                    cents,
                    cap?.Invoke(terms) ?? 0L,
                    ObjectiveOf(sponsor, subject, environment, today, ambition),
                    BonusCents(sponsor, subject, environment, today, ambition, cents)));
            }
        }

        return quotes;
    }

    private static SponsorWishView? WishView(SponsorBook book, SponsorDefinition sponsor, GameDate today) =>
        SponsorWishes.Offered(book.World, sponsor, today) is { } wish
            ? new SponsorWishView(wish.Nationality, wish.RaceSeat, SponsorEstimates.WishBonusMilli, null)
            : null;

    private static SponsorIndustryBonusView? IndustryBonusOf(SponsorDefinition? sponsor) =>
        sponsor is not null && SponsorIndustryBonus.KindOf(sponsor.Industry) is var kind && kind != IndustryBonusKind.None
            ? new SponsorIndustryBonusView(SponsorIndustryBonus.KeyOf(kind), SponsorIndustryBonus.Milli(sponsor.Industry))
            : null;

    /// <summary>The bonus in cents for meeting the condition of this ambition when a year pays <paramref name="annualCents"/>.</summary>
    private static long BonusCents(SponsorDefinition sponsor, OrganizationId organization, SponsorEnvironment environment, GameDate today, SponsorAmbition ambition, long annualCents)
    {
        if (!SponsorRules.AmbitionOpen(sponsor) || sponsor.Objective is not { } spec)
        {
            return 0;
        }

        var scaled = SponsorRules.ForTeam(spec, organization, environment, today, ambition);
        return annualCents * SponsorObjectiveScale.Reward(spec, scaled).BonusMilli / 1000;
    }

    private static DateOnly Day(GameDate date) => new(date.Year, date.Month, date.Day);

    /// <summary>The scaled target the player sees before signing. The same function <see cref="SponsorRules.Sign"/> uses.</summary>
    private static TranslationMessage? ObjectiveOf(
        SponsorDefinition? sponsor,
        OrganizationId organization,
        SponsorEnvironment environment,
        GameDate today,
        SponsorAmbition ambition = SponsorAmbition.Standard)
    {
        if (sponsor is null || !SponsorRules.AmbitionOpen(sponsor) || sponsor.Objective is not { } spec)
        {
            return null;
        }

        var scaled = SponsorRules.ForTeam(spec, organization, environment, today, ambition);
        var predicate = SponsorRules.PredicateOf(scaled);
        var (key, parameter) = ObjectiveKeys.PredicateText(predicate);
        var value = predicate is NumericPredicate numeric
            ? numeric.Target.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : scaled.Value;
        return TranslationMessage.Of(key, (parameter, value));
    }

    /// <summary>
    /// The facts <see cref="SponsorRules.CanBegin"/> rereads for every sponsor. Collected once per view, in the same
    /// order CanBegin reports them, so a slot lists the same blocked reason it did when each sponsor was checked alone.
    /// </summary>
    private sealed class CandidateFacts
    {
        private readonly SponsorsSection _section;
        private readonly HashSet<string> _inDeal;
        private readonly HashSet<string> _industries;
        private readonly double _prestige;
        private readonly bool _tooMany;
        private readonly long _typicalDollars;
        private readonly IReadOnlyList<SponsorDeal> _deals;
        private readonly IReadOnlyList<SponsorTalk> _talks;

        private CandidateFacts(
            SponsorsSection section,
            HashSet<string> inDeal,
            HashSet<string> industries,
            double prestige,
            bool tooMany,
            long typicalDollars,
            IReadOnlyList<SponsorDeal> deals,
            IReadOnlyList<SponsorTalk> talks)
        {
            _section = section;
            _inDeal = inDeal;
            _industries = industries;
            _prestige = prestige;
            _tooMany = tooMany;
            _typicalDollars = typicalDollars;
            _deals = deals;
            _talks = talks;
        }

        public static CandidateFacts Collect(
            SponsorsSection section,
            SponsorCatalog catalog,
            SponsorEnvironment environment,
            OrganizationId subject,
            GameDate today,
            int skill,
            IReadOnlyList<SponsorDeal> deals,
            IReadOnlyList<SponsorTalk> talks,
            long typicalDollars)
        {
            var inDeal = new HashSet<string>(StringComparer.Ordinal);
            foreach (var deal in section.Deals)
            {
                if (deal.IsActive)
                {
                    inDeal.Add(deal.SponsorId);
                }
            }

            var industries = new HashSet<string>(StringComparer.Ordinal);
            foreach (var deal in deals)
            {
                AddIndustry(catalog, industries, deal.SponsorId);
            }

            foreach (var talk in talks)
            {
                AddIndustry(catalog, industries, talk.SponsorId);
            }

            return new CandidateFacts(
                section,
                inDeal,
                industries,
                environment.Appeal.Appeal(subject, today).Prestige,
                talks.Count >= SponsorPricing.ParallelTalks(skill),
                typicalDollars,
                deals,
                talks);
        }

        public SponsorCandidateView[] List(
            SponsorBook book,
            SponsorCatalog catalog,
            SponsorEra era,
            SlotKind kind,
            int slot,
            GameDate today,
            OrganizationId subject,
            bool playerTeam)
        {
            var slotBusy = SlotBusy(slot);
            return catalog.CandidatesFor(today.Year, kind, era, subject, playerTeam)
                .Select(sponsor => new SponsorCandidateView(
                    sponsor.Id,
                    sponsor.Name,
                    TranslationMessage.Of(SponsorKeys.IndustryName(sponsor.Industry)),
                    SponsorPricing.FullAnnualCents(sponsor, kind, _typicalDollars, book.Finance.PopularityMilli),
                    Blocked(sponsor, today, slotBusy)))
                .ToArray();
        }

        private bool SlotBusy(int slot)
        {
            foreach (var deal in _deals)
            {
                if (deal.Slot == slot)
                {
                    return true;
                }
            }

            foreach (var talk in _talks)
            {
                if (talk.Slot == slot)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The first reason CanBegin would return. Kind and era were already applied by <see cref="SponsorCatalog.Candidates"/>.</summary>
        private TranslationMessage? Blocked(SponsorDefinition sponsor, GameDate today, bool slotBusy)
        {
            if (slotBusy)
            {
                return TranslationMessage.Of(SponsorKeys.SlotBusy);
            }

            if (_industries.Contains(sponsor.Industry))
            {
                return TranslationMessage.Of(SponsorKeys.IndustryConflict);
            }

            if (_prestige < sponsor.PrestigeNeed)
            {
                return TranslationMessage.Of(SponsorKeys.PrestigeTooLow);
            }

            if (_tooMany)
            {
                return TranslationMessage.Of(SponsorKeys.TooManyTalks);
            }

            return null;
        }

        private static void AddIndustry(SponsorCatalog catalog, HashSet<string> industries, string sponsorId)
        {
            var industry = catalog.Find(sponsorId)?.Industry;
            if (industry is not null)
            {
                industries.Add(industry);
            }
        }
    }
}
