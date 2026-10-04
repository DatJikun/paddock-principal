using Paddock.Application.Commands;
using Paddock.Application.Finance;
using Paddock.Application.Managers;
using Paddock.Domain.Contracts;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Supply;

/// <summary>The checks and shared steps of the supply commands and the day handler.</summary>
public static class SupplyRules
{
    /// <summary>The checks every supply command starts with: a known manager who runs a team of the world that has books.</summary>
    public static TranslationMessage? Gate(
        SupplyBook book,
        SupplyEnvironment environment,
        CommandContext context,
        ManagerId manager,
        string organizationText,
        out OrganizationId organization)
    {
        organization = default;
        if (!context.Managers.Contains(manager))
        {
            return TranslationMessage.Of(TranslationKeys.ManagerUnknown, ("managerId", manager.Value));
        }

        if (!FinanceIds.TryParse(organizationText, out organization))
        {
            return TranslationMessage.Of(SupplyKeys.UnknownOrganization);
        }

        if (!environment.Control.Controls(manager, organization))
        {
            return TranslationMessage.Of(SupplyKeys.NotYourOrganization);
        }

        var known = organization;
        var found = book.World.Organizations.FirstOrDefault(candidate => candidate.Id == known);
        if (found is null)
        {
            return TranslationMessage.Of(SupplyKeys.UnknownOrganization);
        }

        if (found.Kind != OrganizationKind.Team)
        {
            return TranslationMessage.Of(SupplyKeys.NotATeam);
        }

        return book.Finance.HasBook(organization) ? null : TranslationMessage.Of(SupplyKeys.NoBooks);
    }

    /// <summary>Validates a proposal: what is asked of whom, for which seasons. Does not look at the price against the supplier's floor (that is the supplier's answer).</summary>
    public static TranslationMessage? CheckProposal(
        SupplyBook book,
        SupplyEnvironment environment,
        OrganizationId customer,
        OrganizationId supplier,
        SupplyItem item,
        SupplyKind kind,
        int firstSeason,
        long priceCents,
        int seasons,
        GameDate today,
        string? revising)
    {
        if (!Enum.IsDefined(item) || !Enum.IsDefined(kind))
        {
            return TranslationMessage.Of(SupplyKeys.BadTerms);
        }

        if (priceCents <= 0 || seasons < 1 || seasons > NegotiationEstimates.MaxYears)
        {
            return TranslationMessage.Of(SupplyKeys.BadTerms);
        }

        if (firstSeason < today.Year || firstSeason > today.Year + SupplyEstimates.MaxStartSeasonsAhead)
        {
            return TranslationMessage.Of(SupplyKeys.BadSeason);
        }

        if (kind == SupplyKind.Works)
        {
            return TranslationMessage.Of(SupplyKeys.WorksNotOffered);
        }

        if (kind == SupplyKind.LastYearEngine && item != SupplyItem.Engine)
        {
            return TranslationMessage.Of(SupplyKeys.LastYearEnginesOnly);
        }

        if (item == SupplyItem.Fuel && !environment.Eras.FuelChoice(firstSeason))
        {
            return TranslationMessage.Of(SupplyKeys.FuelNotChoosable);
        }

        var supplierOrganization = book.World.Organizations.FirstOrDefault(candidate => candidate.Id == supplier);
        if (supplierOrganization is null || supplier == customer)
        {
            return TranslationMessage.Of(SupplyKeys.UnknownSupplier);
        }

        var canSupply = supplierOrganization.Kind == OrganizationKind.EngineSupplier
            || (item == SupplyItem.Engine && supplierOrganization.Kind == OrganizationKind.Team && environment.Programmes.Sells(supplier, firstSeason));
        if (!canSupply)
        {
            return TranslationMessage.Of(SupplyKeys.SupplierCannotSupply);
        }

        var last = firstSeason + seasons - 1;
        var section = book.Section;
        if (section.DealsOf(customer).Any(deal => deal.Item == item && deal.Overlaps(firstSeason, last)))
        {
            return TranslationMessage.Of(SupplyKeys.AlreadySupplied);
        }

        if (revising is null
            && section.Active().Any(negotiation => negotiation.Customer == customer && negotiation.Supplier == supplier && negotiation.Item == item))
        {
            return TranslationMessage.Of(SupplyKeys.TalksOpen);
        }

        return null;
    }

    /// <summary>What the supplier knows when it answers <paramref name="negotiation"/> on <paramref name="today"/>.</summary>
    public static SupplierSituation SituationOf(SupplySection section, SupplyEnvironment environment, SupplyNegotiation negotiation, GameDate today)
    {
        var served = section.ServedBy(negotiation.Supplier, negotiation.Item, new GameDate(negotiation.FirstSeason, 1, 1));
        var others = served.Where(deal => deal.Customer != negotiation.Customer).ToArray();
        return new SupplierSituation(
            environment.Eras.ReferenceBudgetCents(Math.Max(today.Year, negotiation.FirstSeason)),
            others.Length,
            others.Any(deal => deal.Terms.Exclusive));
    }

    /// <summary>
    /// Signs the negotiation at <paramref name="terms"/>: adds the deal, closes the negotiation as agreed, and points the customer's
    /// cars at the engine when it is in force. Returns null (and changes nothing) when a deal signed meanwhile now overlaps.
    /// </summary>
    public static (WorldState World, SupplyDeal Deal)? Sign(WorldState world, SupplyNegotiation negotiation, SupplyTerms terms, GameDate today)
    {
        var section = world.Section<SupplySection>(SupplySection.SectionName) ?? SupplySection.Empty;
        var last = negotiation.FirstSeason + terms.Seasons - 1;
        if (section.DealsOf(negotiation.Customer).Any(existing => existing.Item == negotiation.Item && existing.Overlaps(negotiation.FirstSeason, last)))
        {
            return null;
        }

        var (withDeal, deal) = section.AddDeal(new SupplyDeal(
            section.NextDeal,
            negotiation.Item,
            negotiation.Kind,
            negotiation.Supplier,
            negotiation.Customer,
            negotiation.FirstSeason,
            terms,
            today,
            SupplyDealStatus.Active,
            null,
            0,
            null));
        var closed = withDeal.ReplaceNegotiation(negotiation.WithAgreement(deal.Number, today));
        return (SupplyCars.Sync(world.WithSection(closed), closed, today), deal);
    }
}
