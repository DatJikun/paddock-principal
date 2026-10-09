using Paddock.Application.Access;
using Paddock.Application.Commands;
using Paddock.Application.Finance;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.Objectives;
using Paddock.Application.Pool;
using Paddock.Application.World;
using Paddock.Domain.Finance;
using Paddock.Domain.Pool;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Pool;
using Paddock.Simulation.Time;
using AccessManagerId = Paddock.Application.Access.ManagerId;
using ManagerId = Paddock.Application.Managers.ManagerId;

namespace Paddock.Tests.Pool;

/// <summary>
/// The academy of the first playtest (#268): each team recruits its own juniors, a limited number; a junior of one academy is on nobody else's list;
/// a programme changes only the speed toward the potential. Synthetic people, no calibrated number.
/// </summary>
public sealed class AcademyTests
{
    private static readonly ManagerId Anna = new("mgr-anna");

    private static readonly ManagerId Bram = new("mgr-bram");

    private static readonly DateOnly Today = new(1950, 1, 1);

    // ------------------------------------------------------------------ the market

    [Fact]
    public void EveryTeamSeesEveryJuniorOnTheMarket()
    {
        var lab = new Lab();
        var free = lab.Section.Members.Where(member => member.Academy is null).Select(member => member.HandleText).Order().ToArray();
        Assert.True(free.Length > PoolEstimates.AcademySlots, "The fixture pool must be bigger than an academy.");

        Assert.Equal(free, lab.View(Anna).Items.Select(item => item.Handle).Order().ToArray());
        Assert.Equal(free, lab.View(Bram).Items.Select(item => item.Handle).Order().ToArray());
        Assert.Equal(lab.Section.Members.Count, lab.View(null).Items.Count);
    }

    [Fact]
    public void ARecruitedJuniorLeavesTheMarketForEveryoneButHisAcademy()
    {
        var lab = new Lab();
        var handle = lab.Section.Members.First(member => member.Academy is null).HandleText;
        var before = lab.View(Bram).Items.Count;

        lab.Submit(Recruit(Anna, handle));

        Assert.Equal(before - 1, lab.View(Bram).Items.Count);
        Assert.DoesNotContain(lab.View(Bram).Items, item => item.Handle == handle);
        Assert.Contains(lab.View(Anna).Items, item => item.Handle == handle && item.InYourAcademy);
        Assert.Equal(before, lab.View(Anna).Items.Count);
    }

    [Fact]
    public void AHandleOpensItsPersonOnlyForTheTeamsThatSeeHimOnTheirList()
    {
        var lab = new Lab();
        var handle = lab.Section.Members.First(member => member.Academy is null).HandleText;
        var person = lab.Section.FindByHandle(handle)!.Id;
        var query = new PoolQuery(lab.Book, new Teams());
        Assert.Equal(person, query.Resolve(AccessContext.ForManager(new AccessManagerId(Anna.Value)), handle));
        Assert.Equal(person, query.Resolve(AccessContext.ForManager(new AccessManagerId(Bram.Value)), handle));

        lab.Submit(Recruit(Anna, handle));

        Assert.Equal(person, query.Resolve(AccessContext.ForManager(new AccessManagerId(Anna.Value)), handle));
        Assert.Null(query.Resolve(AccessContext.ForManager(new AccessManagerId(Bram.Value)), handle));
        Assert.Null(query.Resolve(AccessContext.ForManager(new AccessManagerId(Anna.Value)), "talent-0"));
        Assert.Null(query.Resolve(AccessContext.ForManager(new AccessManagerId(Anna.Value)), person.Value));
    }

    [Fact]
    public void WhatATeamKnowsAboutTheMarketIsStillItsOwnScoutingAndNotTheTruth()
    {
        var lab = new Lab();
        var handle = lab.Section.Members.First(member => member.Academy is null).HandleText;

        var item = lab.View(Anna).Items.Single(row => row.Handle == handle);

        Assert.Empty(item.Attributes);
        Assert.Null(item.Potential);
    }

