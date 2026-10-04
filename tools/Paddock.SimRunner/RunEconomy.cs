using Paddock.Application.Board;
using Paddock.Domain.Board;
using Paddock.Domain.Cars;
using Paddock.Domain.Finance;
using Paddock.Domain.Sponsors;
using Paddock.Domain.World;

namespace Paddock.SimRunner;

/// <summary>
/// The money, sponsors, board and cars of a world at the end of a run, read from its sections. A read of the finished world, not part
/// of it: nothing here is saved or hashed. Cash is in whole nominal dollars. A world without a section reports zero for it.
/// </summary>
public sealed record RunEconomy(int Books, int Insolvent, long CashLow, long CashHigh, int SponsorDeals, int Boards, int Dismissals, int Cars)
{
    public static RunEconomy Of(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var finance = world.Section<FinanceSection>(FinanceSection.SectionName);
        var books = 0;
        var insolvent = 0;
        long low = 0;
        long high = 0;
        if (finance is not null)
        {
            foreach (var organization in world.Organizations)
            {
                if (!finance.HasBook(organization.Id))
                {
                    continue;
                }

                var cash = new Money(finance.BalanceOf(organization.Id)).WholeDollars;
                low = books == 0 ? cash : Math.Min(low, cash);
                high = books == 0 ? cash : Math.Max(high, cash);
                books++;
                if (finance.IsInsolvent(organization.Id))
                {
                    insolvent++;
                }
            }
        }

        var board = world.Section<BoardSection>(BoardSection.SectionName);
        return new RunEconomy(
            books,
            insolvent,
            low,
            high,
            world.Section<SponsorsSection>(SponsorsSection.SectionName)?.Deals.Count ?? 0,
            board?.Boards.Count ?? 0,
            board?.Changes.Count(change => change.ReasonKey == BoardKeys.ReputationDismissed) ?? 0,
            world.Section<CarsSection>(CarsSection.SectionName)?.Cars.Count ?? 0);
    }
}
