using Paddock.Application.Career;
using Paddock.Domain.Career;
using Paddock.Domain.Contracts;
using Paddock.Domain.Finance;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.SimRunner;
using Paddock.Tests.Career;

namespace Paddock.Tests.Finance;

/// <summary>
/// #355: the win and title bonuses of a driver's contract are paid by the team, on the race day of a win and when the
/// drivers' title is decided. Before the fix they were only read by the other side's evaluation of an offer.
/// </summary>
public class ContractBonusTests
{
    private const ulong Seed = 7;

    private const long WinBonusDollars = 1_500;

    [Fact]
    public void ADriverWithAWinBonusWinsAndTheTeamLedgerShowsTheBonus()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);
        session.StoreWorld(WithBonuses(session.World, WinBonusDollars, 0));

        CareerHost.RunUntil(session, new GameDate(1955, 3, 4), null, CareerKit.Options);

        var race = session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName)!.Latest()!;
        var winner = Assert.Single(race.Rows, row => row.Position == 1 && row.Classified);
        var team = OrganizationId.Real(winner.TeamId);
        var finance = session.World.Section<FinanceSection>(FinanceSection.SectionName)!;
        var paid = finance.EntriesOf(team).Where(entry => entry.ReasonKey == "finance.reason.win_bonus").ToArray();

        var line = Assert.Single(paid);
        Assert.Equal(-Money.FromDollars(WinBonusDollars).Cents, line.AmountCents);
        Assert.Equal(winner.DriverId, line.Counterparty);
        Assert.Equal(LedgerCategories.Salary, line.Category);
        var raceDay = finance.EntriesOf(team).Last(entry => entry.Category == LedgerCategories.StartMoney).Date;
        Assert.Equal(raceDay, line.Date);
    }

    /// <summary>Every active driver contract of the opening world gets the same bonuses, so whoever wins carries one.</summary>
    internal static WorldState WithBonuses(WorldState world, long winBonus, long titleBonus)
    {
        var section = world.Section<ContractsSection>(ContractsSection.SectionName) ?? ContractsSection.Empty;
        foreach (var contract in world.Contracts.Where(contract => contract.Role.IsDriver && contract.IsActiveOn(world.CurrentDate)))
        {
            var current = section.TermsOf(contract.Id);
            section = section.WithTerms(new ContractTerms(
                contract.Id,
                current?.PointsBonus ?? 0,
                winBonus,
                titleBonus,
                current?.OptionHolder,
                current?.Exit));
        }

        return world.WithSection(section);
    }
}