    // ------------------------------------------------------------------ recruiting

    [Fact]
    public void ARecruitedJuniorBelongsToOneAcademyAndShowsOnNobodyElsesScreen()
    {
        var lab = new Lab();
        var handle = lab.Free().First();

        Assert.IsType<CommandResult.Accepted>(lab.Submit(Recruit(Anna, handle)));

        Assert.Equal(PoolKit.Alpha, lab.Section.FindByHandle(handle)!.Academy);
        var annaView = lab.View(Anna);
        var junior = Assert.Single(annaView.Items, item => item.Handle == handle);
        Assert.True(junior.InYourAcademy);
        Assert.NotNull(junior.SeasonsLeft);
        Assert.Equal(1, annaView.AcademyUsed);
        Assert.Equal(PoolEstimates.AcademySlots, annaView.AcademySlots);
        Assert.DoesNotContain(lab.View(Bram).Items, item => item.Handle == handle);
        Assert.Contains(lab.View(null).Items, item => item.Handle == handle);
    }

    [Fact]
    public void AnAcademyHasLimitedPlacesAndReleasingOneFreesIt()
    {
        var lab = new Lab();
        var offered = lab.Free();
        Assert.True(offered.Count > PoolEstimates.AcademySlots, "The fixture pool must have more members than the academy has places.");
        foreach (var handle in offered.Take(PoolEstimates.AcademySlots))
        {
            Assert.IsType<CommandResult.Accepted>(lab.Submit(Recruit(Anna, handle)));
        }

        var extra = offered[PoolEstimates.AcademySlots];
        Assert.Equal(PoolKeys.AcademyFull, Reason(lab.Submit(Recruit(Anna, extra))));
        Assert.Equal(PoolEstimates.AcademySlots, lab.Section.AcademyCount(PoolKit.Alpha));

        Assert.IsType<CommandResult.Accepted>(lab.Submit(Release(Anna, offered[0])));
        Assert.Null(lab.Section.FindByHandle(offered[0])!.Academy);
        Assert.IsType<CommandResult.Accepted>(lab.Submit(Recruit(Anna, extra)));
        Assert.Equal(PoolKeys.AlreadyRecruited, Reason(lab.Submit(Recruit(Anna, extra))));
    }

    [Fact]
    public void AnyJuniorOnTheMarketCanBeRecruitedByAnyTeamAndAnUnknownOneCannot()
    {
        var lab = new Lab();
        var free = lab.Free();

        Assert.IsType<CommandResult.Accepted>(lab.Submit(Recruit(Anna, free[0])));
        Assert.IsType<CommandResult.Accepted>(lab.Submit(Recruit(Bram, free[1])));
        Assert.Equal(PoolKeys.PersonUnknown, Reason(lab.Submit(Recruit(Anna, "talent-999"))));
        Assert.Equal(PoolKit.Alpha, lab.Section.FindByHandle(free[0])!.Academy);
        Assert.Equal(PoolKit.Bravo, lab.Section.FindByHandle(free[1])!.Academy);
        Assert.Equal(1, lab.Section.AcademyCount(PoolKit.Alpha));
        Assert.Equal(1, lab.Section.AcademyCount(PoolKit.Bravo));
    }

    [Fact]
    public void AnotherTeamsJuniorCannotBeRecruitedSignedOrPaidFor()
    {
        var lab = new Lab();
        var handle = lab.Free().First();
        lab.Submit(Recruit(Anna, handle));

        Assert.Equal(PoolKeys.TakenByAnother, Reason(lab.Submit(Recruit(Bram, handle))));
        Assert.Equal(
            PoolKeys.TakenByAnother,
            Reason(lab.Submit(new SignPoolDriverCommand { ManagerId = Bram, IssuedOn = Today, PersonHandle = handle, Role = PoolSigningRole.Junior })));
        Assert.Equal(
            PoolKeys.NotYourJunior,
            Reason(lab.Submit(new FundJuniorCommand { ManagerId = Bram, IssuedOn = Today, PersonHandle = handle, Programme = JuniorProgramme.CheapSlow })));
        Assert.Equal(PoolKeys.NotYourJunior, Reason(lab.Submit(Release(Bram, handle))));
        Assert.Equal(PoolKit.Alpha, lab.Section.FindByHandle(handle)!.Academy);
    }

