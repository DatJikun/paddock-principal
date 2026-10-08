using Paddock.Application.Access;
using Paddock.Application.Supply;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using AccessManagerId = Paddock.Application.Access.ManagerId;

namespace Paddock.Tests.Supply;

/// <summary>What the suppliers screen reads (#268): the engine, named, with its supplier named; nothing else is listed for now.</summary>
public sealed class SupplyViewTests
{
    private static readonly GameDate Opening = new(1955, 1, 1);

    [Fact]
    public void AnEngineDealShowsTheEngineAndTheSupplierByName()
    {
        var kit = new SupplyKit(Opening);
        var (section, deal) = kit.Book.Section.AddDeal(new SupplyDeal(
            kit.Book.Section.NextDeal,
            SupplyItem.Engine,
            SupplyKind.Customer,
            SupplyKit.Acme,
            SupplyKit.Alfa,
            1955,
            new SupplyTerms(1_000_000, 2, exclusive: false),
            Opening,
            SupplyDealStatus.Active,
            null,
            0,
            "Acme 2.5 V8"));
        kit.Book.Write(section);

        var view = new SupplyQuery(kit.Book, kit.Environment).View(AccessContext.ForManager(new AccessManagerId(SupplyKit.Anna.Value)));

        var shown = Assert.Single(view.Deals);
        Assert.Equal(deal.Id, shown.DealId);
        Assert.Equal("Acme 2.5 V8", shown.EngineName);
        Assert.Equal("Acme Motors", shown.SupplierName);
        Assert.NotNull(shown.Engine);
    }

    [Fact]
    public void OnlyTheEngineIsListedUntilTyresAndFuelCome()
    {
        var kit = new SupplyKit(Opening);
        var (section, _) = kit.Book.Section.AddDeal(new SupplyDeal(
            kit.Book.Section.NextDeal,
            SupplyItem.Tyres,
            SupplyKind.Customer,
            SupplyKit.Acme,
            SupplyKit.Alfa,
            1955,
            new SupplyTerms(100_000, 2, exclusive: false),
            Opening,
            SupplyDealStatus.Active,
            null,
            0,
            null));
        kit.Book.Write(section);

        var view = new SupplyQuery(kit.Book, kit.Environment).View(AccessContext.ForManager(new AccessManagerId(SupplyKit.Anna.Value)));

        Assert.Empty(view.Deals);
        Assert.Empty(view.Talks);
    }

    [Fact]
    public void ATalkNamesItsSupplier()
    {
        var kit = new SupplyKit(Opening);
        kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, kit.Floor(SupplyItem.Engine, SupplyKind.Customer) / 2, Opening);
        kit.Live(Opening, 8);

        var view = new SupplyQuery(kit.Book, kit.Environment).View(AccessContext.ForManager(new AccessManagerId(SupplyKit.Anna.Value)));

        Assert.Equal("Acme Motors", Assert.Single(view.Talks).SupplierName);
    }
}
