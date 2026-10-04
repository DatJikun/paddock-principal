using Paddock.Domain.Finance;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Tests.Persistence;

namespace Paddock.Tests.Finance;

public class FinancePersistenceTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-finance-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void AFinanceSectionRoundTripsThroughTheSaveWithTheSameHash()
    {
        var world = WorldFixtures.Small();
        var organization = world.Organizations[0].Id;
        var other = world.Organizations[1].Id;
        var facts = new EraFinanceFacts(FinanceEstimates.PromoterModel, 20_000, 60_000, 250_000);
        var section = FinanceSection.Empty
            .Open(organization, WorldFixtures.Opening, 60_000, facts)
            .Open(other, WorldFixtures.Opening, 20_000, facts)
            .Post(organization, WorldFixtures.Opening, LedgerCategories.PrizeMoney, "fixture_driver_1", 80_000, FinanceReason.PrizeMoney);
        var broke = section.Post(other, new GameDate(1955, 2, 1), LedgerCategories.Supply, null, -Money.FromDollars(30_000).Cents, FinanceReason.RaceRunning);
        var (watched, _) = broke.ReviewInsolvency(new GameDate(1955, 2, 1));
        world = world.WithSection(watched);

        using var file = SaveFile.Create(Path.Combine(_directory, "finance.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(file);
        repository.SaveWorld(world, WorldFixtures.Opening);
        var loaded = repository.LoadWorld();

        Assert.Equal(world.StateHash(), loaded.StateHash());
        var finance = loaded.Section<FinanceSection>(FinanceSection.SectionName)!;
        Assert.Equal(watched.BalanceOf(organization), finance.BalanceOf(organization));
        Assert.Equal(watched.BalanceOf(other), finance.BalanceOf(other));
        Assert.Equal(new GameDate(1955, 2, 1), finance.OverdraftSince(other));
        Assert.False(finance.IsInsolvent(other));
        Assert.Equal(finance.EntriesOf(organization).Sum(entry => entry.AmountCents), finance.BalanceOf(organization));
    }
}