    [Fact]
    public void AProgrammeIsPaidOnlyForAJuniorOfTheOwnAcademyAndReleasingLosesIt()
    {
        var lab = new Lab();
        var handle = lab.Free().First();

        Assert.Equal(
            PoolKeys.NotYourJunior,
            Reason(lab.Submit(new FundJuniorCommand { ManagerId = Anna, IssuedOn = Today, PersonHandle = handle, Programme = JuniorProgramme.CheapSlow })));

        lab.Submit(Recruit(Anna, handle));
        Assert.IsType<CommandResult.Accepted>(lab.Submit(new FundJuniorCommand { ManagerId = Anna, IssuedOn = Today, PersonHandle = handle, Programme = JuniorProgramme.ExpensiveFast }));
        Assert.Equal(JuniorProgramme.ExpensiveFast, lab.View(Anna).Items.Single(item => item.Handle == handle).YourFunding);

        lab.Submit(Release(Anna, handle));
        var member = lab.Section.FindByHandle(handle)!;
        Assert.Null(member.Funding);
        Assert.Null(member.Academy);
    }

    [Fact]
    public void ASeasonsLeftCountdownFollowsTheSameRuleAsTheDayHandler()
    {
        var entered = new PoolMember(PersonId.Real("p1"), 1, new GameDate(1950, 1, 1), null, PoolKit.Alpha);

        Assert.Equal(PoolEstimates.MaxSeasonsInPool, PoolQuery.SeasonsLeft(entered, 1932, 1950));
        Assert.Equal(1, PoolQuery.SeasonsLeft(entered, 1932, 1955));
        Assert.Equal(0, PoolQuery.SeasonsLeft(entered, 1932, 1956));

        // A junior who is old for the pool has less time: he lapses once he is older than the age limit.
        Assert.Equal(2, PoolQuery.SeasonsLeft(entered, 1950 - PoolEstimates.MaxAge - 1 + 2, 1950));
    }

    // ------------------------------------------------------------------ programmes

    [Fact]
    public void AProgrammeChangesOnlyTheSpeedTowardThePotentialNeverThePotential()
    {
        var truth = PoolKit.Truth(8, 6);
        foreach (var speed in new[] { 100, PoolEstimates.CheapSlowSpeedPercent, PoolEstimates.ExpensiveFastSpeedPercent })
        {
            var rng = new RngStream(RngStreamName.People, RngStreams.Derive(5, RngStreamName.People, 1950).State).DeriveChild("academy-test:" + speed);
            var developed = PoolDevelopment.Develop(truth, speed, rng);

            Assert.Equal(truth.Potential, developed.Potential);
            Assert.All(developed.Attributes, attribute => Assert.True(attribute.Value <= truth.Potential.First(ceiling => ceiling.Key == attribute.Key).Value));
        }
    }

    [Fact]
    public void TheFasterProgrammeIsAModestSpeedUpAndCostsMore()
    {
        Assert.True(PoolEstimates.SpeedPercent(JuniorProgramme.CheapSlow) > 100);
        Assert.True(PoolEstimates.SpeedPercent(JuniorProgramme.ExpensiveFast) > PoolEstimates.SpeedPercent(JuniorProgramme.CheapSlow));
        Assert.True(PoolEstimates.SpeedPercent(JuniorProgramme.ExpensiveFast) <= 150, "A programme is a modest speed-up, not a doubling.");
        Assert.True(PoolEstimates.CostShare(JuniorProgramme.ExpensiveFast) > PoolEstimates.CostShare(JuniorProgramme.CheapSlow));
    }

