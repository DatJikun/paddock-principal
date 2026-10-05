using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Finance;
using Paddock.Application.Managers;
using AccessManagerId = Paddock.Application.Access.ManagerId;
using Paddock.Application.World;
using Paddock.Data.Authored;
using Paddock.Domain.Finance;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Time;

namespace Paddock.Tests.Finance;

/// <summary>
/// T37 ledger, era revenue and popularity. Benchmark dollars are the authored ESTIMATE for 1950–57.
/// Finance draws no RNG stream.
/// </summary>
public class FinanceTests
{
    private static readonly GameDate Opening = new(1955, 1, 1);

    [Fact]
    public void TheLedgerBalanceIsTheSumAndEntriesAreNotMutated()
    {
        var alfa = OrganizationId.Real("alfa");
        var opened = FinanceSection.Empty.Open(alfa, Opening, 60_000, Facts());
        var first = opened.EntriesOf(alfa);
        var posted = opened.Post(alfa, Opening, LedgerCategories.PrizeMoney, "fangio", 12_500, FinanceReason.PrizeMoney);
        var spent = posted.Post(alfa, Opening, LedgerCategories.Salary, "fangio", -5_000, FinanceReason.Salary);

        Assert.Equal(first.Sum(entry => entry.AmountCents), opened.BalanceOf(alfa));
        Assert.Equal(spent.EntriesOf(alfa).Sum(entry => entry.AmountCents), spent.BalanceOf(alfa));
        Assert.Equal(Money.FromDollars(60_000).Cents, first[0].AmountCents);
        Assert.Equal(Money.FromDollars(60_000).Cents, opened.EntriesOf(alfa)[0].AmountCents);
        Assert.Equal(2, posted.EntriesOf(alfa).Count);
        Assert.Single(opened.EntriesOf(alfa));
        Assert.Equal(3, spent.EntriesOf(alfa).Count);
        Assert.All(typeof(LedgerEntry).GetProperties(), property => Assert.Null(property.SetMethod));

        var world = TeamWorld(alfa);
        var baseline = world.WithSection(spent).StateHash();
        Assert.Equal(baseline, world.WithSection(spent).StateHash());
        var changed = spent.Post(alfa, Opening, LedgerCategories.Other, null, 1, FinanceReason.OpeningCapital);
        Assert.NotEqual(baseline, world.WithSection(changed).StateHash());
        Assert.NotEqual(baseline, world.WithSection(spent.Post(alfa, Opening.AddDays(1), LedgerCategories.PrizeMoney, "fangio", 12_500, FinanceReason.PrizeMoney)).StateHash());
        Assert.NotEqual(baseline, world.WithSection(spent.Post(alfa, Opening, LedgerCategories.Sponsor, "fangio", 12_500, FinanceReason.PrizeMoney)).StateHash());
    }

    [Fact]
    public void At1955TheBenchmarksAreTwentySixtyAndTwoHundredFiftyThousandDollars()
    {
        var data = AuthoredDataLoader.Load(Path.Combine(RepoPaths.Root(), "data"));
        var facts = EraFinance.ForYear(data.EraPeriods, 1955);

        Assert.Equal(20_000, facts.LowDollars);
        Assert.Equal(60_000, facts.TypicalDollars);
        Assert.Equal(250_000, facts.TopDollars);
        Assert.Equal(FinanceEstimates.PromoterModel, facts.RevenueModel);

        var alfa = OrganizationId.Real("alfa");
        foreach (var (tier, dollars) in new[] { (TeamTier.Low, 20_000L), (TeamTier.Typical, 60_000L), (TeamTier.Top, 250_000L) })
        {
            var section = FinanceSection.Empty.Open(alfa, Opening, facts.Dollars(tier), facts);
            Assert.Equal(dollars, Money.FromCents(section.BalanceOf(alfa)).WholeDollars);
        }

        var blended = EraFinance.Dollars(data.EraPeriods, FinanceEstimates.BudgetLow, 1958);
        Assert.Equal((20_000L + 35_000L) / 2, blended);
    }

