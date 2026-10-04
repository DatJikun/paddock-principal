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
        if (!sponsor.ActiveIn(today.Year) || section.SponsorInDeal(sponsor.Id) || section.IsTaken(sponsor.Id, today))
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

    /// <summary>Adds a signed deal and, if the sponsor asks for something, its objective. Returns the new sections.</summary>
    public static (SponsorsSection Sponsors, ObjectivesSection Objectives, SponsorDeal Deal) Sign(
        SponsorsSection sponsors,
        ObjectivesSection objectives,
        SponsorEnvironment environment,
        SponsorDefinition sponsor,
        OrganizationId organization,
        int slot,
        SlotKind kind,
        long annualCents,
        GameDate start)
    {
        string? objectiveId = null;
        if (sponsor.Objective is { } spec)
        {
            var predicate = PredicateOf(spec);
            decimal? baseline = predicate is NumericPredicate numeric ? environment.Facts.Number(organization, numeric.FactKey) ?? 0m : null;
            var bonusDollars = new Money(annualCents * SponsorEstimates.BonusMilli / 1000).WholeDollars;
            var arguments = new[]
            {
                new KeyValuePair<string, string>("sponsorId", sponsor.Id),
                new KeyValuePair<string, string>("bonus", bonusDollars.ToString(CultureInfo.InvariantCulture)),
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
            objectives = added.Section;
            objectiveId = added.Objective.Id;
        }

        var deal = new SponsorDeal(
            0,
            organization,
            sponsor.Id,
            slot,
            kind,
            start,
            start.AddDays(SponsorEstimates.DealDays - 1),
            annualCents,
            0,
            objectiveId,
            DealObjectiveOutcome.None,
            0,
            DealStatus.Active,
            null);
        var result = sponsors.AddDeal(deal);
        return (result.Section, objectives, result.Deal);
    }
}