    [Fact]
    public void ThePriceOfAProgrammeIsAShareOfTheTeamsBudgetSoItMeansSomethingInEveryEra()
    {
        long Cost(int year, long typicalDollars)
        {
            var facts = new EraFinanceFacts("promoter_individual_deals", typicalDollars / 2, typicalDollars, typicalDollars * 3);
            var world = PoolKit.EmptyWorld().WithSection(FinanceSection.Empty.Open(PoolKit.Alpha, new GameDate(year, 1, 1), typicalDollars, facts));
            var funding = new FinanceJuniorFunding(new FinanceBook(() => world, next => world = next));
            return funding.Cost(JuniorProgramme.CheapSlow);
        }

        var fifties = Cost(1955, 60_000);
        var seventies = Cost(1976, 800_000);

        Assert.Equal((long)Math.Round(60_000 * PoolEstimates.CheapSlowCostShare), fifties);
        Assert.True(seventies > fifties * 10);
    }

    [Fact]
    public void TheViewShowsEveryProgrammeWithItsCostAndSpeedBeforeAnythingIsConfirmed()
    {
        var lab = new Lab();

        var view = lab.View(Anna);

        Assert.Equal(100, view.BaseSpeedPercent);
        Assert.Equal(2, view.Programmes.Count);
        foreach (var programme in view.Programmes)
        {
            Assert.Equal(PoolEstimates.SpeedPercent(programme.Programme), programme.SpeedPercent);
            Assert.Equal(Money.FromDollars(PoolEstimates.CostOf(programme.Programme)).Cents, programme.CostCents);
        }
    }

    // ------------------------------------------------------------------ nothing fails silently

    [Fact]
    public void ACareerThatLapsesInAnAcademyIsToldToTheTeamAndNotToAnyoneElse()
    {
        var lab = new Lab();
        var handle = lab.Free().First();
        lab.Submit(Recruit(Anna, handle));
        var member = lab.Section.FindByHandle(handle)!;
        lab.Holder.World = lab.Holder.World.WithSection(lab.Section.Lapse(member.Id, new GameDate(1956, 1, 1)));
        var inbox = new InboxBook(new InboxResolvers());
        var marker = new DomainEvent(new EventId("e1"), new GameDate(1956, 1, 1), PoolEventTypes.CareerLapsed, new MarkerPayload(member.Id.Value));

        var posted = PoolNotices.Apply(lab.Book, [marker], inbox, lab.Managers, new Teams());

        Assert.Equal(1, posted);
        var item = Assert.Single(inbox.Section.ItemsOf(Anna.Value));
        Assert.Equal(PoolKeys.InboxLapsedSubject, item.Draft.SubjectKey);
        Assert.Empty(inbox.Section.ItemsOf(Bram.Value));
        Assert.Equal(PoolKit.Alpha, lab.Section.Lapsed.Single().Academy);
    }

    [Fact]
    public void ALapsedMemberNobodyRecruitedPostsNothing()
    {
        var lab = new Lab();
        var member = lab.Section.Members.First();
        lab.Holder.World = lab.Holder.World.WithSection(lab.Section.Lapse(member.Id, new GameDate(1956, 1, 1)));
        var inbox = new InboxBook(new InboxResolvers());
        var marker = new DomainEvent(new EventId("e1"), new GameDate(1956, 1, 1), PoolEventTypes.CareerLapsed, new MarkerPayload(member.Id.Value));

        Assert.Equal(0, PoolNotices.Apply(lab.Book, [marker], inbox, lab.Managers, new Teams()));
    }

    // ------------------------------------------------------------------ the text of the section

    [Fact]
    public void ASectionWithNoAcademyHasTheTextItAlwaysHadAndOneWithAJuniorAddsTheLine()
    {
        var plain = TalentPoolSection.Empty.EnterAll([PersonId.Real("p1")], new GameDate(1950, 1, 1));
        var recruited = plain.Recruit(PersonId.Real("p1"), PoolKit.Alpha);

        Assert.DoesNotContain("academy", Canonical(plain), StringComparison.Ordinal);
        Assert.Contains("academy", Canonical(recruited), StringComparison.Ordinal);
        Assert.Equal(Canonical(plain), Canonical(recruited.Release(PersonId.Real("p1"))));
    }

