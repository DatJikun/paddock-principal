using Paddock.Application.Access;
using Paddock.Application.Cars;
using Paddock.Application.Contracts;
using Paddock.Domain.Cars;
using Paddock.Domain.Contracts;
using Paddock.Domain.People;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using ManagerId = Paddock.Application.Managers.ManagerId;

namespace Paddock.Application.Supply;

/// <summary>The engine a team races, as its engineers read it: bands, never the supplier's exact numbers (INV-003).</summary>
public sealed record OwnEngineView(CarBandView Power, CarBandView Reliability, int VersionSeason, int LagSeasons);

/// <summary>
/// One of the team's own deals. The engine bands are present for an engine deal that is in force. <paramref name="EngineName"/> is the
/// authored model of the engine when there is one and <paramref name="SupplierName"/> is the supplier as the world names it (#268).
/// </summary>
public sealed record OwnSupplyDealView(
    string DealId,
    string SupplierId,
    SupplyItem Item,
    SupplyKind Kind,
    int FirstSeason,
    int LastSeason,
    long AnnualPriceCents,
    bool Exclusive,
    SupplyDealStatus Status,
    OwnEngineView? Engine,
    string? EngineName,
    string SupplierName);

/// <summary>One of the team's own negotiations, with what the supplier has said (the stated reasons are translation keys).</summary>
public sealed record OwnSupplyTalkView(
    string NegotiationId,
    string SupplierId,
    SupplyItem Item,
    SupplyKind Kind,
    NegotiationStatus Status,
    long OfferCents,
    long? CounterCents,
    int RoundsLeft,
    GameDate Deadline,
    IReadOnlyList<string> Reasons,
    string SupplierName);

/// <summary>
/// What one manager may see: own deals and own talks only. A supplier's other customers and true development state stay hidden. Only the engine
/// is listed for now (PP-064: tyres and fuel come later), so a deal or a talk for anything else is not shown.
/// </summary>
public sealed record ManagerSupplyView(IReadOnlyList<OwnSupplyDealView> Deals, IReadOnlyList<OwnSupplyTalkView> Talks);

/// <summary>Queries do not change the world and do not draw RNG (INV-005).</summary>
public sealed class SupplyQuery
{
    private readonly SupplyBook _book;
    private readonly SupplyEnvironment _environment;

    public SupplyQuery(SupplyBook book, SupplyEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    public ManagerSupplyView View(AccessContext access)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (access.Kind == AccessKind.Developer)
        {
            throw new InvalidOperationException("A developer reads Truth, not the manager view.");
        }

        if (access.Manager is not { } actor)
        {
            throw new InvalidOperationException("A manager view needs a manager.");
        }

        var manager = new ManagerId(actor.Value);
        var world = _book.World;
        var today = new GameDate(world.CurrentDate.Year, world.CurrentDate.Month, world.CurrentDate.Day);
        var section = _book.Section;
        var deals = section.Deals
            .Where(deal => deal.Item == SupplyItem.Engine && _environment.Control.Controls(manager, deal.Customer))
            .Select(deal => new OwnSupplyDealView(
                deal.Id,
                deal.Supplier.Value,
                deal.Item,
                deal.Kind,
                deal.FirstSeason,
                deal.LastSeason,
                deal.AnnualPriceCents,
                deal.Terms.Exclusive,
                deal.Status,
                deal.Item == SupplyItem.Engine && deal.IsInForceOn(today) ? EngineBand(deal, world, today) : null,
                deal.EngineName,
                SupplierName(world, deal.Supplier, today)))
            .ToArray();
        var talks = section.Negotiations
            .Where(negotiation => negotiation.Item == SupplyItem.Engine && _environment.Control.Controls(manager, negotiation.Customer))
            .Select(negotiation => new OwnSupplyTalkView(
                negotiation.Id,
                negotiation.Supplier.Value,
                negotiation.Item,
                negotiation.Kind,
                negotiation.Status,
                negotiation.Offer.AnnualPriceCents,
                negotiation.Counter?.AnnualPriceCents,
                negotiation.RoundsLeft,
                negotiation.Deadline,
                negotiation.Reasons,
                SupplierName(world, negotiation.Supplier, today)))
            .ToArray();
        return new ManagerSupplyView(deals, talks);
    }

    /// <summary>The supplier as the world names it on the day, or its id when the world does not know it.</summary>
    private static string SupplierName(Paddock.Domain.World.WorldState world, Paddock.Domain.World.OrganizationId supplier, GameDate today)
    {
        foreach (var organization in world.Organizations)
        {
            if (organization.Id == supplier)
            {
                return organization.NameOn(today);
            }
        }

        return supplier.Value;
    }

    private OwnEngineView EngineBand(SupplyDeal deal, Paddock.Domain.World.WorldState world, GameDate today)
    {
        var versionSeason = SupplyContribution.VersionSeason(deal.Kind, today.Year);
        var version = _environment.Profiles.EngineOf(deal.Supplier, versionSeason);
        var vision = TeamEngineers.Attribute(world, deal.Customer, today, StaffRole.TechnicalDirector, "vision");
        var aero = TeamEngineers.Attribute(world, deal.Customer, today, StaffRole.HeadOfAerodynamics, "aerodynamics");
        var key = deal.Customer.Value + "|" + deal.Supplier.Value + "|" + versionSeason.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var power = CarKnowledgeBands.Around(version.Power, vision, aero, key + "|power");
        var reliability = CarKnowledgeBands.Around(version.Reliability, vision, aero, key + "|reliability");
        return new OwnEngineView(
            new CarBandView(power.Low, power.High),
            new CarBandView(reliability.Low, reliability.High),
            versionSeason,
            SupplyEstimates.LagSeasons(deal.Kind));
    }
}
