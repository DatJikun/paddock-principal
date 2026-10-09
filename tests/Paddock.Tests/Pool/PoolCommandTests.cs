using Paddock.Application.Access;
using Paddock.Application.Commands;
using Paddock.Application.Managers;
using Paddock.Application.Objectives;
using Paddock.Application.Pool;
using Paddock.Application.World;
using Paddock.Domain.Pool;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;
using AccessManagerId = Paddock.Application.Access.ManagerId;
using ManagerId = Paddock.Application.Managers.ManagerId;

namespace Paddock.Tests.Pool;

/// <summary>
/// The pool commands and the pool query (T40). Managers, teams and people are synthetic fixtures. The funding and the
/// negotiation are fakes of the ports that finance (T37) and contracts (T39) will implement.
/// </summary>
public class PoolCommandTests
{
    private static readonly ManagerId Anna = new("mgr-anna");

    private static readonly ManagerId Bram = new("mgr-bram");

    private static readonly ManagerId Ghost = new("mgr-ghost");

    private static readonly ManagerId Bot = new("ai-bot");

    private static readonly DateOnly Today = new(1950, 1, 1);

    [Fact]
    public void ScoutFocusOnOnePersonIsStoredAndReplacedByAPoolFocus()
    {
        var lab = new Lab();
        var handle = lab.HandleOf("d_a");

        var result = lab.Submit(new AssignScoutFocusCommand { ManagerId = Anna, IssuedOn = Today, PersonHandle = handle });

        Assert.IsType<CommandResult.Accepted>(result);
        var focus = lab.Section.FocusOf(PoolKit.Alpha)!;
        Assert.Equal(ScoutFocusKind.Person, focus.Kind);
        Assert.Equal("d_a", focus.Person!.Value.Value);
        Assert.Null(lab.Section.FocusOf(PoolKit.Bravo));

        lab.Submit(new AssignScoutFocusCommand { ManagerId = Anna, IssuedOn = Today });
        Assert.Equal(ScoutFocusKind.Pool, lab.Section.FocusOf(PoolKit.Alpha)!.Kind);
        var view = lab.Query.View(AccessContext.ForManager(new AccessManagerId(Anna.Value)));
        Assert.Equal(ScoutFocusKind.Pool, view.Focus);
        Assert.Null(view.FocusHandle);
    }

    [Fact]
    public void ARejectedCommandChangesNothing()
    {
        var lab = new Lab();
        var before = lab.World.StateHash();

        var unknown = lab.Submit(new AssignScoutFocusCommand { ManagerId = Anna, IssuedOn = Today, PersonHandle = "talent-99" });
        var real = lab.Submit(new AssignScoutFocusCommand { ManagerId = Anna, IssuedOn = Today, PersonHandle = "d_a" });
        var noTeam = lab.Submit(new AssignScoutFocusCommand { ManagerId = Ghost, IssuedOn = Today });
        var noFunds = lab.Submit(new FundJuniorCommand { ManagerId = Anna, IssuedOn = Today, PersonHandle = "gen:1", Programme = JuniorProgramme.CheapSlow });

        Assert.Equal(PoolKeys.PersonUnknown, Reason(unknown));
        Assert.Equal(PoolKeys.PersonUnknown, Reason(real));
        Assert.Equal(PoolKeys.NoOrganization, Reason(noTeam));
        Assert.Equal(PoolKeys.PersonUnknown, Reason(noFunds));
        Assert.Equal(before, lab.World.StateHash());
        Assert.Empty(lab.Funding.Charges);
    }