    // ------------------------------------------------------------------ helpers

    private static string Canonical(TalentPoolSection section)
    {
        var writer = new CanonicalWriter();
        section.WriteCanonical(writer);
        return writer.ToString();
    }

    private static string Reason(CommandResult result) => Assert.IsType<CommandResult.Rejected>(result).Reason.Key;

    private static RecruitJuniorCommand Recruit(ManagerId manager, string handle) => new() { ManagerId = manager, IssuedOn = Today, PersonHandle = handle };

    private static ReleaseJuniorCommand Release(ManagerId manager, string handle) => new() { ManagerId = manager, IssuedOn = Today, PersonHandle = handle };

    private sealed class Teams : IManagerOrganizations
    {
        public OrganizationId? OrganizationOf(string managerId) => managerId switch
        {
            "mgr-anna" => PoolKit.Alpha,
            "mgr-bram" => PoolKit.Bravo,
            _ => null,
        };
    }

    private sealed class Holder
    {
        public required WorldState World { get; set; }
    }

    /// <summary>A pool of sixteen invented drivers, two managers with a team each, the academy commands and an unmetered funding port.</summary>
    private sealed class Lab
    {
        public Lab()
        {
            var world = PoolKit.EmptyWorld();
            var people = new List<PersonId>();
            for (var index = 1; index <= 16; index++)
            {
                (world, var id) = PoolKit.Add(world, PoolKit.RealDriver("d_" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), 1930 + (index % 4), current: 8, extra: 5));
                people.Add(id);
            }

            world = world.WithSection(TalentPoolSection.Empty.EnterAll(people, world.CurrentDate));
            Holder = new Holder { World = world };
            Book = new PoolBook(() => Holder.World, next => Holder.World = next);
            Managers = new ManagerRegistry();
            Managers.Register(Anna, ManagerKind.Human, "Anna");
            Managers.Register(Bram, ManagerKind.Human, "Bram");
        }

        public Holder Holder { get; }

        public PoolBook Book { get; }

        public ManagerRegistry Managers { get; }

        public TalentPoolSection Section => Book.Section;

        /// <summary>The members nobody has recruited, in handle order: what the market shows every team.</summary>
        public IReadOnlyList<string> Free() =>
            Section.Members.Where(member => member.Academy is null).OrderBy(member => member.Handle).Select(member => member.HandleText).ToArray();

        public PoolView View(ManagerId? manager) =>
            new PoolQuery(Book, new Teams()).View(manager is { } who ? AccessContext.ForManager(new AccessManagerId(who.Value)) : AccessContext.Developer);

        public CommandResult Submit(ICommand command)
        {
            var dispatcher = new CommandDispatcher();
            var teams = new Teams();
            dispatcher.Register(new RecruitJuniorHandler(Book, teams));
            dispatcher.Register(new ReleaseJuniorHandler(Book, teams));
            dispatcher.Register(new FundJuniorHandler(Book, teams));
            dispatcher.Register(new SignPoolDriverHandler(Book, teams, new OpenNegotiations()));
            var queue = new CommandQueue();
            queue.Enqueue(command);
            return dispatcher.DispatchAll(queue, new CommandContext(new StubWorldState(Today), Managers)).Single();
        }
    }

    private sealed class OpenNegotiations : IPoolNegotiations
    {
        public TranslationMessage? ValidateStart(OrganizationId organization, PersonId person, PoolSigningRole role, GameDate on) => null;

        public IReadOnlyList<IDomainEvent> Start(ManagerId manager, OrganizationId organization, PersonId person, PoolSigningRole role, GameDate on) => [];
    }
}

internal static class AcademyTestDates
{
    public static GameDate ToGameDate(this DateOnly date) => new(date.Year, date.Month, date.Day);
}