    [Fact]
    public void PopularityRisesForACloseTitleFightAndFallsForADominantSeason()
    {
        // These seasons publish no drivers' table, so the drivers' fight term is zero (PP-057 adds it only when the table is there).
        var close = new SeasonEnded(
            1955,
            8,
            [
                new ConstructorTitleRow(OrganizationId.Real("alfa"), 1, 50m),
                new ConstructorTitleRow(OrganizationId.Real("ferrari"), 2, 49m),
            ],
            Winners("alfa", "ferrari", "maserati", "mercedes", "gordini", "lancia", "vanwall", "cooper"));
        var dominant = new SeasonEnded(
            1955,
            8,
            [
                new ConstructorTitleRow(OrganizationId.Real("mercedes"), 1, 100m),
                new ConstructorTitleRow(OrganizationId.Real("ferrari"), 2, 10m),
            ],
            [OrganizationId.Real("mercedes")]);

        var baseline = FinanceEstimates.BaselinePopularityMilli;
        var afterClose = PopularityModel.Next(baseline, close);
        var afterDominant = PopularityModel.Next(baseline, dominant);
        Assert.True(afterClose > baseline);
        Assert.True(afterDominant < baseline);

        var section = FinanceSection.Empty.ApplySeason(close);
        Assert.Equal(afterClose, section.PopularityMilli);
        Assert.Equal(section.PopularityMilli, FinanceSection.Empty.ApplySeason(close).PopularityMilli);
    }

    [Fact]
    public void ADriversTitleFightRaisesPopularityLessThanAMultiTeamFightAndARunawayStaysNegative()
    {
        var baseline = FinanceEstimates.BaselinePopularityMilli;
        var multiTeam = PopularityModel.Next(baseline, Season(
            races: 20,
            constructors: [(1, 300m), (2, 290m)],
            winners: 8,
            drivers: [(1, 180m), (2, 175m)]));
        var oneTeamTwoDrivers = PopularityModel.Next(baseline, Season(
            races: 21,
            constructors: [(1, 400m), (2, 20m)],
            winners: 1,
            drivers: [(1, 200m), (2, 199m)]));
        var oneDriver = PopularityModel.Next(baseline, Season(
            races: 17,
            constructors: [(1, 200m), (2, 20m)],
            winners: 1,
            drivers: [(1, 180m), (2, 20m)]));

        Assert.True(multiTeam > oneTeamTwoDrivers);
        Assert.True(oneTeamTwoDrivers > oneDriver);
        Assert.True(multiTeam > baseline);
        Assert.True(oneTeamTwoDrivers > baseline);
        Assert.True(oneDriver < baseline);
        Assert.Equal(multiTeam, PopularityModel.Next(baseline, Season(20, [(1, 300m), (2, 290m)], 8, [(1, 180m), (2, 175m)])));
    }

    private static SeasonEnded Season(
        int races,
        (int Position, decimal Points)[] constructors,
        int winners,
        (int Position, decimal Points)[] drivers) =>
        new(
            2016,
            races,
            constructors.Select((row, index) => new ConstructorTitleRow(OrganizationId.Real("team-" + index), row.Position, row.Points)).ToArray(),
            Enumerable.Range(0, winners).Select(index => OrganizationId.Real("team-" + index)).ToArray(),
            drivers.Select((row, index) => new DriverTitleRow("driver-" + index, row.Position, row.Points)).ToArray());