    [Fact]
    public void FundingAJuniorChargesTheTeamOnceAndRecordsTheSeason()
    {
        var lab = new Lab();
        var handle = lab.HandleOf("d_a");
        lab.PutInAcademy("d_a", PoolKit.Alpha);

        var result = lab.Submit(new FundJuniorCommand { ManagerId = Anna, IssuedOn = Today, PersonHandle = handle, Programme = JuniorProgramme.ExpensiveFast });

        Assert.IsType<CommandResult.Accepted>(result);
        var charge = Assert.Single(lab.Funding.Charges);
        Assert.Equal((PoolKit.Alpha, PoolEstimates.CostOf(JuniorProgramme.ExpensiveFast), PoolKeys.FundingReason), (charge.Payer, charge.Amount, charge.Reason));
        var funding = lab.Section.Find(PersonId.Real("d_a"))!.Funding!;
        Assert.Equal((PoolKit.Alpha, JuniorProgramme.ExpensiveFast, 1950), (funding.Funder, funding.Programme, funding.Season));

        var again = lab.Submit(new FundJuniorCommand { ManagerId = Anna, IssuedOn = Today, PersonHandle = handle, Programme = JuniorProgramme.CheapSlow });
        Assert.Equal(PoolKeys.AlreadyFunded, Reason(again));
        var rival = lab.Submit(new FundJuniorCommand { ManagerId = Bram, IssuedOn = Today, PersonHandle = handle, Programme = JuniorProgramme.CheapSlow });
        Assert.Equal(PoolKeys.NotYourJunior, Reason(rival));
        Assert.Single(lab.Funding.Charges);
        var event0 = Assert.IsType<JuniorFunded>(Assert.Single(((CommandResult.Accepted)result).Events));
        Assert.Equal((handle, JuniorProgramme.ExpensiveFast), (event0.PersonHandle, event0.Programme));
    }

    [Fact]
    public void ATeamThatCannotPayIsRefusedWithTheFinanceReasonAndNothingIsRecorded()
    {
        var lab = new Lab { Broke = true };
        var handle = lab.HandleOf("d_a");
        lab.PutInAcademy("d_a", PoolKit.Alpha);

        var result = lab.Submit(new FundJuniorCommand { ManagerId = Anna, IssuedOn = Today, PersonHandle = handle, Programme = JuniorProgramme.CheapSlow });

        Assert.Equal("test.finance.noMoney", Reason(result));
        Assert.Null(lab.Section.Find(PersonId.Real("d_a"))!.Funding);
        Assert.Empty(lab.Funding.Charges);
    }

    [Fact]
    public void WithoutFinanceTheDefaultPortPricesFromTheEstimatesAndRecordsNothing()
    {
        var funding = UnmeteredJuniorFunding.Instance;
        Assert.Equal(PoolEstimates.CheapSlowCost, funding.Cost(JuniorProgramme.CheapSlow));
        Assert.Null(funding.Validate(PoolKit.Alpha, long.MaxValue, new GameDate(1950, 1, 1)));
        funding.Charge(PoolKit.Alpha, 5, PoolKeys.FundingReason, new GameDate(1950, 1, 1));
    }

    [Fact]
    public void SigningStartsANegotiationWithThePortAndTheRoleChosen()
    {
        var lab = new Lab();
        var handle = lab.HandleOf("d_a");

        var result = lab.Submit(new SignPoolDriverCommand { ManagerId = Anna, IssuedOn = Today, PersonHandle = handle, Role = PoolSigningRole.Junior });

        Assert.IsType<CommandResult.Accepted>(result);
        var started = Assert.Single(lab.Negotiations.Started);
        Assert.Equal((PoolKit.Alpha, PersonId.Real("d_a"), PoolSigningRole.Junior), (started.Team, started.Person, started.Role));
        var events = ((CommandResult.Accepted)result).Events;
        Assert.Contains(events, e => e is PoolSigningStarted { Role: PoolSigningRole.Junior });
        Assert.Contains(events, e => e.TypeId == "test.negotiation.started");
    }

    [Fact]
    public void SigningIsRefusedWhenTheNegotiationPortSaysNoAndByDefaultUntilContractsExist()
    {
        var lab = new Lab();
        lab.Negotiations.Refuse = true;
        var refused = lab.Submit(new SignPoolDriverCommand { ManagerId = Anna, IssuedOn = Today, PersonHandle = lab.HandleOf("d_a"), Role = PoolSigningRole.Test });
        Assert.Equal("test.negotiation.closed", Reason(refused));
        Assert.Empty(lab.Negotiations.Started);

        var defaults = new Lab { UseDefaultPorts = true };
        var unavailable = defaults.Submit(new SignPoolDriverCommand { ManagerId = Anna, IssuedOn = Today, PersonHandle = defaults.HandleOf("d_a"), Role = PoolSigningRole.Test });
        Assert.Equal(PoolKeys.SigningUnavailable, Reason(unavailable));
    }

