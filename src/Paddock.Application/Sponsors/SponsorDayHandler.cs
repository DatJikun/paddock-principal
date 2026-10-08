using System.Globalization;
using Paddock.Application.Finance;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Finance;
using Paddock.Domain.Inbox;
using Paddock.Domain.Objectives;
using Paddock.Domain.Random;
using Paddock.Domain.Sponsors;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Time;

namespace Paddock.Application.Sponsors;

/// <summary>
/// Posts sponsor notices to the human managers who run an organization. A notice is not blocking, but a renewal offer is a decision
/// with two options. An AI manager gets nothing: it answers through commands and has nobody to read an inbox, so its items
/// would only pile up (#254).
/// </summary>
internal sealed class SponsorNotices
{
    private readonly SponsorEnvironment _environment;
    private readonly InboxBook? _inbox;
    private readonly ManagerRegistry? _managers;

    public SponsorNotices(SponsorEnvironment environment, InboxBook? inbox, ManagerRegistry? managers)
    {
        _environment = environment;
        _inbox = inbox;
        _managers = managers;
    }

    public void Post(OrganizationId organization, string subjectKey, GameDate today, params (string Name, string Value)[] arguments)
    {
        if (_inbox is null || _managers is null)
        {
            return;
        }

        foreach (var manager in Humans(organization))
        {
            var draft = new InboxItemDraft(
                SponsorKeys.InboxKind,
                subjectKey,
                arguments.Select(argument => new KeyValuePair<string, string>(argument.Name, argument.Value)),
                null,
                null,
                null);
            _inbox.Post(_managers, manager, draft, today);
        }
    }

    /// <summary>A renewal offer as a decision: accept, or let the sponsor go. Unanswered by the end of the deal it is declined.</summary>
    public void PostOffer(OrganizationId organization, GameDate today, GameDate until, SponsorOffer offer, string sponsorName)
    {
        if (_inbox is null || _managers is null)
        {
            return;
        }

        var arguments = new KeyValuePair<string, string>[]
        {
            new("sponsor", sponsorName),
            new("amount", Dollars(offer.AnnualCents)),
            new(SponsorOfferCodes.OfferArgument, offer.Id),
            new("until", until.ToString()),
        };
        foreach (var manager in Humans(organization))
        {
            var draft = new InboxItemDraft(
                SponsorOfferCodes.OfferKind,
                SponsorKeys.OfferSubject(offer.Years),
                arguments,
                [
                    new InboxOption(SponsorOfferCodes.OptionAccept, SponsorKeys.OfferAcceptLabel, SponsorKeys.OfferAcceptConsequence),
                    new InboxOption(SponsorOfferCodes.OptionDecline, SponsorKeys.OfferDeclineLabel, SponsorKeys.OfferDeclineConsequence),
                ],
                until,
                SponsorOfferCodes.OptionDecline);
            _inbox.Post(_managers, manager, draft, today);
        }
    }

    private IEnumerable<ManagerId> Humans(OrganizationId organization) =>
        _environment.Control.ManagersOf(organization).Where(manager => _managers!.KindOf(manager) == ManagerKind.Human);

    public string NameOf(string sponsorId) => _environment.Catalog.Find(sponsorId)?.Name ?? sponsorId;

    public static string Dollars(long cents) => new Money(cents).WholeDollars.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Sponsors for one lived day, in this order:
/// <list type="number">
/// <item>Open talks. The first day, a hidden roll decides whether a rival team is also talking; after that a talking rival
/// may sign the sponsor, ending the talks (the waiting game). A sponsor who signed with anyone else ends the talks too.</item>
/// <item>Instalments of active deals, posted to the finance ledger: one on the 15th of each month, and all that are still
/// owed on the last day of the deal.</item>
/// <item>Deals past their end date complete, and unanswered renewal offers lapse.</item>
/// <item>Renewal offers: shortly before a deal ends a sponsor whose trust is high enough offers a renewal.</item>
/// </list>
/// Every random draw comes from a child of the <c>Market</c> stream keyed by organization, sponsor, slot and opening date, never
/// by a counter, so an unrelated deal elsewhere cannot shift this organization's rolls (INV-004). It posts only through
/// <see cref="FinanceSection.Post"/> and tells the managers through the inbox.
/// </summary>
public sealed class SponsorDayHandler : IDayHandler
{
    /// <summary>ESTIMATE: after contracts (700) so a new contract is seen, before finance (800) so its review sees the money.</summary>
    public const int DefaultOrder = 750;

