using System.Globalization;
using Paddock.Application.Commands;
using Paddock.Application.Finance;
using Paddock.Domain.Finance;
using Paddock.Domain.Objectives;
using Paddock.Domain.Sponsors;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Sponsors;

/// <summary>The checks and constructions shared by the commands, the day handler and the views. Pure: no state change, no RNG.</summary>
public static class SponsorRules
{
    /// <summary>Why the organization cannot open talks with this sponsor for this slot today, or null when it can.</summary>
    public static TranslationMessage? CanBegin(
        SponsorBook book,
        SponsorEnvironment environment,
        OrganizationId organization,
        string sponsorId,
        int slot,
        GameDate today)
    {
        if (slot < 1 || slot > SponsorEstimates.SlotsPerTeam)
        {
            return TranslationMessage.Of(SponsorKeys.BadSlot);
        }

        var sponsor = environment.Catalog.Find(sponsorId);
        if (sponsor is null)
        {
            return TranslationMessage.Of(SponsorKeys.UnknownSponsor);
        }

        var era = environment.Eras.Era(today.Year);
        var kind = era.Slots[slot - 1];
        if (!sponsor.FitsSlot(kind))
        {
            return TranslationMessage.Of(SponsorKeys.SlotKindMismatch);
        }

        if (!era.Allows(sponsor.Industry, kind))
        {
            return TranslationMessage.Of(SponsorKeys.EraForbids);
        }

        var section = book.Section;
        if (!sponsor.ActiveIn(today.Year) || !SponsorCatalog.OfferedTo(sponsor, organization, environment.IsPlayerTeam(organization)))
        {
            return TranslationMessage.Of(SponsorKeys.SponsorUnavailable);
        }

        if (section.SlotBusy(organization, slot))
        {
            return TranslationMessage.Of(SponsorKeys.SlotBusy);
        }

        if (IndustryConflict(section, environment.Catalog, organization, sponsor.Industry))
        {
            return TranslationMessage.Of(SponsorKeys.IndustryConflict);
        }

        if (environment.Appeal.Appeal(organization, today).Prestige < sponsor.PrestigeNeed)
        {
            return TranslationMessage.Of(SponsorKeys.PrestigeTooLow);
        }

        var skill = environment.Negotiators.Skill(book.World, organization, today);
        if (section.OpenTalksOf(organization).Count >= SponsorPricing.ParallelTalks(skill))
        {
            return TranslationMessage.Of(SponsorKeys.TooManyTalks);
        }

        return null;
    }

