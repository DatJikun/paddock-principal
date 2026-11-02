using AccessContext = Paddock.Application.Access.AccessContext;
using Paddock.Application.Career;
using Paddock.Application.Contracts;
using Paddock.Application.Finance;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Career;
using Paddock.Domain.Contracts;
using Paddock.Domain.Finance;
using Paddock.Domain.People;
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

    private const long TitleBonusDollars = 9_000;

    private static readonly GameDate Opening = new(1955, 1, 1);

    private static readonly OrganizationId Alfa = OrganizationId.Real("alfa");

    private static readonly OrganizationId Ferrari = OrganizationId.Real("ferrari");

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

    [Fact]
    public void TheDriversChampionsTeamPaysTheTitleBonusWhenTheSeasonIsSettled()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);
        session.StoreWorld(WithBonuses(session.World, 0, TitleBonusDollars));

        CareerHost.RunUntil(session, new GameDate(1956, 1, 2), null, CareerKit.Options);

        var finance = session.World.Section<FinanceSection>(FinanceSection.SectionName)!;
        var paid = finance.Rows().Where(row => row.Entry.ReasonKey == FinanceReason.TitleBonus).ToArray();
        var (payer, line) = Assert.Single(paid);
        Assert.Equal(-Money.FromDollars(TitleBonusDollars).Cents, line.AmountCents);
        Assert.Equal(new GameDate(1955, 12, 31), line.Date);
        Assert.Contains(session.World.Contracts, contract =>
            contract.OrganizationId.Value == payer && contract.PersonId.Value == line.Counterparty && contract.Role.IsDriver && contract.IsActiveOn(line.Date));
        var points = session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName)!.Races
            .Where(race => race.Season == 1955)
            .SelectMany(race => race.Rows)
            .GroupBy(row => row.DriverId)
            .ToDictionary(group => group.Key, group => group.Sum(row => decimal.Parse(row.Points, System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(points.Values.Max(), points[line.Counterparty!]);
        Assert.DoesNotContain(finance.Rows(), row => row.Entry.ReasonKey == FinanceReason.WinBonus);
    }

    [Fact]
    public void OnlyTheClassifiedWinnerUnderContractWithTheWinningTeamIsPaid()
    {
        var (world, fangio, ascari) = TwoTeamWorld(winBonus: 2_000, titleBonus: 0);
        var finance = Books();
        var race = new RaceResultsPublished(1955, 1, 7, FinanceEstimates.PromoterModel,
        [
            new RaceEntryResult(Alfa, fangio.Value, 1, true),
            new RaceEntryResult(Ferrari, ascari.Value, 2, true),
        ]);

        var (paid, bonuses) = finance.PayWinBonuses(world, race, new GameDate(1955, 1, 16));

        var bonus = Assert.Single(bonuses);
        Assert.Equal((Alfa, fangio, Money.FromDollars(2_000).Cents, FinanceReason.WinBonus), (bonus.Organization, bonus.Driver, bonus.AmountCents, bonus.ReasonKey));
        var line = paid.EntriesOf(Alfa)[^1];
        Assert.Equal((LedgerCategories.Salary, fangio.Value, -Money.FromDollars(2_000).Cents, FinanceReason.WinBonus), (line.Category, line.Counterparty, line.AmountCents, line.ReasonKey));
        Assert.Equal(finance.EntriesOf(Ferrari).Count, paid.EntriesOf(Ferrari).Count);

        var unclassified = new RaceResultsPublished(1955, 2, 7, FinanceEstimates.PromoterModel, [new RaceEntryResult(Alfa, fangio.Value, 1, false)]);
        Assert.Empty(finance.PayWinBonuses(world, unclassified, new GameDate(1955, 2, 6)).Paid);
        var forAnotherTeam = new RaceResultsPublished(1955, 3, 7, FinanceEstimates.PromoterModel, [new RaceEntryResult(Ferrari, fangio.Value, 1, true)]);
        Assert.Empty(finance.PayWinBonuses(world, forAnotherTeam, new GameDate(1955, 3, 6)).Paid);
        var afterTheContract = finance.PayWinBonuses(world, race, new GameDate(1956, 1, 15));
        Assert.Empty(afterTheContract.Paid);
    }

    [Fact]
    public void TheTitleBonusIsPaidOnlyForTheChampionAndOnlyIfTheContractPromisedIt()
    {
        var (world, fangio, ascari) = TwoTeamWorld(winBonus: 0, titleBonus: 10_000);
        var finance = Books();
        var decided = new GameDate(1955, 12, 31);
        var season = new SeasonEnded(1955, 7, [new ConstructorTitleRow(Alfa, 1, 30)], [Alfa],
        [
            new DriverTitleRow(fangio.Value, 1, 30),
            new DriverTitleRow(ascari.Value, 2, 20),
        ]);

        var (paid, bonuses) = finance.PayTitleBonuses(world, season, decided);

        var bonus = Assert.Single(bonuses);
        Assert.Equal((Alfa, fangio, FinanceReason.TitleBonus), (bonus.Organization, bonus.Driver, bonus.ReasonKey));
        Assert.Equal(finance.BalanceOf(Alfa) - Money.FromDollars(10_000).Cents, paid.BalanceOf(Alfa));
        Assert.Equal(finance.BalanceOf(Ferrari), paid.BalanceOf(Ferrari));

        var (noTerms, _, _) = TwoTeamWorld(winBonus: 0, titleBonus: 0);
        Assert.Empty(finance.PayTitleBonuses(noTerms, season, decided).Paid);
    }

    [Fact]
    public void TheHumanManagerIsToldAndTheLedgerLineNamesTheDriver()
    {
        var (world, fangio, _) = TwoTeamWorld(winBonus: 2_000, titleBonus: 0);
        var race = new RaceResultsPublished(1955, 1, 7, FinanceEstimates.PromoterModel, [new RaceEntryResult(Alfa, fangio.Value, 1, true)]);
        var day = new GameDate(1955, 1, 16);
        var (paid, bonuses) = Books().PayWinBonuses(world, race, day);
        world = world.WithSection(paid);
        var managers = new ManagerRegistry();
        managers.Register(new ManagerId("human:anna"), ManagerKind.Human, "Anna");
        managers.Register(new ManagerId("ai:alfa"), ManagerKind.Ai, "Alfa AI");
        var control = new ControlTable().Assign(new ManagerId("human:anna"), Alfa).Assign(new ManagerId("ai:alfa"), Alfa);
        var inbox = new InboxBook();

        ContractBonusNotices.Post(bonuses, world, day, inbox, managers, control, ("season", "1955"), ("round", "1"));

        var item = Assert.Single(inbox.Section.Items);
        Assert.Equal("human:anna", item.ManagerId);
        Assert.Equal(FinanceKeys.BonusNoticeKind, item.Kind);
        Assert.Equal(FinanceKeys.WinBonusNotice, item.SubjectKey);
        Assert.Equal("Juan Fangio", item.Arguments["driver"]);
        Assert.Equal("$2,000", item.Arguments["amount"]);
        Assert.Equal("1", item.Arguments["round"]);
        Assert.False(item.NeedsDecision);

        var view = Assert.IsType<FinanceView.Own>(FinanceQuery.Read(
            AccessContext.ForManager(new Paddock.Application.Access.ManagerId("human:anna")), Alfa, world, day, control, withLedger: true));
        var line = view.Ledger[0];
        Assert.Equal(FinanceKeys.WinBonusLine, line.Reason.Key);
        Assert.Equal("Juan Fangio", line.Reason.Parameters["driver"]);
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

    private static FinanceSection Books()
    {
        var facts = new EraFinanceFacts(FinanceEstimates.PromoterModel, 20_000, 60_000, 250_000);
        return FinanceSection.Empty.Open(Alfa, Opening, 60_000, facts).Open(Ferrari, Opening, 60_000, facts);
    }

    /// <summary>Alfa holds Fangio and Ferrari holds Ascari for 1955. Only Fangio's contract carries the given bonuses.</summary>
    private static (WorldState World, PersonId Fangio, PersonId Ascari) TwoTeamWorld(long winBonus, long titleBonus)
    {
        var world = WorldState.At(Opening);
        (world, _) = world.AddOrganization(Team(Alfa, "Alfa Romeo"));
        (world, _) = world.AddOrganization(Team(Ferrari, "Ferrari"));
        (world, var fangio) = world.AddPerson(Driver("Juan", "Fangio", "fangio"));
        (world, var ascari) = world.AddPerson(Driver("Alberto", "Ascari", "ascari"));
        (world, var contract) = world.AddContract(Seat(fangio, Alfa));
        (world, _) = world.AddContract(Seat(ascari, Ferrari));
        var terms = ContractsSection.Empty.WithTerms(new ContractTerms(contract, 0, winBonus, titleBonus, null, null));
        return (world.WithSection(terms), fangio, ascari);
    }

    private static ContractSpec Seat(PersonId person, OrganizationId team) =>
        new(person, team, ContractRole.Driver(SeatStatus.Equal), Opening, new GameDate(1955, 12, 31), 12_000, true, null, null);

    private static OrganizationSpec Team(OrganizationId organization, string name) =>
        new(OrganizationKind.Team, true, organization.Value, Opening, null, 0, [new OrganizationNameSpan(name, Opening, null)]);

    private static PersonSpec Driver(string given, string family, string id)
    {
        var flat = new DriverAttributes(10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10);
        return new PersonSpec(given, family, new GameDate(1911, 6, 24), "AR", true, id, [PersonRole.Driver], PersonTruth.FromDriver(flat, flat));
    }
}