    private readonly SponsorBook _book;
    private readonly SponsorEnvironment _environment;
    private readonly SponsorNotices _notices;

    public SponsorDayHandler(
        SponsorBook book,
        SponsorEnvironment environment,
        InboxBook? inbox = null,
        ManagerRegistry? managers = null,
        int order = DefaultOrder)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
        _notices = new SponsorNotices(environment, inbox, managers);
        Order = order;
    }

    public int Order { get; }

    public void OnDay(DayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var today = context.Today;
        var sponsors = _book.Section;
        if (sponsors.Talks.Count == 0 && sponsors.Deals.Count == 0 && sponsors.Offers.Count == 0)
        {
            return;
        }

        var finance = _book.Finance;
        var objectives = _book.Objectives;
        var market = new Lazy<RngStream>(() => new RngStream(RngStreamName.Market, context.Stream(RngStreamName.Market).State));
        var before = sponsors;
        var financeBefore = finance;
        var objectivesBefore = objectives;

        sponsors = RunTalks(sponsors, market, today);
        (sponsors, finance) = PostInstalments(sponsors, finance, today);
        (sponsors, objectives) = RollYears(sponsors, objectives, today);
        sponsors = Expire(sponsors, today);
        sponsors = OfferRenewals(sponsors, today);

        if (!ReferenceEquals(before, sponsors) || !ReferenceEquals(financeBefore, finance) || !ReferenceEquals(objectivesBefore, objectives))
        {
            _book.Write(
                sponsors,
                ReferenceEquals(objectivesBefore, objectives) ? null : objectives,
                ReferenceEquals(financeBefore, finance) ? null : finance);
        }
    }

    private SponsorsSection RunTalks(SponsorsSection sponsors, Lazy<RngStream> market, GameDate today)
    {
        foreach (var talk in sponsors.Talks.Where(talk => talk.IsOpen))
        {
            var current = talk;
            if (current.Rival == RivalState.Undecided)
            {
                var roll = market.Value.DeriveChild(Tag("rival-presence", current, today: null)).NextDouble();
                current = current with { Rival = roll < SponsorEstimates.RivalPresenceChance ? RivalState.Present : RivalState.Absent };
                sponsors = sponsors.Replace(current);
            }

            if (current.Rival == RivalState.Present && current.Opened < today)
            {
                var roll = market.Value.DeriveChild(Tag("rival-sign", current, today)).NextDouble();
                if (roll < SponsorEstimates.RivalSignChance)
                {
                    sponsors = Lose(sponsors, current, today, take: true);
                }
            }
        }

        return sponsors;
    }

    private SponsorsSection Lose(SponsorsSection sponsors, SponsorTalk talk, GameDate today, bool take)
    {
        sponsors = sponsors.Replace(talk with { Status = TalkStatus.LostToRival, ClosedOn = today });
        if (take)
        {
            sponsors = sponsors.Take(talk.SponsorId, today.AddDays(SponsorEstimates.RivalDealDays));
        }

        _notices.Post(talk.Organization, SponsorKeys.InboxLostSubject, today, ("sponsor", _notices.NameOf(talk.SponsorId)));
        return sponsors;
    }