    /// <summary>True when the organization already has an active deal or an open talk with a sponsor of this industry.</summary>
    public static bool IndustryConflict(SponsorsSection section, SponsorCatalog catalog, OrganizationId organization, string industry)
    {
        foreach (var deal in section.ActiveDealsOf(organization))
        {
            if (catalog.Find(deal.SponsorId)?.Industry == industry)
            {
                return true;
            }
        }

        foreach (var talk in section.OpenTalksOf(organization))
        {
            if (catalog.Find(talk.SponsorId)?.Industry == industry)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The full annual price of a sponsor in a slot of this kind, in cents, from the era benchmark and the popularity index.</summary>
    public static long FullAnnualCents(SponsorBook book, SponsorEnvironment environment, SponsorDefinition sponsor, SlotKind kind, int year) =>
        SponsorPricing.FullAnnualCents(
            sponsor,
            kind,
            environment.Finance.Facts(year).TypicalDollars,
            book.Finance.PopularityMilli);

    /// <summary>
    /// The annual amount a sponsor offers to renew <paramref name="deal"/> on <paramref name="terms"/>: the full price of the season the renewal
    /// starts in, the sponsor's trust in the team, and what the old deal paid (a satisfied sponsor offers more than it paid). Pure.
    /// </summary>
    public static long RenewalCents(SponsorBook book, SponsorEnvironment environment, SponsorDeal deal, SponsorDefinition sponsor, SponsorTerms terms)
    {
        var start = deal.End.AddDays(1);
        var full = FullAnnualCents(book, environment, sponsor, deal.Kind, start.Year);
        var trust = book.Section.TrustOf(deal.SponsorId, deal.Organization);
        return SponsorPricing.RenewalCents(full, trust, deal.AnnualCents, deal.Terms, terms);
    }

    /// <summary>The authored objective scaled to the team's public strength, or the authored one when that strength is unknown.</summary>
    public static SponsorObjectiveSpec ForTeam(
        SponsorObjectiveSpec spec,
        OrganizationId organization,
        SponsorEnvironment environment,
        GameDate on,
        SponsorAmbition ambition = SponsorAmbition.Standard)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(environment);
        if (environment.Outlook?.ExpectedPosition(organization, on) is not int expected)
        {
            // The team's strength is unknown, so the authored target stands, but the player's choice of a harder or an easier condition still counts.
            return SponsorObjectiveScale.Ambitious(spec, ambition);
        }

        return SponsorObjectiveScale.Scale(spec, expected, environment.Outlook.FieldSize(on), ambition);
    }

    /// <summary>
    /// True when the sponsor asks for something the team's results can meet, so the player can ask an easier or a harder condition. A sponsor with
    /// no condition, and one whose only wish is a nationality (a bonus, not a condition), has a single standard deal.
    /// </summary>
    public static bool AmbitionOpen(SponsorDefinition sponsor)
    {
        ArgumentNullException.ThrowIfNull(sponsor);
        return sponsor.Objective is { } spec && spec.Kind != SponsorObjectiveSpec.DriverNationalityInLineup;
    }

    /// <summary>Why the terms cannot be asked of this sponsor, or null. An ambition other than the standard one needs a condition to change.</summary>
    public static TranslationMessage? CheckTerms(SponsorDefinition sponsor, SponsorTerms terms)
    {
        ArgumentNullException.ThrowIfNull(sponsor);
        if (!terms.IsValid || (terms.Ambition != SponsorAmbition.Standard && !AmbitionOpen(sponsor)))
        {
            return TranslationMessage.Of(SponsorKeys.BadTerms);
        }

        return null;
    }

    public static ObjectivePredicate PredicateOf(SponsorObjectiveSpec spec) => spec.Kind switch
    {
        SponsorObjectiveSpec.PodiumsAtLeast => new PodiumsAtLeast(int.Parse(spec.Value, CultureInfo.InvariantCulture)),
        SponsorObjectiveSpec.ChampionshipPositionAtMost => new ChampionshipPositionAtMost(int.Parse(spec.Value, CultureInfo.InvariantCulture)),
        SponsorObjectiveSpec.PointsAtLeast => new PointsAtLeast(decimal.Parse(spec.Value, CultureInfo.InvariantCulture)),
        SponsorObjectiveSpec.DriverNationalityInLineup => new DriverNationalityInLineup(spec.Value),
        _ => throw new ArgumentException("Unknown sponsor objective kind '" + spec.Kind + "'.", nameof(spec)),
    };

    /// <summary>The grantor of a sponsor's objectives. Sponsors are catalogue entries, not world organizations, so the id is the sponsor's own.</summary>
    public static OrganizationId GrantorOf(string sponsorId) => OrganizationId.Real(sponsorId);

    /// <summary>
    /// Adds a signed deal and, if the sponsor asks for something, its objective. Returns the new sections. The deal runs
    /// <see cref="SponsorTerms.Years"/> years; a deal of more than one year gets a fresh objective each year (<see cref="GrantObjective"/>, called
    /// by the day handler on each anniversary). A nationality wish, when the caller passes one, rides on the deal as a bonus and is no objective.
    /// </summary>
    public static (SponsorsSection Sponsors, ObjectivesSection Objectives, SponsorDeal Deal) Sign(
        SponsorsSection sponsors,
        ObjectivesSection objectives,
        SponsorEnvironment environment,
        SponsorDefinition sponsor,
        OrganizationId organization,
        int slot,
        SlotKind kind,
        long annualCents,
        GameDate start,
        SponsorTerms? terms = null,
        SponsorWish? wish = null)
    {
        var agreed = terms ?? SponsorTerms.Default;
        string? objectiveId = null;
        if (AmbitionOpen(sponsor))
        {
            (objectives, objectiveId) = GrantObjective(objectives, environment, sponsor, organization, annualCents, start, agreed.Ambition);
        }

        var deal = new SponsorDeal(
            0,
            organization,
            sponsor.Id,
            slot,
            kind,
            start,
            start.AddDays(agreed.Days - 1),
            annualCents,
            0,
            objectiveId,
            DealObjectiveOutcome.None,
            0,
            DealStatus.Active,
            null,
            agreed.Years,
            agreed.Ambition,
            wish?.Nationality,
            wish?.RaceSeat ?? false);
        var result = sponsors.AddDeal(deal);
        return (result.Section, objectives, result.Deal);
    }

    /// <summary>
    /// The objective of one year of a deal: the sponsor's authored condition, scaled to the team's strength today and to the ambition the player
    /// chose, with the bonus for meeting it fixed in the effect (so a later change of the numbers cannot change a deal already signed).
    /// Returns the objectives section and the new objective's id, or the section and null when the sponsor asks for nothing.
    /// </summary>
    public static (ObjectivesSection Objectives, string? ObjectiveId) GrantObjective(
        ObjectivesSection objectives,
        SponsorEnvironment environment,
        SponsorDefinition sponsor,
        OrganizationId organization,
        long annualCents,
        GameDate start,
        SponsorAmbition ambition)
    {
        if (!AmbitionOpen(sponsor) || sponsor.Objective is not { } spec)
        {
            return (objectives, null);
        }

        var scaled = ForTeam(spec, organization, environment, start, ambition);
        var predicate = PredicateOf(scaled);
        decimal? baseline = predicate is NumericPredicate numeric ? environment.Facts.Number(organization, numeric.FactKey) ?? 0m : null;
        var reward = SponsorObjectiveScale.Reward(spec, scaled);
        var bonusDollars = new Money(annualCents * reward.BonusMilli / 1000).WholeDollars;
        var arguments = new[]
        {
            new KeyValuePair<string, string>("sponsorId", sponsor.Id),
            new KeyValuePair<string, string>("bonus", bonusDollars.ToString(CultureInfo.InvariantCulture)),
            new KeyValuePair<string, string>("bonusMilli", reward.BonusMilli.ToString(CultureInfo.InvariantCulture)),
            new KeyValuePair<string, string>("trust", reward.Trust.ToString(CultureInfo.InvariantCulture)),
        };
        var draft = new ObjectiveDraft(
            organization,
            GrantorOf(sponsor.Id),
            SponsorKeys.ObjectiveKind,
            SponsorKeys.ObjectiveReason,
            predicate,
            baseline,
            start.AddDays(Math.Min(spec.WithinDays, SponsorEstimates.DealDays - 1)),
            new ObjectiveEffect(SponsorKeys.ObjectiveOnMet, arguments),
            new ObjectiveEffect(SponsorKeys.ObjectiveOnFailed, arguments));
        var added = objectives.Add(draft, start);
        return (added.Section, added.Objective.Id);
    }
}