    [Fact]
    public void CommandsCarryAManagerAndTheSessionStoresWhatTheyChange()
    {
        var world = PoolKit.EmptyWorld();
        (world, var id) = PoolKit.Add(world, PoolKit.RealDriver("d_session", 1932));
        var session = new CareerSession(world, 3, [id], []);
        var book = PoolBook.ForSession(session);
        var handle = session.Pool.Find(id)!.HandleText;
        var managers = new ManagerRegistry();
        managers.Register(Anna, ManagerKind.Human, "Anna");
        var dispatcher = new CommandDispatcher();
        dispatcher.Register(new AssignScoutFocusHandler(book, new Teams()));
        var queue = new CommandQueue();
        queue.Enqueue(new AssignScoutFocusCommand { ManagerId = Anna, IssuedOn = Today, PersonHandle = handle });

        var results = dispatcher.DispatchAll(queue, new CommandContext(new StubWorldState(Today), managers));

        Assert.IsType<CommandResult.Accepted>(Assert.Single(results));
        Assert.Equal(ScoutFocusKind.Person, session.Pool.FocusOf(PoolKit.Alpha)!.Kind);
        Assert.Single(dispatcher.Log.Entries);
        Assert.Equal(Anna, dispatcher.Log.Entries[0].ManagerId);
    }

    [Fact]
    public void ReplayingTheSameCommandsOnTheSameWorldGivesTheSameHash()
    {
        string Play()
        {
            var lab = new Lab();
            var a = lab.HandleOf("d_a");
            lab.Submit(new AssignScoutFocusCommand { ManagerId = Anna, IssuedOn = Today, PersonHandle = a });
            lab.Submit(new FundJuniorCommand { ManagerId = Bram, IssuedOn = Today, PersonHandle = lab.HandleOf("d_b"), Programme = JuniorProgramme.CheapSlow });
            lab.Submit(new AssignScoutFocusCommand { ManagerId = Bram, IssuedOn = Today });
            return lab.World.StateHash();
        }

        Assert.Equal(Play(), Play());
    }

    // The query and what a viewer may learn (INV-003).

    [Fact]
    public void PoolViewShowsNamesNationalityAgeAndNothingElseUntilScoutingStarts()
    {
        var lab = new Lab();

        var view = lab.Query.View(AccessContext.ForManager(new AccessManagerId(Anna.Value)));

        // A team sees the whole market: every member nobody has recruited (#268).
        Assert.Equal(lab.Section.Members.Where(member => member.Academy is null).Select(member => member.HandleText).Order().ToArray(), view.Items.Select(i => i.Handle).Order().ToArray());
        Assert.NotEmpty(view.Items);
        Assert.All(view.Items, item =>
        {
            var person = lab.World.GetPerson(lab.Section.FindByHandle(item.Handle)!.Id);
            Assert.Equal((person.FamilyName, person.Nationality, person.AgeOn(lab.World.CurrentDate)), (item.FamilyName, item.Nationality, item.Age));
            Assert.Empty(item.Attributes);
            Assert.Null(item.Potential);
            Assert.False(item.InYourAcademy);
            Assert.Null(item.SeasonsLeft);
        });
        Assert.All(view.Items, i => Assert.StartsWith(TalentPoolSection.HandlePrefix, i.Handle, StringComparison.Ordinal));
    }