    private (SponsorsSection Sponsors, FinanceSection Finance) PostInstalments(SponsorsSection sponsors, FinanceSection finance, GameDate today)
    {
        foreach (var deal in sponsors.Deals.Where(deal => deal.IsActiveOn(today)))
        {
            var total = deal.InstalmentsInAll;
            if (deal.InstalmentsPaid >= total || !finance.HasBook(deal.Organization))
            {
                continue;
            }

            var owed = today == deal.End
                ? total - deal.InstalmentsPaid
                : today.Day == SponsorEstimates.InstalmentDayOfMonth ? 1 : 0;
            var industry = _environment.Catalog.Find(deal.SponsorId)?.Industry;
            var industryMilli = industry is null ? 0 : SponsorIndustryBonus.Milli(industry);
            var industryKind = industry is null ? IndustryBonusKind.None : SponsorIndustryBonus.KindOf(industry);
            var wished = deal.WishNationality is { } nationality
                && SponsorWishes.Satisfied(_book.World, deal.Organization, new SponsorWish(nationality, deal.WishRaceSeat), today);
            var paid = deal.InstalmentsPaid;
            for (var step = 0; step < owed; step++)
            {
                paid++;
                var number = ((paid - 1) % SponsorEstimates.InstalmentsPerYear) + 1;
                var cents = SponsorPricing.InstalmentCents(deal.AnnualCents, number);
                if (cents > 0)
                {
                    finance = finance.Post(deal.Organization, today, LedgerCategories.Sponsor, deal.SponsorId, cents, SponsorReason.Instalment);
                }

                if (industryKind == IndustryBonusKind.InKind)
                {
                    finance = PostExtra(finance, deal, today, SponsorPricing.InstalmentCents(deal.AnnualCents * industryMilli / 1000, number), SponsorReason.InKind);
                }
                else if (industryKind == IndustryBonusKind.Signing && paid == 1)
                {
                    finance = PostExtra(finance, deal, today, deal.AnnualCents * industryMilli / 1000, SponsorReason.Signing);
                }

                if (wished)
                {
                    finance = PostExtra(
                        finance,
                        deal,
                        today,
                        SponsorPricing.InstalmentCents(deal.AnnualCents * SponsorEstimates.WishBonusMilli / 1000, number),
                        SponsorReason.Nationality);
                }
            }

            if (paid != deal.InstalmentsPaid)
            {
                sponsors = sponsors.Replace(deal with { InstalmentsPaid = paid });
            }
        }

        return (sponsors, finance);
    }

    private static FinanceSection PostExtra(FinanceSection finance, SponsorDeal deal, GameDate today, long cents, string reason) =>
        cents > 0 ? finance.Post(deal.Organization, today, LedgerCategories.Sponsor, deal.SponsorId, cents, reason) : finance;

    /// <summary>
    /// A deal of more than one year gets a new condition on each anniversary: the sponsor looks at the team as it is then, and the player's
    /// chosen ambition applies again (#268). The result of the year that ended was settled by its own deadline, so the slate is clean.
    /// A sponsor that is open to a long partnership first raises the annual amount when the year's condition was met, and tells the manager.
    /// </summary>
    private (SponsorsSection Sponsors, ObjectivesSection Objectives) RollYears(SponsorsSection sponsors, ObjectivesSection objectives, GameDate today)
    {
        foreach (var deal in sponsors.Deals.Where(deal => deal.IsActive && deal.Years > 1))
        {
            var anniversary = false;
            for (var year = 1; year < deal.Years; year++)
            {
                anniversary |= deal.YearStart(year) == today;
            }

            if (!anniversary || _environment.Catalog.Find(deal.SponsorId) is not { } sponsor)
            {
                continue;
            }

            // A satisfied sponsor raises the amount for the year that starts today, and says so (#268, owner decision).
            var updated = deal;
            var raise = SponsorRules.AnniversaryRaiseMilli(sponsors, sponsor, deal);
            if (raise > 0)
            {
                updated = deal with { AnnualCents = SponsorPartnership.Raised(deal.AnnualCents, raise) };
                _notices.Post(
                    deal.Organization,
                    SponsorKeys.InboxRaisedSubject,
                    today,
                    ("sponsor", sponsor.Name),
                    ("amount", SponsorNotices.Dollars(updated.AnnualCents)),
                    ("percent", (raise / 10.0).ToString("0.#", CultureInfo.InvariantCulture)));
            }

            if (SponsorRules.AmbitionOpen(sponsor))
            {
                var (granted, objectiveId) = SponsorRules.GrantObjective(objectives, _environment, sponsor, deal.Organization, updated.AnnualCents, today, deal.Ambition);
                objectives = granted;
                updated = updated with { ObjectiveId = objectiveId, Outcome = DealObjectiveOutcome.None };
            }

            if (!ReferenceEquals(updated, deal))
            {
                sponsors = sponsors.Replace(updated);
            }
        }

        return (sponsors, objectives);
    }

