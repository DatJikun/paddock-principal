using Paddock.Application.Finance;
using Paddock.Domain.Finance;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Tests.Finance;

/// <summary>
/// #160: the severance of a dismissed manager is a cost in the team's ledger, a termination line with the manager as the
/// counterparty. The amounts are SYNTHETIC fixtures; the real severance is a board ESTIMATE.
/// </summary>
public sealed class FinanceSeveranceTests
{
    private static readonly OrganizationId Alfa = OrganizationId.Real("alfa");

    private static readonly GameDate Day = new(1956, 6, 7);

    private static FinanceBook BookWith(params OrganizationId[] withBooks)
    {
        var world = WorldState.At(Day);
        var facts = new EraFinanceFacts(FinanceEstimates.PromoterModel, 20_000, 60_000, 250_000);
        var section = FinanceSection.Empty;
        foreach (var organization in withBooks)
        {
            section = section.Open(organization, Day, 60_000, facts);
        }

        world = world.WithSection(section);
        return new FinanceBook(() => world, next => world = next);
    }

    [Fact]
    public void ASeverancePostsATerminationCostToTheTeamsLedger()
    {
        var book = BookWith(Alfa);
        var before = book.Section.BalanceOf(Alfa);

        new FinanceSeverance(book).Pay(Alfa, "human:pam", 50_000, Day);

        Assert.Equal(before - Money.FromDollars(50_000).Cents, book.Section.BalanceOf(Alfa));
        var line = book.Section.EntriesOf(Alfa)[^1];
        Assert.Equal(LedgerCategories.Other, line.Category);
        Assert.Equal("human:pam", line.Counterparty);
        Assert.Equal(FinanceReason.Termination, line.ReasonKey);
        Assert.Equal(-Money.FromDollars(50_000).Cents, line.AmountCents);
    }

    [Fact]
    public void ATeamWithNoBooksOrAnAmountOfZeroPostsNothing()
    {
        var book = BookWith(Alfa);
        var entries = book.Section.EntriesOf(Alfa).Count;

        new FinanceSeverance(book).Pay(OrganizationId.Real("beta"), "human:pam", 50_000, Day);
        new FinanceSeverance(book).Pay(Alfa, "human:pam", 0, Day);

        Assert.Equal(entries, book.Section.EntriesOf(Alfa).Count);
        Assert.False(book.Section.HasBook(OrganizationId.Real("beta")));
    }
}