    [Fact]
    public void PoolViewTypesHaveNoRoomForTruthPotentialOrIsReal()
    {
        var types = new[] { typeof(PoolView), typeof(PoolItemView), typeof(PoolBandView) };
        var forbidden = new[] { typeof(Person), typeof(PersonTruth), typeof(PersonId), typeof(PersonKnowledge) };
        foreach (var type in types)
        {
            foreach (var property in type.GetProperties())
            {
                Assert.DoesNotContain("real", property.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("truth", property.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(property.PropertyType, forbidden);
                Assert.False(property.PropertyType.IsGenericType && property.PropertyType.GetGenericArguments().Any(forbidden.Contains));
            }
        }

        // The potential is a band of the viewer's own belief, never a number.
        Assert.Equal(typeof(PoolBandView), typeof(PoolItemView).GetProperty(nameof(PoolItemView.Potential))!.PropertyType);
    }

    [Fact]
    public void ViewsNeverRevealWhoIsRealNotInHandlesNorInAnythingElse()
    {
        var lab = new Lab();
        lab.Scout(Anna, ScoutFocusKind.Pool, months: 12);
        var view = lab.Query.View(AccessContext.ForManager(new AccessManagerId(Anna.Value)));
        var text = string.Join("|", view.Items.Select(i => string.Join(",", i.Handle, i.GivenName, i.FamilyName, i.Nationality, i.Age)
            + string.Join(",", i.Attributes.Select(a => a.Key + a.Low + a.High)) + i.Potential));

        foreach (var person in lab.World.Persons)
        {
            Assert.DoesNotContain(person.Id.Value, text, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("gen:", text, StringComparison.Ordinal);
        var shape = view.Items.Select(i => System.Text.RegularExpressions.Regex.Replace(i.Handle, "[0-9]+", "#")).Distinct().ToArray();
        Assert.Equal(["talent-#"], shape);
        var real = lab.World.Persons.Where(p => p.IsReal).Select(p => p.GivenName).ToHashSet();
        Assert.Equal(lab.Section.Members.Count(member => member.Academy is null && lab.World.GetPerson(member.Id).IsReal), view.Items.Count(i => real.Contains(i.GivenName)));
    }

    [Fact]
    public void BandsAreTheViewersOwnBeliefAndNotThePotentialOrOtherTeamsKnowledge()
    {
        var lab = new Lab();
        lab.Scout(Anna, ScoutFocusKind.Pool, months: 12);

        var anna = lab.Query.View(AccessContext.ForManager(new AccessManagerId(Anna.Value)));
        var bram = lab.Query.View(AccessContext.ForManager(new AccessManagerId(Bram.Value)));
        var developer = lab.Query.View(AccessContext.Developer);

        Assert.All(anna.Items, item =>
        {
            Assert.Equal(11, item.Attributes.Count);
            Assert.All(item.Attributes, band => Assert.InRange(band.Low, 1, band.High));
            Assert.NotNull(item.Potential);
        });
        Assert.All(bram.Items, item => Assert.Empty(item.Attributes));
        Assert.All(developer.Items, item => Assert.Empty(item.Attributes));
        Assert.Equal(4, developer.Items.Count);

        // The band holds the true current value for a perfect scout but is not it, and the potential is a band too.
        var shown = anna.Items.First();
        var shownTruth = lab.World.TruthOf(lab.Section.FindByHandle(shown.Handle)!.Id);
        Assert.All(shown.Attributes, band => Assert.InRange(shownTruth.Value(band.Key), band.Low, band.High));
        Assert.All(shown.Attributes, band => Assert.True(band.High > band.Low));
        Assert.True(shown.Potential!.High > shown.Potential.Low);
    }

    [Fact]
    public void AiKnowledgeComesOnlyFromItsOwnScoutingAsBandsAndSaysNothingOfRealness()
    {
        var lab = new Lab();
        var handle = lab.HandleOf("d_a");
        var viewer = AccessContext.ForAi(new AccessManagerId(Bot.Value));
        var knowledge = new PoolKnowledgeView(viewer, lab.Book, new Teams());

        Assert.Equal(KnownKind.Unknown, knowledge.Get(new FactKey("pool." + handle + ".cornering")).Kind);

        lab.Scout(Bot, ScoutFocusKind.Pool, months: 12);
        var cornering = knowledge.Get(new FactKey("pool." + handle + ".cornering"));
        var potential = knowledge.Get(new FactKey("pool." + handle + ".potential"));
        Assert.Equal(KnownKind.Banded, cornering.Kind);
        Assert.Equal(KnownKind.Banded, potential.Kind);
        Assert.False(cornering.TryGetExact(out _));
        Assert.False(potential.TryGetExact(out _));
        Assert.True(cornering.TryGetBand(out var band));
        Assert.InRange(lab.World.TruthOf(PersonId.Real("d_a")).Value("cornering"), band.Min, band.Max);

        foreach (var fact in new[] { "pool." + handle + ".isReal", "pool." + handle + ".real", "pool.d_a.cornering", "pool." + handle, "other." + handle + ".cornering", "pool." + handle + ".nonsense" })
        {
            Assert.Equal(KnownKind.Unknown, knowledge.Get(new FactKey(fact)).Kind);
        }

        var other = new PoolKnowledgeView(AccessContext.ForManager(new AccessManagerId(Anna.Value)), lab.Book, new Teams());
        Assert.Equal(KnownKind.Unknown, other.Get(new FactKey("pool." + handle + ".cornering")).Kind);
        var developer = new PoolKnowledgeView(AccessContext.Developer, lab.Book, new Teams());
        Assert.Equal(KnownKind.Unknown, developer.Get(new FactKey("pool." + handle + ".cornering")).Kind);
    }

    [Fact]
    public void QueriesChangeNothingAndDrawNoRandomness()
    {
        var lab = new Lab();
        lab.Scout(Anna, ScoutFocusKind.Pool, months: 6);
        var before = lab.World.StateHash();

        for (var i = 0; i < 3; i++)
        {
            _ = lab.Query.View(AccessContext.ForManager(new AccessManagerId(Anna.Value)));
            _ = lab.Query.View(AccessContext.Developer);
            _ = new PoolKnowledgeView(AccessContext.ForAi(new AccessManagerId(Anna.Value)), lab.Book, new Teams())
                .Get(new FactKey("pool." + lab.HandleOf("d_a") + ".cornering"));
        }

        Assert.Equal(before, lab.World.StateHash());
    }

    [Fact]
    public void AMemberWhoLeftThePoolIsNoLongerNamedByHisHandle()
    {
        var lab = new Lab();
        var handle = lab.HandleOf("d_a");
        lab.Holder.World = lab.Holder.World.WithSection(lab.Section.Leave(PersonId.Real("d_a")));

        var result = lab.Submit(new AssignScoutFocusCommand { ManagerId = Anna, IssuedOn = Today, PersonHandle = handle });

        Assert.Equal(PoolKeys.PersonUnknown, Reason(result));
        Assert.DoesNotContain(lab.Query.View(AccessContext.Developer).Items, item => item.Handle == handle);
    }

    private static string Reason(CommandResult result) => Assert.IsType<CommandResult.Rejected>(result).Reason.Key;

    /// <summary>Anna runs alpha, Bram runs bravo and the bot runs alpha's rival (also bravo); Ghost runs nothing.</summary>
    private sealed class Teams : IManagerOrganizations
    {
        public OrganizationId? OrganizationOf(string managerId) => managerId switch
        {
            "mgr-anna" => PoolKit.Alpha,
            "mgr-bram" => PoolKit.Bravo,
            "ai-bot" => PoolKit.Bravo,
            _ => null,
        };
    }

    private sealed class Holder
    {
        public required WorldState World { get; set; }
    }

    private sealed class FakeFunding : IJuniorFunding
    {
        public bool Broke { get; set; }

        public List<(OrganizationId Payer, long Amount, string Reason)> Charges { get; } = [];

        public long Cost(JuniorProgramme programme) => PoolEstimates.CostOf(programme);

        public TranslationMessage? Validate(OrganizationId payer, long amount, GameDate on) =>
            Broke ? TranslationMessage.Of("test.finance.noMoney") : null;

        public void Charge(OrganizationId payer, long amount, string reasonKey, GameDate on) => Charges.Add((payer, amount, reasonKey));
    }

    private sealed class FakeNegotiations : IPoolNegotiations
    {
        public bool Refuse { get; set; }

        public List<(OrganizationId Team, PersonId Person, PoolSigningRole Role)> Started { get; } = [];

        public TranslationMessage? ValidateStart(OrganizationId organization, PersonId person, PoolSigningRole role, GameDate on) =>
            Refuse ? TranslationMessage.Of("test.negotiation.closed") : null;

        public IReadOnlyList<IDomainEvent> Start(ManagerId manager, OrganizationId organization, PersonId person, PoolSigningRole role, GameDate on)
        {
            Started.Add((organization, person, role));
            return [new Started(manager, new DateOnly(on.Year, on.Month, on.Day))];
        }
    }

    private sealed record Started(ManagerId ManagerId, DateOnly OccurredOn) : IDomainEvent
    {
        public string TypeId => "test.negotiation.started";
    }

    /// <summary>Two real and two invented drivers in the pool, three teams' managers, and the pool commands wired to fakes.</summary>
    private sealed class Lab
    {
        private readonly bool _defaults;

        public Lab()
        {
            var world = PoolKit.EmptyWorld();
            (world, var a) = PoolKit.Add(world, PoolKit.RealDriver("d_a", 1932, current: 9, extra: 6));
            (world, var b) = PoolKit.Add(world, PoolKit.RealDriver("d_b", 1931, current: 7, extra: 3));
            var filler = new List<PersonId>();
            for (var i = 0; i < 2; i++)
            {
                (world, var id) = world.AddPerson(new PersonSpec(
                    "Invented" + i,
                    "Filler" + i,
                    new GameDate(1932 + i, 3, 4),
                    "ITA",
                    false,
                    null,
                    [PersonRole.Driver],
                    PoolKit.Truth(8, 4)));
                filler.Add(id);
            }

            (world, var scout) = PoolKit.Add(world, PoolKit.Scout("s_best", 20, 20));
            world = PoolKit.SignScout(world, scout, PoolKit.Alpha);
            world = world.WithSection(TalentPoolSection.Empty.EnterAll([a, b, filler[0], filler[1]], world.CurrentDate));
            Holder = new Holder { World = world };
            Book = new PoolBook(() => Holder.World, next => Holder.World = next);
            Managers = new ManagerRegistry();
            Managers.Register(Anna, ManagerKind.Human, "Anna");
            Managers.Register(Bram, ManagerKind.Human, "Bram");
            Managers.Register(Ghost, ManagerKind.Human, "Ghost");
            Managers.Register(Bot, ManagerKind.Ai, "Bot");
            Query = new PoolQuery(Book, new Teams());
            Queue = new CommandQueue();
        }

        public bool Broke { get; set; }

        public bool UseDefaultPorts
        {
            get => _defaults;
            init => _defaults = value;
        }

        public Holder Holder { get; }

        public PoolBook Book { get; }

        public ManagerRegistry Managers { get; }

        public PoolQuery Query { get; }

        public CommandQueue Queue { get; }

        public FakeFunding Funding { get; } = new();

        public FakeNegotiations Negotiations { get; } = new();

        public WorldState World => Holder.World;

        public TalentPoolSection Section => Book.Section;

        public string HandleOf(string personId) => Section.Find(PersonId.Real(personId))!.HandleText;

        /// <summary>Puts a member in a team's academy directly, without a command.</summary>
        public void PutInAcademy(string personId, OrganizationId team) =>
            Holder.World = Holder.World.WithSection(Section.Recruit(PersonId.Real(personId), team));

        public CommandResult Submit(ICommand command)
        {
            Funding.Broke = Broke;
            var dispatcher = new CommandDispatcher();
            var teams = new Teams();
            dispatcher.Register(new AssignScoutFocusHandler(Book, teams));
            dispatcher.Register(new RecruitJuniorHandler(Book, teams));
            dispatcher.Register(new ReleaseJuniorHandler(Book, teams));
            dispatcher.Register(_defaults ? new FundJuniorHandler(Book, teams) : new FundJuniorHandler(Book, teams, Funding));
            dispatcher.Register(_defaults ? new SignPoolDriverHandler(Book, teams) : new SignPoolDriverHandler(Book, teams, Negotiations));
            Queue.Enqueue(command);
            return dispatcher.DispatchAll(Queue, new CommandContext(new StubWorldState(Today), Managers)).Single();
        }

        /// <summary>Sets the manager's focus and lets the scouts work for some months, through the real day handler.</summary>
        public void Scout(ManagerId manager, ScoutFocusKind kind, int months)
        {
            var organization = new Teams().OrganizationOf(manager.Value)!.Value;
            Holder.World = Holder.World.WithSection(Section.SetFocus(new ScoutFocus(organization, kind, null)));
            var run = new PoolKit.Run(Holder.World, 5, [], [], new Paddock.Simulation.Pool.TalentPoolOptions { TargetSize = 0 });
            run.World = Holder.World;
            run.LiveUntil(new GameDate(1950, 1, 1).AddDays((31 * months) + 1));
            Holder.World = run.World.WithDate(Holder.World.CurrentDate);
        }
    }
}