    [Fact]
    public void InsolvencyFiresOnTheAnniversaryAndNotBefore()
    {
        var alfa = OrganizationId.Real("alfa");
        var broke = FinanceSection.Empty
            .Open(alfa, Opening, 100, Facts())
            .Post(alfa, new GameDate(1955, 3, 1), LedgerCategories.Other, null, -Money.FromDollars(500).Cents, FinanceReason.Termination);
        var start = new GameDate(1955, 3, 1);
        var (watched, none) = broke.ReviewInsolvency(start);
        Assert.Empty(none);
        Assert.False(watched.IsInsolvent(alfa));
        Assert.Equal(start, watched.OverdraftSince(alfa));

        var eve = FinanceSection.PlusOneSeason(start).AddDays(-1);
        var (still, stillNone) = watched.ReviewInsolvency(eve);
        Assert.Empty(stillNone);
        Assert.False(still.IsInsolvent(alfa));
        Assert.Same(watched, still);

        var (gone, fired) = watched.ReviewInsolvency(FinanceSection.PlusOneSeason(start));
        Assert.Equal(alfa, Assert.Single(fired));
        Assert.True(gone.IsInsolvent(alfa));

        var recovered = broke.Post(alfa, eve, LedgerCategories.OwnerFunds, null, Money.FromDollars(1_000).Cents, FinanceReason.OpeningCapital);
        var (cleared, clearNone) = recovered.ReviewInsolvency(eve);
        Assert.Empty(clearNone);
        Assert.Null(cleared.OverdraftSince(alfa));
    }

    [Fact]
    public void TheDayHandlerEmitsInsolvencyAfterOneSeasonAndDrawsNoRandomStream()
    {
        var alfa = OrganizationId.Real("alfa");
        var world = TeamWorld(alfa);
        var broke = FinanceSection.Empty
            .Open(alfa, Opening, 100, Facts())
            .Post(alfa, new GameDate(1955, 3, 1), LedgerCategories.Other, null, -Money.FromDollars(500).Cents, FinanceReason.Termination);
        world = world.WithSection(broke);
        var handler = new FinanceDayHandler(() => world, next => world = next);
        var registry = new DayHandlerRegistry([handler]);
        var start = new GameDate(1955, 3, 1);
        var anniversary = FinanceSection.PlusOneSeason(start);
        var clock = new WorldClockState(start, 99UL);
        DomainEvent? fired = null;
        while (true)
        {
            var living = clock.Date;
            var step = WorldClock.AdvanceDay(clock, registry);
            Assert.Empty(step.State.RngStates);
            var hit = step.Events.SingleOrDefault(item => item.TypeId == FinanceEventTypes.Insolvent);
            if (living < anniversary)
            {
                Assert.Null(hit);
            }
            else
            {
                fired = hit;
                break;
            }

            clock = step.State;
        }

        Assert.NotNull(fired);
        Assert.Equal(anniversary, fired.Date);
        var marker = Assert.IsType<MarkerPayload>(fired.Payload);
        Assert.Equal(alfa.Value, marker.Marker);
        Assert.True(world.Section<FinanceSection>(FinanceSection.SectionName)!.IsInsolvent(alfa));
    }

