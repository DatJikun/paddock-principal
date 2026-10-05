using Paddock.Application.Board;
using Paddock.Application.Contracts;
using Paddock.Application.Career;
using Paddock.Application.Finance;
using Paddock.Domain.Board;
using Paddock.Domain.Cars;
using Paddock.Domain.Career;
using Paddock.Domain.Development;
using Paddock.Domain.Finance;
using Paddock.Domain.Objectives;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Simulation.Career;

namespace Paddock.Tests.Career;

/// <summary>
/// #160: finance, sponsors, cars and the board in the career loop, on a real opening world (Chaos, 1955, the authored data) with
/// the data inputs of the tool. The 1955 world has no Jolpica results (PP-041), so the team tiers are the authored ESTIMATE.
/// </summary>
public sealed class CareerWiringTests
{
    private const ulong Seed = 7;

    [Fact]
    public void TheDayRunsEveryRegisteredHandlerInTheDocumentedOrder()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);

        CareerHost.RunUntil(session, new GameDate(1955, 1, 2), null, CareerKit.Options);

        // season change 5 (host), pool 10, ageing 20, last season 25, contract expiry 30, rollover 40 (the session), negotiations 700, contract lifecycle 710,
        // sponsors 750, supply 760, development 780, finance 800, objectives 900, board 910 (TECH 6.2).
        Assert.Equal([5, 10, 15, 20, 25, 30, 40, 700, 705, 710, 750, 760, 780, 800, 900, 910], session.DayHandlers.Select(handler => handler.Order).ToArray());
    }

    [Fact]
    public void TheModuleListIsTheDocumentedOneAndNamesAreUnique()
    {
        Assert.Equal(
            ["objectives", "finance", "contracts", "pool", "cars", "sponsors", "supply", "development", "board", "principals"],
            CareerModules.Default.Select(module => module.Name).ToArray());
    }

    [Fact]
    public void ANewCareerHasBooksTwoCarsAndABoardForEveryTeamBeforeTheFirstDay()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);

        CareerHost.RunUntil(session, session.Date, null, CareerKit.Options);

        var teams = CareerTeams.Active(session.World, session.Date);
        Assert.NotEmpty(teams);
        var finance = session.World.Section<FinanceSection>(FinanceSection.SectionName)!;
        var cars = session.World.Section<CarsSection>(CarsSection.SectionName)!;
        var board = session.World.Section<BoardSection>(BoardSection.SectionName)!;
        foreach (var team in teams)
        {
            Assert.True(finance.HasBook(team.Id), team.Id.Value + " has no books.");
            Assert.Equal(CarEstimates.CarsPerTeam, cars.Of(team.Id).Count);
            Assert.NotNull(board.Board(team.Id));
        }
    }

    [Fact]
    public void OpeningCapitalFollowsTheAuthoredTierEstimateAndMissingFactsMeanTypical()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);
        CareerHost.RunUntil(session, session.Date, null, CareerKit.Options);
        var facts = new EraPeriodFinance(CareerKit.Data.EraPeriods).Facts(1955);
        var finance = session.World.Section<FinanceSection>(FinanceSection.SectionName)!;

        long Opening(string team)
        {
            var first = finance.EntriesOf(OrganizationId.Real(team))[0];
            Assert.Equal(LedgerCategories.OwnerFunds, first.Category);
            return first.AmountCents;
        }

        Assert.Equal(Money.FromDollars(facts.TopDollars).Cents, Opening("mercedes"));
        Assert.Equal(Money.FromDollars(facts.TypicalDollars).Cents, Opening("ferrari"));
        Assert.Equal(Money.FromDollars(facts.LowDollars).Cents, Opening("gordini"));
        // pawl's only 1955 entries are the Indianapolis 500, so that constructor is not planned and has no book.
        // arzani-volpini is a 1955 Grand Prix team with no authored 1954 standing: missing facts mean Typical.
        Assert.Equal(Money.FromDollars(facts.TypicalDollars).Cents, Opening("arzani-volpini"));
        Assert.True(facts.TopDollars > facts.TypicalDollars && facts.TypicalDollars > facts.LowDollars);
    }

    [Fact]
    public void WithoutEraDataTheStandInsStayAndFinanceIsAbsent()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);

        var result = CareerHost.RunUntil(session, new GameDate(1955, 3, 1), null, new CareerRunOptions());

        Assert.Null(result.Modules.TryGet<IPayrollLedger>());
        Assert.Null(session.World.Section<FinanceSection>(FinanceSection.SectionName));
        Assert.DoesNotContain(session.DayHandlers, handler => handler.Order is 750 or 800);
        Assert.Contains(session.DayHandlers, handler => handler.Order == 910);
    }

    [Fact]
    public void TheLedgerPaysTheSalariesOfRenewedContractsAndStandsInForThePayroll()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);

        var result = CareerHost.Run(session, 1956, null, CareerKit.Options);

        Assert.IsType<FinancePayroll>(result.Modules.Require<IPayrollLedger>());
        var finance = session.World.Section<FinanceSection>(FinanceSection.SectionName)!;
        var salaries = CareerTeams.Active(session.World, session.Date)
            .SelectMany(team => finance.EntriesOf(team.Id))
            .Where(entry => entry.Category == LedgerCategories.Salary)
            .ToArray();
        Assert.NotEmpty(salaries);
        Assert.All(salaries, entry => Assert.True(entry.AmountCents < 0));
    }

    [Fact]
    public void EveryTeamIsPaidTheSamePlaceholderStartMoneyOnTheFirstDayOfASeason()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);

        CareerHost.RunUntil(session, new GameDate(1955, 1, 2), null, CareerKit.Options);

        var typical = new EraPeriodFinance(CareerKit.Data.EraPeriods).Facts(1955).TypicalDollars;
        var expected = Money.RoundDollars(typical * FinanceEstimates.StartMoneyShare).Cents;
        var finance = session.World.Section<FinanceSection>(FinanceSection.SectionName)!;
        var teams = CareerTeams.Active(session.World, session.Date);
        Assert.NotEmpty(teams);
        foreach (var team in teams)
        {
            var income = Assert.Single(finance.EntriesOf(team.Id), entry => entry.Category == LedgerCategories.StartMoney);
            Assert.Equal(expected, income.AmountCents);
            Assert.Equal(FinanceReason.StartMoney, income.ReasonKey);
        }
    }

    [Fact]
    public void WithThePlaceholderIncomeAnAiOnlyCareerDoesNotRunDryAndKeepsRenewingContracts()
    {
        // Measured without the income stand-in: every team is insolvent by 1958 and a Chaos career from 1950 has no contracts by 1959.
        // A8 does not plan Indianapolis-only constructors. The 1950 Chaos grid is then the Grand Prix teams (two generated
        // seats each) plus the unsigned new team. With the renewal placeholder, seed 7 ended 1960 with 16 live contracts.
        // The AI principals renew and sign through the decade (25 contracts at the end of 1960). They also spend, and alta's
        // book reaches the insolvency watch; the other books do not.
        var session = CareerKit.Open(CareerPreset.Chaos, 1950, Seed);

        CareerHost.Run(session, 1960, null, CareerKit.Options);

        var finance = session.World.Section<FinanceSection>(FinanceSection.SectionName)!;
        // The renewal placeholder did not spend. The principals do (development, supply, sponsors), so one backmarker
        // reaches the insolvency watch. The grid does not collapse: 25 contracts at the end of 1960, measured for seed 7.
        var insolvent = session.World.Organizations
            .Where(organization => finance.HasBook(organization.Id) && finance.IsInsolvent(organization.Id))
            .Select(organization => organization.Id.Value)
            .ToArray();
        Assert.Equal(["alta"], insolvent);
        Assert.True(session.Years[^1].Contracts >= 20, "contracts at the end of 1960: " + session.Years[^1].Contracts);
    }

    [Fact]
    public void TheCashOfATeamIsAnObjectiveFactInWholeDollars()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);

        var result = CareerHost.Run(session, 1955, null, CareerKit.Options);

        var facts = result.Modules.Require<IObjectiveFacts>();
        var finance = session.World.Section<FinanceSection>(FinanceSection.SectionName)!;
        var mercedes = OrganizationId.Real("mercedes");
        Assert.Equal(new Money(finance.BalanceOf(mercedes)).WholeDollars, facts.Number(mercedes, ObjectiveFactKeys.Cash));
        Assert.Null(facts.Number(OrganizationId.Real("nobody"), ObjectiveFactKeys.Cash));
    }

    [Fact]
    public void TheBoardAppliesAnObjectiveOutcomeBeforeTheObjectiveIsSettled()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);

        CareerHost.Run(session, 1955, null, CareerKit.Options);

        // The run has no standings, so a position objective cannot be shown to be met: it fails on 31 December. The board must have
        // seen it open to move its confidence, and it is settled afterwards.
        var objectives = session.World.Section<ObjectivesSection>(ObjectivesSection.SectionName)!;
        var board = session.World.Section<BoardSection>(BoardSection.SectionName)!;
        Assert.Empty(objectives.Due(new GameDate(1955, 12, 31)));
        Assert.All(
            board.Boards,
            record => Assert.True(
                record.ConfidenceTenths < BoardEstimates.InitialConfidenceTenths,
                record.Organization.Value + " kept its initial confidence although its season objective failed."));
    }

    [Fact]
    public void ASeasonRunTwiceGivesTheSameWorldHashAndTheNewSectionsAreInIt()
    {
        var first = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);
        var second = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);
        var other = CareerKit.Open(CareerPreset.Chaos, 1955, Seed + 1);

        CareerHost.Run(first, 1955, null, CareerKit.Options);
        CareerHost.Run(second, 1955, null, CareerKit.Options);
        CareerHost.Run(other, 1955, null, CareerKit.Options);

        Assert.Equal(first.World.StateHash(), second.World.StateHash());
        Assert.NotEqual(first.World.StateHash(), other.World.StateHash());
        var names = first.World.Sections.Select(section => section.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Superset(
            new HashSet<string>(["board", "cars", "contracts", "development", "finance", "objectives", "principals", "talent-pool"], StringComparer.Ordinal),
            names);
    }

    [Theory]
    [InlineData(1955, 1, 2)]
    [InlineData(1955, 3, 20)]
    [InlineData(1955, 7, 1)]
    [InlineData(1955, 12, 31)]
    [InlineData(1956, 1, 1)]
    public void ASaveInTheMiddleOfASeasonResumesToTheFutureThatNeverStopped(int year, int month, int day)
    {
        var directory = Directory.CreateTempSubdirectory("paddock-wiring-");
        try
        {
            var stop = new GameDate(1957, 1, 1);
            var whole = CareerKit.Opened(CareerPreset.Chaos, 1955, Seed);
            var wholeResult = CareerHost.RunUntil(whole.Session, stop, null, CareerKit.OptionsFor(whole));

            var first = CareerKit.Opened(CareerPreset.Chaos, 1955, Seed);
            var firstResult = CareerHost.RunUntil(first.Session, new GameDate(year, month, day), null, CareerKit.OptionsFor(first));
            var path = Path.Combine(directory.FullName, "split.paddock");
            CareerKit.Save(path, first, first.Session, firstResult.Host);
            var (resumed, host) = CareerKit.Resume(path);
            var resumedResult = CareerHost.RunUntil(resumed, stop, host, CareerKit.Options);

            Assert.Equal(stop, resumed.Date);
            Assert.Equal(whole.Session.World.StateHash(), resumed.World.StateHash());
            Assert.Equal(whole.Session.Years, resumed.Years);
            Assert.Equal(whole.Session.TalentPool, resumed.TalentPool);
            Assert.Equal(whole.Session.ContractExpiries, resumed.ContractExpiries);
            Assert.Equal(
                wholeResult.Host.Log.Entries.Select(command => command.GetType().Name + command.SubmissionNumber),
                resumedResult.Host.Log.Entries.Select(command => command.GetType().Name + command.SubmissionNumber));
            Assert.Equal(wholeResult.Host.NextSubmissionNumber, resumedResult.Host.NextSubmissionNumber);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ANewCareerInstallsOpeningSupplyDealsOnceAndTheSameRunTwiceMatches()
    {
        var first = CareerKit.Opened(CareerPreset.Chaos, 1955, Seed);
        var second = CareerKit.Opened(CareerPreset.Chaos, 1955, Seed);
        var firstResult = CareerHost.RunUntil(first.Session, new GameDate(1955, 1, 4), null, CareerKit.OptionsFor(first));
        CareerHost.RunUntil(second.Session, new GameDate(1955, 1, 4), null, CareerKit.OptionsFor(second));

        var supply = first.Session.World.Section<SupplySection>(SupplySection.SectionName);
        Assert.NotNull(supply);
        Assert.NotEmpty(supply.Deals);
        Assert.Equal(first.Session.World.StateHash(), second.Session.World.StateHash());

        var deals = supply.Deals.Select(deal => deal.Id).ToArray();
        var whole = CareerKit.Opened(CareerPreset.Chaos, 1955, Seed);
        CareerHost.RunUntil(whole.Session, new GameDate(1955, 1, 8), null, CareerKit.OptionsFor(whole));
        var directory = Directory.CreateTempSubdirectory("paddock-supply-");
        try
        {
            var path = Path.Combine(directory.FullName, "open.paddock");
            CareerKit.Save(path, first, first.Session, firstResult.Host);
            var (resumed, host) = CareerKit.Resume(path);
            CareerHost.RunUntil(resumed, new GameDate(1955, 1, 8), host, CareerKit.OptionsFor(whole));
            var again = resumed.World.Section<SupplySection>(SupplySection.SectionName);
            Assert.NotNull(again);
            var later = again.Deals.Select(deal => deal.Id).ToArray();
            Assert.Equal(deals, later.Take(deals.Length).ToArray());
            Assert.Equal(
                whole.Session.World.Section<SupplySection>(SupplySection.SectionName)!.Deals.Select(deal => deal.Id).ToArray(),
                later);
            Assert.Equal(whole.Session.World.StateHash(), resumed.World.StateHash());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void TheHostChangesTheSeasonOnTheFirstOfJanuaryAndDevelopmentFollows()
    {
        var opened = CareerKit.Opened(CareerPreset.Chaos, 1955, Seed);
        CareerHost.RunUntil(opened.Session, new GameDate(1956, 1, 2), null, CareerKit.OptionsFor(opened));

        Assert.Equal(new GameDate(1956, 1, 2), opened.Session.Date);
        var cars = opened.Session.World.Section<CarsSection>(CarsSection.SectionName);
        Assert.NotNull(cars);
        Assert.NotEmpty(cars.Cars);
        Assert.All(cars.Cars, car => Assert.Equal(1956, car.Season));
        var development = opened.Session.World.Section<DevelopmentSection>(DevelopmentSection.SectionName);
        Assert.NotNull(development);
        Assert.NotEmpty(development.Accounts);
        Assert.All(development.Accounts, account => Assert.Equal(1956, account.RulesYear));
    }

    [Fact]
    public void EverySectionOfACareerHasASaveStoreSoTheWholeWorldIsSaved()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);
        CareerHost.Run(session, 1955, null, CareerKit.Options);

        var stores = SectionStores.Production.Select(store => store.SectionName).ToHashSet(StringComparer.Ordinal);

        Assert.All(session.World.Sections, section => Assert.Contains(section.Name, stores));
    }
}