    private SponsorsSection Expire(SponsorsSection sponsors, GameDate today)
    {
        foreach (var deal in sponsors.Deals.Where(deal => deal.IsActive && deal.End < today))
        {
            sponsors = sponsors.Replace(deal with { Status = DealStatus.Completed, EndedOn = today });
            sponsors = sponsors.WithTrust(deal.SponsorId, deal.Organization, sponsors.TrustOf(deal.SponsorId, deal.Organization) + SponsorEstimates.TrustOnCompleted);
            _notices.Post(deal.Organization, SponsorKeys.InboxCompletedSubject, today, ("sponsor", _notices.NameOf(deal.SponsorId)));
        }

        foreach (var offer in sponsors.Offers.Where(offer => offer.IsOpen && offer.ValidUntil < today))
        {
            sponsors = sponsors.Replace(offer with { Status = OfferStatus.Lapsed, ClosedOn = today });
        }

        return sponsors;
    }

    private SponsorsSection OfferRenewals(SponsorsSection sponsors, GameDate today)
    {
        foreach (var deal in sponsors.Deals.Where(deal => deal.IsActive))
        {
            var left = today.DaysUntil(deal.End);
            if (left < 0 || left > SponsorEstimates.RenewalLeadDays || sponsors.Offers.Any(offer => offer.DealNumber == deal.Number))
            {
                continue;
            }

            var sponsor = _environment.Catalog.Find(deal.SponsorId);
            var start = deal.End.AddDays(1);
            var trust = sponsors.TrustOf(deal.SponsorId, deal.Organization);
            var era = _environment.Eras.Era(start.Year);
            if (sponsor is null || trust < SponsorEstimates.RenewalMinTrust || !(sponsor.Local || sponsor.ActiveIn(start.Year)) || !era.Allows(sponsor.Industry, deal.Kind))
            {
                if (left == SponsorEstimates.RenewalLeadDays)
                {
                    // No offer is coming. Say so while there is still time to look for a replacement, never in silence (#254).
                    _notices.Post(deal.Organization, SponsorKeys.InboxEndingSubject, today, ("sponsor", _notices.NameOf(deal.SponsorId)), ("until", deal.End.ToString()));
                }

                continue;
            }

            var amount = SponsorRules.RenewalCents(_book, _environment, deal, sponsor, deal.Terms);
            var added = sponsors.AddOffer(new SponsorOffer(
                0, deal.Number, deal.Organization, deal.SponsorId, deal.Slot, deal.Kind, amount, today, deal.End, OfferStatus.Open, null, deal.Years, deal.Ambition));
            sponsors = added.Section;
            _notices.PostOffer(deal.Organization, today, deal.End, added.Offer, sponsor.Name);
        }

        return sponsors;
    }

    private static string Tag(string purpose, SponsorTalk talk, GameDate? today) =>
        purpose + ":" + talk.Organization.Value + ":" + talk.SponsorId + ":" + talk.Slot.ToString(CultureInfo.InvariantCulture)
        + ":" + talk.Opened + (today is { } day ? ":" + day : string.Empty);
}
