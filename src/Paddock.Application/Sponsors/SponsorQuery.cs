using Paddock.Application.Access;
using Paddock.Application.Commands;
using Paddock.Application.Finance;
using Paddock.Application.Objectives;
using Paddock.Domain.Sponsors;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Sponsors;

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
    string? ObjectiveId);

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
    TranslationMessage Note);

public sealed record SponsorOfferView(string Id, string SponsorName, long AnnualCents, DateOnly ValidUntil);

/// <summary>A sponsor that could fill a slot, and the reason it cannot yet, if there is one.</summary>
public sealed record SponsorCandidateView(string SponsorId, string SponsorName, TranslationMessage Industry, long IndicativeAnnualCents, TranslationMessage? Blocked);

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
        IReadOnlyList<ObjectiveItemView> Objectives) : SponsorView;

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

        var slots = new List<SponsorSlotView>();
        for (var slot = 1; slot <= SponsorEstimates.SlotsPerTeam; slot++)
        {
            var kind = era.Slots[slot - 1];
            var deal = deals.FirstOrDefault(candidate => candidate.Slot == slot && candidate.IsActiveOn(today));
            var talk = talks.FirstOrDefault(candidate => candidate.Slot == slot);
            var candidates = deal is not null || talk is not null
                ? []
                : catalog.Candidates(today.Year, kind, era)
                    .Select(sponsor => new SponsorCandidateView(
                        sponsor.Id,
                        sponsor.Name,
                        TranslationMessage.Of(SponsorKeys.IndustryName(sponsor.Industry)),
                        SponsorRules.FullAnnualCents(book, environment, sponsor, kind, today.Year),
                        SponsorRules.CanBegin(book, environment, subject, sponsor.Id, slot, today)))
                    .ToArray();
            slots.Add(new SponsorSlotView(slot, TranslationMessage.Of(SponsorKeys.SlotName(kind)), deal?.Id, talk?.Id, candidates));
        }

        var dealViews = deals.Select(deal => new SponsorDealView(
            deal.Id,
            deal.SponsorId,
            catalog.Find(deal.SponsorId)?.Name ?? deal.SponsorId,
            TranslationMessage.Of(SponsorKeys.SlotName(deal.Kind)),
            TranslationMessage.Of(SponsorKeys.IndustryName(catalog.Find(deal.SponsorId)?.Industry ?? SponsorIndustries.Patron)),
            deal.AnnualCents,
            Day(deal.Start),
            Day(deal.End),
            section.TrustOf(deal.SponsorId, subject),
            deal.ObjectiveId)).ToArray();

        var insight = skill >= SponsorEstimates.RivalInsightSkill;
        var talkViews = talks.Select(talk => new SponsorTalkView(
            talk.Id,
            talk.SponsorId,
            catalog.Find(talk.SponsorId)?.Name ?? talk.SponsorId,
            TranslationMessage.Of(SponsorKeys.SlotName(talk.Kind)),
            talk.AnnualCentsOn(today),
            talk.CappedAnnualCents,
            insight && talk.Rival != RivalState.Undecided ? talk.Rival == RivalState.Present : null,
            TranslationMessage.Of(insight && talk.Rival == RivalState.Present ? SponsorKeys.ViewRivalKnown : SponsorKeys.ViewTermsEstimate))).ToArray();

        var offerViews = section.OpenOffersOf(subject).Select(offer => new SponsorOfferView(
            offer.Id,
            catalog.Find(offer.SponsorId)?.Name ?? offer.SponsorId,
            offer.AnnualCents,
            Day(offer.ValidUntil))).ToArray();

        var objectiveIds = deals.Where(deal => deal.ObjectiveId is not null).Select(deal => deal.ObjectiveId!).ToHashSet(StringComparer.Ordinal);
        var items = objectiveIds.Count == 0
            ? []
            : objectives.View(access, book.Objectives, today).Items.Where(item => objectiveIds.Contains(item.Id)).ToArray();
        return new SponsorView.Own(slots, dealViews, talkViews, offerViews, items);
    }

    private static DateOnly Day(GameDate date) => new(date.Year, date.Month, date.Day);
}