    [Fact]
    public void OwnViewShowsTheForecastAndAnotherOrganizationShowsNothing()
    {
        var alfa = OrganizationId.Real("alfa");
        var ferrari = OrganizationId.Real("ferrari");
        var (world, _) = TeamWorld(alfa).AddOrganization(Team(ferrari, "Ferrari"));
        var (withPerson, person) = world.AddPerson(Driver("fangio"));
        (withPerson, _) = withPerson.AddContract(new ContractSpec(
            person,
            alfa,
            ContractRole.Driver(SeatStatus.Equal),
            Opening,
            new GameDate(1955, 12, 31),
            12_000,
            true,
            null,
            null));
        var section = FinanceSection.Empty
            .Open(alfa, Opening, 60_000, Facts())
            .Open(ferrari, Opening, 20_000, Facts());
        world = withPerson.WithSection(section);
        var control = new ControlTable().Assign(new ManagerId("human:anna"), alfa);
        var before = world.StateHash();
        var own = FinanceQuery.Read(Paddock.Application.Access.AccessContext.ForManager(new AccessManagerId("human:anna")), alfa, world, Opening, control);
        var shown = Assert.IsType<FinanceView.Own>(own);
        Assert.Equal(Money.FromDollars(60_000).Cents, shown.CashCents);
        Assert.True(shown.ObligationsCents > 0);
        Assert.Equal(FinanceKeys.ViewForecast, shown.ForecastNote.Key);
        Assert.Equal(0, shown.LoanOffers);
        Assert.Equal(before, world.StateHash());
        Assert.Equal(before, world.StateHash());

        var other = FinanceQuery.Read(Paddock.Application.Access.AccessContext.ForManager(new AccessManagerId("human:anna")), ferrari, world, Opening, control);
        var hidden = Assert.IsType<FinanceView.Unknown>(other);
        Assert.Equal(FinanceKeys.ViewUnknown, hidden.Reason.Key);
        var ai = FinanceQuery.Read(Paddock.Application.Access.AccessContext.ForAi(new AccessManagerId("human:anna")), ferrari, world, Opening, control);
        Assert.IsType<FinanceView.Unknown>(ai);

        var developer = Assert.IsType<FinanceView.Own>(
            FinanceQuery.Read(Paddock.Application.Access.AccessContext.Developer, ferrari, world, Opening, control));
        Assert.Equal(Money.FromDollars(20_000).Cents, developer.CashCents);
        Assert.Equal(before, world.StateHash());
    }

    [Fact]
    public void PromoterDealsPayStartAndPrizeMoneyAndOtherModelsWarn()
    {
        var alfa = OrganizationId.Real("alfa");
        var ferrari = OrganizationId.Real("ferrari");
        var section = FinanceSection.Empty.Open(alfa, Opening, 60_000, Facts()).Open(ferrari, Opening, 60_000, Facts());
        var race = new RaceResultsPublished(
            1955,
            1,
            7,
            FinanceEstimates.PromoterModel,
            [
                new RaceEntryResult(alfa, "fangio", 1, true),
                new RaceEntryResult(ferrari, "ascari", 2, true),
            ]);
        var (paid, report) = section.ApplyRace(race, Money.FromDollars(60_000).Cents, Opening);
        Assert.False(report.UsedFallback);
        Assert.Null(report.WarningKey);
        var alfaPrize = paid.EntriesOf(alfa).Single(entry => entry.Category == LedgerCategories.PrizeMoney).AmountCents;
        var ferrariPrize = paid.EntriesOf(ferrari).Single(entry => entry.Category == LedgerCategories.PrizeMoney).AmountCents;
        Assert.True(alfaPrize > ferrariPrize);
        Assert.Equal(
            paid.EntriesOf(alfa).Single(entry => entry.Category == LedgerCategories.StartMoney).AmountCents,
            paid.EntriesOf(ferrari).Single(entry => entry.Category == LedgerCategories.StartMoney).AmountCents);
        Assert.True(paid.EntriesOf(alfa).Single(entry => entry.Category == LedgerCategories.RaceRunning).AmountCents < 0);

        var fallback = new RaceResultsPublished(1955, 1, 7, "concorde_1_fisa_foca_split", race.Entries);
        var (estimated, warned) = section.ApplyRace(fallback, Money.FromDollars(60_000).Cents, Opening);
        Assert.True(warned.UsedFallback);
        Assert.Equal(FinanceKeys.FallbackEstimate, warned.WarningKey);
        Assert.Equal(RevenueModels.FallbackWarningKey, estimated.WarningKey);
        Assert.DoesNotContain(estimated.EntriesOf(alfa), entry => entry.Category == LedgerCategories.StartMoney);
    }

