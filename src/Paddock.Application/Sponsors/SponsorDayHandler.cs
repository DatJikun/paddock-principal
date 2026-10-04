using System.Globalization;
using Paddock.Application.Finance;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Finance;
using Paddock.Domain.Inbox;
using Paddock.Domain.Random;
using Paddock.Domain.Sponsors;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Time;

namespace Paddock.Application.Sponsors;

/// <summary>Posts sponsor notices to the managers who run an organization. Not blocking: a notice needs no decision.</summary>
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

        foreach (var manager in _environment.Control.ManagersOf(organization))
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
        var market = new Lazy<RngStream>(() => new RngStream(RngStreamName.Market, context.Stream(RngStreamName.Market).State));
        var before = sponsors;
        var financeBefore = finance;

        sponsors = RunTalks(sponsors, market, today);
        (sponsors, finance) = PostInstalments(sponsors, finance, today);
        sponsors = Expire(sponsors, today);
        sponsors = OfferRenewals(sponsors, today);

        if (!ReferenceEquals(before, sponsors) || !ReferenceEquals(financeBefore, finance))
        {
            _book.Write(sponsors, null, ReferenceEquals(financeBefore, finance) ? null : finance);
        }
    }

    private SponsorsSection RunTalks(SponsorsSection sponsors, Lazy<RngStream> market, GameDate today)
    {
        foreach (var talk in sponsors.Talks.Where(talk => talk.IsOpen))
        {
            var current = talk;
            if (sponsors.SponsorInDeal(talk.SponsorId) || sponsors.IsTaken(talk.SponsorId, today))
            {
                sponsors = Lose(sponsors, current, today, take: false);
                continue;
            }

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
            if (deal.InstalmentsPaid >= SponsorEstimates.InstalmentsPerYear || !finance.HasBook(deal.Organization))
            {
                continue;
            }

            var owed = today == deal.End
                ? SponsorEstimates.InstalmentsPerYear - deal.InstalmentsPaid
                : today.Day == SponsorEstimates.InstalmentDayOfMonth ? 1 : 0;
            var paid = deal.InstalmentsPaid;
            for (var step = 0; step < owed; step++)
            {
                paid++;
                var cents = SponsorPricing.InstalmentCents(deal.AnnualCents, paid);
                if (cents > 0)
                {
                    finance = finance.Post(deal.Organization, today, LedgerCategories.Sponsor, deal.SponsorId, cents, SponsorReason.Instalment);
                }
            }

            if (paid != deal.InstalmentsPaid)
            {
                sponsors = sponsors.Replace(deal with { InstalmentsPaid = paid });
            }
        }

        return (sponsors, finance);
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
            if (sponsor is null || trust < SponsorEstimates.RenewalMinTrust || !sponsor.ActiveIn(start.Year) || !era.Allows(sponsor.Industry, deal.Kind))
            {
                continue;
            }

            var full = SponsorRules.FullAnnualCents(_book, _environment, sponsor, deal.Kind, start.Year);
            var amount = full * (SponsorEstimates.RenewalBaseMilli + (SponsorEstimates.RenewalPerTrustMilli * trust)) / 1000;
            var added = sponsors.AddOffer(new SponsorOffer(0, deal.Number, deal.Organization, deal.SponsorId, deal.Slot, deal.Kind, amount, today, deal.End, OfferStatus.Open, null));
            sponsors = added.Section;
            _notices.Post(
                deal.Organization,
                SponsorKeys.InboxOfferSubject,
                today,
                ("sponsor", sponsor.Name),
                ("amount", SponsorNotices.Dollars(amount)),
                ("offer", added.Offer.Id),
                ("until", deal.End.ToString()));
        }

        return sponsors;
    }

    private static string Tag(string purpose, SponsorTalk talk, GameDate? today) =>
        purpose + ":" + talk.Organization.Value + ":" + talk.SponsorId + ":" + talk.Slot.ToString(CultureInfo.InvariantCulture)
        + ":" + talk.Opened + (today is { } day ? ":" + day : string.Empty);
}
