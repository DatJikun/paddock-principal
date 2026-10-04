using System.Reflection;
using Paddock.Application.Access;
using Paddock.Application.Contracts;
using Paddock.Domain.Contracts;
using Paddock.Domain.People;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Tests.Persistence;
using static Paddock.Tests.Contracts.ContractKit;

namespace Paddock.Tests.Contracts;

/// <summary>
/// INV-003 for contracts (a team sees bands, never truth; the person's truth stays inside the decision), the manager-facing
/// queries, and the save of the contracts section. Fixtures are SYNTHETIC (see <see cref="ContractKit"/>).
/// </summary>
public class ContractKnowledgeAndSaveTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-contracts-").FullName;

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private static AccessContext AnnaAccess => AccessContext.ForManager(new ManagerId(Anna.Value));

    /// <summary>The same story told in a world with the given hidden attribute: an AI-side caller and a human both negotiate.</summary>
    private static (Lab Lab, OfferTerms AiOffer) Story(int hiddenCornering)
    {
        var lab = new Lab(hiddenCornering);
        var aiOffer = ReferenceOffer.Suggest(NegotiationSubject.DriverSeat, lab.World.KnowledgeOf(TeamC, DriverX), 1955, lab.Environment.Pay);
        var bot = lab.OpenOk(Bot, TeamC, DriverX, deadlineDays: 30);
        lab.Offer(Bot, bot, aiOffer);
        var anna = lab.OpenOk(Anna, TeamA, DriverX, deadlineDays: 40);
        lab.Offer(Anna, anna, Terms(50_000, SeatStatus.NumberTwo));
        lab.AdvanceUntilAnswered(anna);
        lab.Offer(Anna, anna, Terms(60_000, SeatStatus.NumberTwo, 3));
        lab.AdvanceUntilAnswered(anna);
        lab.Advance(45);
        return (lab, aiOffer);
    }

    private static object[] Projection(Lab lab) => lab.Section.Negotiations
        .Select(negotiation => (object)(
            negotiation.Id,
            negotiation.Status,
            negotiation.RoundsUsed,
            negotiation.MaxRounds,
            negotiation.Interest,
            negotiation.RespondOn,
            negotiation.Deadline,
            negotiation.CurrentOffer,
            negotiation.Counter,
            string.Join(",", negotiation.Reasons),
            string.Join(";", negotiation.History.Select(round => round.Kind + ":" + round.On + ":" + string.Join(",", round.Reasons) + ":" + round.Terms))))
        .ToArray();

    [Fact]
    public void TwoWorldsThatDifferOnlyInAHiddenAttributeGiveIdenticalOffersAndVisibleReasons()
    {
        var (low, lowOffer) = Story(11);
        var (high, highOffer) = Story(13);

        // The hidden attribute really differs, and the bands the teams believe do not.
        Assert.NotEqual(low.World.TruthOf(DriverX).Value("cornering"), high.World.TruthOf(DriverX).Value("cornering"));
        Assert.NotEqual(low.World.StateHash(), high.World.StateHash());
        foreach (var team in new[] { TeamA, TeamB, TeamC })
        {
            Assert.Equal(low.World.KnowledgeOf(team, DriverX)!.Value.Attributes, high.World.KnowledgeOf(team, DriverX)!.Value.Attributes);
        }

        // AI-side callers build the same offer from the same bands.
        Assert.Equal(lowOffer, highOffer);

        // The person answers the same way, for the same reasons, on the same days.
        Assert.Equal(Projection(low), Projection(high));
        Assert.Equal(
            WorldState.At(Start).WithSection(low.Section).StateHash(),
            WorldState.At(Start).WithSection(high.Section).StateHash());
        var lowView = low.Query.View(AnnaAccess).Items;
        var highView = high.Query.View(AnnaAccess).Items;
        Assert.Equal(
            lowView.Select(view => (view.Id, view.Status, string.Join(",", view.Reasons.Select(reason => reason.Key)), view.Counter, view.Interest.Key)),
            highView.Select(view => (view.Id, view.Status, string.Join(",", view.Reasons.Select(reason => reason.Key)), view.Counter, view.Interest.Key)));
        Assert.NotEmpty(lowView.Single().Reasons);
    }

    [Fact]
    public void TheViewsCarryNoTruthTypesAtAll()
    {
        var forbidden = new HashSet<Type> { typeof(PersonTruth), typeof(PersonalityTraits), typeof(NamedAttribute), typeof(Person), typeof(PrimaryPersonality) };
        var seen = new HashSet<Type>();

        void Walk(Type type)
        {
            if (!seen.Add(type))
            {
                return;
            }

            Assert.DoesNotContain(type, forbidden);
            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    Walk(argument);
                }
            }

            if (type.HasElementType)
            {
                Walk(type.GetElementType()!);
            }

            if (type.Namespace is { } ns && ns.StartsWith("Paddock.", StringComparison.Ordinal))
            {
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    Walk(property.PropertyType);
                }
            }
        }

        foreach (var type in new[] { typeof(NegotiationsView), typeof(NegotiationView), typeof(FreeAgentView), typeof(KnownAttributeView), typeof(NegotiationRoundView) })
        {
            Walk(type);
        }
    }

    [Fact]
    public void AManagerSeesOnlyTheirOwnNegotiationsAndTheDeveloperSeesAll()
    {
        var lab = new Lab();
        lab.OpenOk(Anna, TeamA, DriverX);
        lab.OpenOk(Bram, TeamB, DriverY);

        Assert.Equal(["neg:1"], lab.Query.View(AnnaAccess).Items.Select(view => view.Id));
        Assert.Equal(["neg:2"], lab.Query.View(AccessContext.ForManager(new ManagerId(Bram.Value))).Items.Select(view => view.Id));
        Assert.Equal(["neg:1", "neg:2"], lab.Query.View(AccessContext.Developer).Items.Select(view => view.Id));
        Assert.Empty(lab.Query.View(AccessContext.ForAi(new ManagerId(Bot.Value))).Items);
    }

    [Fact]
    public void AViewShowsBandsOfTheProposersBeliefAndChangesNothing()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, DriverX);
        lab.Offer(Anna, id, Terms(60_000));
        lab.Advance(1);
        var hash = lab.Hash();
        var rng = lab.Clock.RngStates.Count;

        var view = lab.Query.View(AnnaAccess).Items.Single();

        Assert.Equal(hash, lab.Hash());
        Assert.Equal(rng, lab.Clock.RngStates.Count);
        Assert.Equal("Ada Xavier", view.PersonName);
        Assert.Equal(GenerationEstimates.DriverAttributeKeys.Length, view.KnownAttributes.Count);
        Assert.All(view.KnownAttributes, attribute => Assert.Equal((11, 13), (attribute.Low, attribute.High)));
        Assert.Equal(ContractKeys.StatusAwaitingResponse, view.StatusText.Key);
        Assert.Equal(ContractKeys.InterestHigh, view.Interest.Key);
        Assert.NotNull(view.RespondOn);
    }

    // --- Saving ---

    private static Lab BusyLab()
    {
        var lab = new Lab();
        var anna = lab.OpenOk(Anna, TeamA, DriverX, deadlineDays: 20);
        var bram = lab.OpenOk(Bram, TeamB, DriverX, deadlineDays: 40);
        var bot = lab.OpenOk(Bot, TeamC, DriverY);
        var staff = lab.OpenOk(Anna, TeamA, Strategist, NegotiationSubject.Staff(StaffRole.Strategist));
        lab.Offer(Anna, anna, Terms(100_000, SeatStatus.Equal, 2, 100, 5_000, 50_000, new OfferOption(OptionHolder.Team, 1), new ExitClause(6)));
        lab.Offer(Bram, bram, Terms(130_000));
        lab.Offer(Bot, bot, Terms(50_000));
        lab.Offer(Anna, staff, Terms(25_000, null));
        lab.Advance(10);
        return lab;
    }

    private SaveFile NewSave() => SaveFile.Create(Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".paddock"), WorldFixtures.Meta());

    [Fact]
    public void TheContractsSectionRoundTripsThroughTheSaveWithTheSameHash()
    {
        var lab = BusyLab();
        lab.Advance(30);
        Assert.Contains(lab.Section.Negotiations, negotiation => negotiation.Status == NegotiationStatus.PersonAgreed);
        Assert.Contains(lab.Section.Negotiations, negotiation => negotiation.Status == NegotiationStatus.Lost);
        Assert.Contains(lab.Section.Negotiations, negotiation => negotiation.History.Count > 1);
        var contracted = lab.Book.Into(lab.World);
        using var file = NewSave();
        var repository = new WorldRepository(file);

        repository.SaveWorld(contracted, lab.Today);
        var loaded = repository.LoadWorld();

        Assert.Equal(contracted.StateHash(), loaded.StateHash());
        var section = loaded.Section<ContractsSection>(ContractsSection.SectionName)!;
        Assert.Equal(lab.Section.NextNegotiation, section.NextNegotiation);
        Assert.Equal(Projection(lab), Projection(new LabView(section)));
    }

    [Fact]
    public void TermsAndRenewalPromptsAreSavedToo()
    {
        var lab = new Lab(customizeSection: section => section
            .WithTerms(new ContractTerms(ContractId.Generated(1), 10, 20, 30, OptionHolder.Person, new ExitClause(4)))
            .WithTerms(new ContractTerms(ContractId.Generated(2), 0, 0, 0, null, null))
            .WithRenewalPrompt(ContractId.Generated(1)));
        using var file = NewSave();
        var repository = new WorldRepository(file);

        repository.SaveWorld(lab.Book.Into(), lab.Today);
        var section = repository.LoadWorld().Section<ContractsSection>(ContractsSection.SectionName)!;

        Assert.Equal(lab.Section.Terms, section.Terms);
        Assert.Equal([ContractId.Generated(1)], section.PromptedRenewals);
        Assert.Equal(1L, section.NextNegotiation);
    }

    [Fact]
    public void SavingAgainReplacesTheRowsAndDroppingTheSectionClearsThem()
    {
        var lab = BusyLab();
        using var file = NewSave();
        var repository = new WorldRepository(file);
        repository.SaveWorld(lab.Book.Into(), lab.Today);
        Assert.Equal(4L, Count(file, "negotiations"));
        Assert.True(Count(file, "negotiation_terms") >= 4);

        lab.Walk(Anna, "neg:1");
        repository.SaveWorld(lab.Book.Into(), lab.Today);
        Assert.Equal(4L, Count(file, "negotiations"));
        Assert.Equal(lab.Book.Into().StateHash(), repository.LoadWorld().StateHash());

        repository.SaveWorld(lab.World, lab.Today);
        Assert.Equal(0L, Count(file, "negotiations"));
        Assert.Equal(0L, Count(file, "negotiation_terms"));
        Assert.Equal(0L, Count(file, "negotiation_rounds"));
        Assert.Equal(0L, Count(file, "negotiation_reasons"));
        Assert.Equal(0L, Count(file, "contract_terms"));
        Assert.Empty(repository.LoadWorld().Sections);
    }

    [Fact]
    public void ASaveFromBeforeTheContractsMigrationLoadsWithNoSectionAndTheSameHash()
    {
        var path = Path.Combine(_directory, "old.paddock");
        var before = SaveMigrations.Production.TakeWhile(migration => migration is not V007_ContractsSection).ToArray();
        var world = WorldFixtures.Small();
        using (var created = SaveFile.Create(path, WorldFixtures.Meta(), before))
        {
            Assert.Equal(before.Length, created.ReadMeta().SchemaVersion);
        }

        using var opened = SaveFile.Open(path);
        Assert.Equal(SaveMigrations.CurrentVersion, opened.ReadMeta().SchemaVersion);
        var repository = new WorldRepository(opened);
        repository.SaveWorld(world, WorldFixtures.Opening);

        Assert.Empty(repository.LoadWorld().Sections);
        Assert.Equal(world.StateHash(), repository.LoadWorld().StateHash());
        Assert.Equal(0L, Count(opened, "negotiations"));
    }

    private static long Count(SaveFile file, string table)
    {
        using var command = file.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM " + table;
        return (long)command.ExecuteScalar()!;
    }

    /// <summary>Lets <see cref="Projection"/> read a section loaded from a save.</summary>
    private sealed class LabView
    {
        public LabView(ContractsSection section) => Section = section;

        public ContractsSection Section { get; }
    }

    private static object[] Projection(LabView view) => view.Section.Negotiations
        .Select(negotiation => (object)(
            negotiation.Id,
            negotiation.Status,
            negotiation.RoundsUsed,
            negotiation.MaxRounds,
            negotiation.Interest,
            negotiation.RespondOn,
            negotiation.Deadline,
            negotiation.CurrentOffer,
            negotiation.Counter,
            string.Join(",", negotiation.Reasons),
            string.Join(";", negotiation.History.Select(round => round.Kind + ":" + round.On + ":" + string.Join(",", round.Reasons) + ":" + round.Terms))))
        .ToArray();
}