    [Fact]
    public void SalariesArePostedOnTheFirstOfTheMonthAndSumToTheContract()
    {
        var alfa = OrganizationId.Real("alfa");
        var world = TeamWorld(alfa);
        var (withPerson, person) = world.AddPerson(Driver("fangio"));
        (world, _) = withPerson.AddContract(new ContractSpec(
            person,
            alfa,
            ContractRole.Driver(SeatStatus.Equal),
            Opening,
            new GameDate(1955, 12, 31),
            12_005,
            true,
            null,
            null));
        var section = FinanceSection.Empty.Open(alfa, Opening, 60_000, Facts());
        long paid = 0;
        for (var month = 1; month <= 12; month++)
        {
            var day = new GameDate(1955, month, 1);
            var next = section.AccrueSalaries(world, day);
            var line = Assert.Single(next.EntriesOf(alfa), entry => entry.Date == day && entry.Category == LedgerCategories.Salary);
            Assert.Equal(-FinanceSection.MonthSalaryCents(12_005, month), line.AmountCents);
            Assert.Same(section, section.AccrueSalaries(world, day.AddDays(1)));
            paid += -line.AmountCents;
            section = next;
        }

        Assert.Equal(Money.FromDollars(12_005).Cents, paid);
    }

    [Fact]
    public void TenSeasonsTwiceShareAHashAndTheHandlerStillDrawsNoStream()
    {
        var first = RunSeasons();
        var second = RunSeasons();
        Assert.Equal(first, second);

        var alfa = OrganizationId.Real("alfa");
        var world = TeamWorld(alfa).WithSection(FinanceSection.Empty.Open(alfa, Opening, 60_000, Facts()));
        var handler = new FinanceDayHandler(() => world, next => world = next, [new CarBuildHook(alfa)]);
        var clock = new WorldClockState(Opening, 5UL);
        var registry = new DayHandlerRegistry([handler]);
        for (var i = 0; i < 40; i++)
        {
            var step = WorldClock.AdvanceDay(clock, registry);
            clock = step.State;
            Assert.Empty(clock.RngStates);
        }

        Assert.Contains(world.Section<FinanceSection>(FinanceSection.SectionName)!.EntriesOf(alfa), entry => entry.Category == LedgerCategories.CarBuild);
    }

    [Fact]
    public void PreviousSeasonStandingPicksTheTier()
    {
        var tiers = new PreviousSeasonStandingTier(
        [
            new ConstructorStandingFact(1954, "mercedes", 1),
            new ConstructorStandingFact(1954, "ferrari", 2),
            new ConstructorStandingFact(1954, "gordini", 6),
        ]);
        Assert.Equal(TeamTier.Top, tiers.TierOf(OrganizationId.Real("mercedes"), 1955));
        Assert.Equal(TeamTier.Typical, tiers.TierOf(OrganizationId.Real("ferrari"), 1955));
        Assert.Equal(TeamTier.Low, tiers.TierOf(OrganizationId.Real("gordini"), 1955));
        Assert.Equal(TeamTier.Typical, tiers.TierOf(OrganizationId.Real("alfa"), 1955));
    }

    [Fact]
    public void OpeningBooksPostsTheTierBenchmarkAndAnotherManagerIsRefused()
    {
        var alfa = OrganizationId.Real("alfa");
        var world = TeamWorld(alfa);
        var book = new FinanceBook(() => world, next => world = next);
        var control = new ControlTable().Assign(new ManagerId("human:anna"), alfa);
        var data = AuthoredDataLoader.Load(Path.Combine(RepoPaths.Root(), "data"));
        var handler = new OpenBooksHandler(
            book,
            control,
            new EraPeriodFinance(data.EraPeriods),
            new PreviousSeasonStandingTier([new ConstructorStandingFact(1954, "alfa", 1)]));
        var context = Context();
        var command = new OpenBooksCommand
        {
            ManagerId = new ManagerId("human:anna"),
            IssuedOn = new DateOnly(1955, 1, 1),
            OrganizationId = "alfa",
        };
        Assert.Null(handler.Validate(command, context));
        var events = handler.Execute(command, context);
        var opened = Assert.IsType<BooksOpened>(Assert.Single(events));
        Assert.Equal(Money.FromDollars(250_000).Cents, opened.OpeningCents);
        Assert.Equal(opened.OpeningCents, world.Section<FinanceSection>(FinanceSection.SectionName)!.BalanceOf(alfa));
        Assert.Equal(FinanceKeys.BooksOpen, handler.Validate(command, context)!.Key);

        var stranger = command with { ManagerId = new ManagerId("human:bram") };
        Assert.Equal(FinanceKeys.NotYourOrganization, handler.Validate(stranger, context)!.Key);
    }

