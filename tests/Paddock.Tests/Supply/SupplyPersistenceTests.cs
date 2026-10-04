using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Persistence;
using Paddock.Tests.Persistence;

namespace Paddock.Tests.Supply;

public class SupplyPersistenceTests : IDisposable
{
    private static readonly GameDate Opening = new(1955, 1, 1);

    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-supply-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void DealsAndNegotiationsRoundTripThroughTheSaveWithTheSameHash()
    {
        var kit = new SupplyKit(Opening);
        var floor = kit.Floor(SupplyItem.Engine, SupplyKind.Customer);
        kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, floor, Opening);
        kit.Propose(SupplyKit.Bram, SupplyKit.Beta, SupplyKit.Bolt, floor / 2, Opening, exclusive: false);
        kit.Live(Opening, 8);
        var world = kit.World.WithDate(new GameDate(1955, 1, 9));
        var section = kit.Book.Section;
        Assert.NotEmpty(section.Deals);
        Assert.Contains(section.Negotiations, negotiation => negotiation.Counter is not null && negotiation.Reasons.Count > 0);

        using var file = SaveFile.Create(Path.Combine(_directory, "supply.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(file);
        repository.SaveWorld(world, new GameDate(1955, 1, 9));
        var loaded = repository.LoadWorld();

        Assert.Equal(world.StateHash(), loaded.StateHash());
        var restored = loaded.Section<SupplySection>(SupplySection.SectionName)!;
        Assert.Equal(section.NextDeal, restored.NextDeal);
        Assert.Equal(section.NextNegotiation, restored.NextNegotiation);
        Assert.Equal(section.Deals, restored.Deals);
        Assert.Equal(section.Negotiations.Select(item => (item.Id, item.Status, item.Counter, item.RespondOn)), restored.Negotiations.Select(item => (item.Id, item.Status, item.Counter, item.RespondOn)));
    }

    [Fact]
    public void ASaveWithoutSupplyLoadsAsNoSection()
    {
        var world = WorldFixtures.Small();
        using var file = SaveFile.Create(Path.Combine(_directory, "plain.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(file);
        repository.SaveWorld(world, WorldFixtures.Opening);

        var loaded = repository.LoadWorld();

        Assert.Equal(world.StateHash(), loaded.StateHash());
        Assert.Null(loaded.Section<SupplySection>(SupplySection.SectionName));
    }

    [Fact]
    public void RecordNumbersAreNeverReusedAndRestoreChecksTheCounters()
    {
        var kit = new SupplyKit(Opening);
        kit.Propose(SupplyKit.Anna, SupplyKit.Alfa, SupplyKit.Acme, kit.Floor(SupplyItem.Engine, SupplyKind.Customer), Opening);
        kit.Live(Opening, 8);
        var section = kit.Book.Section;

        Assert.Throws<InvalidOperationException>(() => SupplySection.Restore(1, section.NextNegotiation, section.Deals, section.Negotiations));
        Assert.Throws<InvalidOperationException>(() => SupplySection.Restore(section.NextDeal, 1, section.Deals, section.Negotiations));
        var (next, added) = section.AddDeal(section.Deals[0]);
        Assert.Equal(section.NextDeal, added.Number);
        Assert.Equal(section.NextDeal + 1, next.NextDeal);
    }
}