    [Fact]
    public void ASeasonBuiltFromStandingsUpdatesPopularity()
    {
        var rows = new[]
        {
            new StandingsRow(1, "alfa", 50m, 50m, [], false),
            new StandingsRow(2, "ferrari", 49m, 49m, [], false),
        };
        var season = FinanceStandings.Season(1955, 8, rows, ["alfa", "ferrari", "maserati", "mercedes", "gordini", "lancia", "vanwall", "cooper"]);
        var section = FinanceSection.Empty.ApplySeason(season);
        Assert.True(section.PopularityMilli > FinanceEstimates.BaselinePopularityMilli);
    }

    [Fact]
    public void PayrollAndJuniorFundingPostThroughTheLedger()
    {
        var alfa = OrganizationId.Real("alfa");
        var world = TeamWorld(alfa).WithSection(FinanceSection.Empty.Open(alfa, Opening, 1_000, Facts()));
        var book = new FinanceBook(() => world, next => world = next);
        var payroll = new FinancePayroll(book);
        var person = PersonId.Real("fangio");
        Assert.True(payroll.CanPay(alfa, 50_000, Opening));
        payroll.RecordCompensation(alfa, person, 400, Opening);
        Assert.Equal(Money.FromDollars(600).Cents, book.Section.BalanceOf(alfa));

        var funding = new FinanceJuniorFunding(book);
        Assert.Null(funding.Validate(alfa, funding.Cost(Domain.Pool.JuniorProgramme.CheapSlow), Opening));
        funding.Charge(alfa, funding.Cost(Domain.Pool.JuniorProgramme.CheapSlow), Paddock.Application.Pool.PoolKeys.FundingReason, Opening);
        Assert.True(book.Section.BalanceOf(alfa) < 0);
        Assert.Contains(book.Section.EntriesOf(alfa), entry => entry.Category == LedgerCategories.Development);
    }

    [Fact]
    public void ReasonKeysMatchTheTranslationConstants()
    {
        Assert.Equal(FinanceReason.OpeningCapital, FinanceKeys.OpeningCapital);
        Assert.Equal(FinanceReason.StartMoney, FinanceKeys.StartMoney);
        Assert.Equal(FinanceReason.PrizeMoney, FinanceKeys.PrizeMoney);
        Assert.Equal(FinanceReason.RaceRunning, FinanceKeys.RaceRunning);
        Assert.Equal(FinanceReason.Salary, FinanceKeys.Salary);
        Assert.Equal(FinanceReason.Termination, FinanceKeys.Termination);
        Assert.Equal(RevenueModels.FallbackWarningKey, FinanceKeys.FallbackEstimate);
    }

    [Fact]
    public void FinanceCommandsRoundTripThroughTheSaveCodec()
    {
        ICommand[] commands =
        [
            new OpenBooksCommand { ManagerId = new ManagerId("human:anna"), IssuedOn = new DateOnly(1955, 6, 1), OrganizationId = "alfa", SubmissionNumber = 4 },
            new ApplyRaceResultsCommand
            {
                ManagerId = new ManagerId("human:anna"),
                IssuedOn = new DateOnly(1955, 6, 1),
                SubmissionNumber = 5,
                Season = 1955,
                Round = 2,
                RacesInSeason = 7,
                Entries = "alfa|fangio|1|1;ferrari|ascari|2|0",
            },
            new ApplySeasonEndedCommand
            {
                ManagerId = new ManagerId("human:anna"),
                IssuedOn = new DateOnly(1955, 12, 31),
                SubmissionNumber = 6,
                Season = 1955,
                Races = 7,
                Constructors = "alfa|1|40",
                Winners = "alfa,ferrari",
                Drivers = "fangio|1|40;ascari|2|39",
            },
        ];
        foreach (var command in commands)
        {
            var encoded = CommandCodec.Production.Encode(command);
            var decoded = CommandCodec.Production.Decode(encoded.Tag, encoded.Text, command.ManagerId, command.SubmissionNumber, command.IssuedOn);
            Assert.Equal(command, decoded);
        }
    }

    private static string RunSeasons()
    {
        var alfa = OrganizationId.Real("alfa");
        var ferrari = OrganizationId.Real("ferrari");
        var world = TeamWorld(alfa);
        (world, _) = world.AddOrganization(Team(ferrari, "Ferrari"));
        var facts = Facts();
        var section = FinanceSection.Empty.Open(alfa, Opening, facts.TypicalDollars, facts).Open(ferrari, Opening, facts.LowDollars, facts);
        for (var year = 1955; year < 1965; year++)
        {
            var day = new GameDate(year, 1, 1);
            section = section.AccrueSalaries(world, day);
            var race = new RaceResultsPublished(
                year,
                1,
                7,
                FinanceEstimates.PromoterModel,
                [
                    new RaceEntryResult(alfa, "fangio", 1, true),
                    new RaceEntryResult(ferrari, "ascari", 2, true),
                ]);
            (section, _) = section.ApplyRace(race, Money.FromDollars(facts.TypicalDollars).Cents, new GameDate(year, 6, 1));
            section = section.ApplySeason(new SeasonEnded(
                year,
                7,
                [
                    new ConstructorTitleRow(alfa, 1, 40m + year - 1955),
                    new ConstructorTitleRow(ferrari, 2, 30m),
                ],
                [alfa, ferrari]));
            (section, _) = section.ReviewInsolvency(new GameDate(year, 12, 31));
            world = world.WithDate(new GameDate(year, 12, 31)).WithSection(section);
        }

        return world.StateHash();
    }

    private static CommandContext Context()
    {
        var managers = new ManagerRegistry();
        managers.Register(new ManagerId("human:anna"), ManagerKind.Human, "Anna");
        managers.Register(new ManagerId("human:bram"), ManagerKind.Human, "Bram");
        return new CommandContext(new StubWorldState(new DateOnly(1955, 1, 1)), managers);
    }

    private static EraFinanceFacts Facts() => new(FinanceEstimates.PromoterModel, 20_000, 60_000, 250_000);

    private static WorldState TeamWorld(OrganizationId organization)
    {
        var (world, _) = WorldState.At(Opening).AddOrganization(Team(organization, "Alfa Romeo"));
        return world;
    }

    private static OrganizationSpec Team(OrganizationId organization, string name) => new(
        OrganizationKind.Team,
        true,
        organization.Value,
        Opening,
        null,
        0,
        [new OrganizationNameSpan(name, Opening, null)]);

    private static PersonSpec Driver(string id)
    {
        var flat = new DriverAttributes(10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10);
        return new PersonSpec("Juan", "Fangio", new GameDate(1911, 6, 24), "AR", true, id, [PersonRole.Driver], PersonTruth.FromDriver(flat, flat));
    }

    private static OrganizationId[] Winners(params string[] ids) => ids.Select(OrganizationId.Real).ToArray();

    private sealed class CarBuildHook : ILedgerSource
    {
        private readonly OrganizationId _organization;
        private bool _sent;

        public CarBuildHook(OrganizationId organization) => _organization = organization;

        public IReadOnlyList<LedgerDraft> Due(GameDate today)
        {
            if (_sent || today != Opening)
            {
                return [];
            }

            _sent = true;
            return [new LedgerDraft(_organization, LedgerCategories.CarBuild, null, -1_000, FinanceReason.OpeningCapital)];
        }
    }
}
