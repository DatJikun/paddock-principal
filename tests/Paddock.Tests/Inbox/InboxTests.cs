using AccessContext = Paddock.Application.Access.AccessContext;
using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Inbox;
using Paddock.Domain.World;
using Paddock.Tests.Commands;
using static Paddock.Tests.Inbox.InboxKit;
using AccessManagerId = Paddock.Application.Access.ManagerId;

namespace Paddock.Tests.Inbox;

public class InboxTests
{
    private static ManagerId Anna => CommandLab.Anna;

    private static ManagerId Bram => CommandLab.Bram;

    private static ManagerId Bot => CommandLab.Bot;

    private static Lab TwoHumansAndABot() => new(
        (Anna, ManagerKind.Human, "Anna"),
        (Bram, ManagerKind.Human, "Bram"),
        (Bot, ManagerKind.Ai, "Bot"));

    // --- Items and ids ---

    [Fact]
    public void ItemsGetStableIdsFromTheSectionCounterAndNeverReuseOne()
    {
        var lab = TwoHumansAndABot();

        var first = lab.Post(Anna, Note());
        var second = lab.Post(Bram, Note());
        lab.Dismiss(Anna, first);
        var third = lab.Post(Anna, Note());

        Assert.Equal(["inb:1", "inb:2", "inb:3"], new[] { first, second, third });
        Assert.Equal(4, lab.Book.Section.NextNumber);
    }

    [Fact]
    public void ADecisionNeedsAtLeastTwoOptionsAndALapsingOneNeedsADefault()
    {
        var accept = new InboxOption("accept", "k.accept", "k.acceptConsequence");
        var decline = new InboxOption("decline", "k.decline", "k.declineConsequence");

        Assert.Throws<ArgumentException>(() => new InboxItemDraft("k", "s", null, [accept], null, null));
        Assert.Throws<ArgumentException>(() => new InboxItemDraft("k", "s", null, [accept, accept], null, null));
        Assert.Throws<ArgumentException>(() => new InboxItemDraft("k", "s", null, [accept, decline], Day(5), null));
        Assert.Throws<ArgumentException>(() => new InboxItemDraft("k", "s", null, [accept, decline], null, "decline"));
        Assert.Throws<ArgumentException>(() => new InboxItemDraft("k", "s", null, [accept, decline], Day(5), "missing"));
        Assert.Throws<ArgumentException>(() => new InboxItemDraft("k", "s", null, null, null, "decline"));
        Assert.Throws<ArgumentException>(() => new InboxItemDraft("k", "s", [new("a", "1"), new("a", "2")], null, null, null));
        Assert.Throws<ArgumentException>(() => new InboxOption("accept", "k.accept", " "));

        var ok = new InboxItemDraft("k", "s", null, [accept, decline], Day(5), "decline");
        Assert.True(ok.NeedsDecision);
    }

    [Fact]
    public void AnOptionAlwaysCarriesItsConsequencePreview()
    {
        var lab = TwoHumansAndABot();
        lab.Post(Anna, Decision());

        var item = Assert.Single(lab.Query.View(AccessContext.ForManager(new AccessManagerId("mgr-anna"))).Items);

        Assert.All(item.Options, option => Assert.False(string.IsNullOrWhiteSpace(option.Consequence.Key)));
        Assert.Equal("100", item.Subject.Parameters["amount"]);
        Assert.Equal("100", item.Options[0].Consequence.Parameters["amount"]);
    }

    [Fact]
    public void ADecisionWithoutAResolverCannotBePosted()
    {
        var lab = TwoHumansAndABot();
        var draft = new InboxItemDraft(
            "unowned.kind",
            "s",
            null,
            [new InboxOption("a", "k.a", "k.ac"), new InboxOption("b", "k.b", "k.bc")],
            null,
            null);

        Assert.Throws<InvalidOperationException>(() => lab.Book.Post(lab.Managers, Anna, draft, Today));
        Assert.Empty(lab.Book.Section.Items);
    }

    [Fact]
    public void AnUnknownManagerCannotReceiveAnItem()
    {
        var lab = TwoHumansAndABot();

        Assert.Throws<ArgumentException>(() => lab.Book.Post(lab.Managers, new ManagerId("mgr-nobody"), Note(), Today));
    }

    // --- Blocking the ready gate ---

    [Fact]
    public void AnOpenDecisionHoldsTheClockAndAnswerReleasesIt()
    {
        var lab = TwoHumansAndABot();
        lab.Managers.SetReady(Anna, true);
        lab.Managers.SetReady(Bram, true);
        var id = lab.Post(Anna, Decision());
        var gate = new ReadyGate();

        var refused = Assert.IsType<AdvanceResult.Refused>(gate.RequestAdvance(lab.Managers, lab.World));
        Assert.Equal(AdvanceRefusalKind.BlockingItem, refused.Refusal.Kind);
        Assert.Equal(InboxBook.BlockingKind, lab.Managers.Get(Anna).BlockingItem?.Kind);
        Assert.Null(lab.Managers.Get(Bram).BlockingItem);
        Assert.Equal(CommandLab.OpeningDay, lab.World.CurrentDate);

        var result = lab.Resolve(Anna, id, "accept");

        Assert.IsType<CommandResult.Accepted>(result);
        Assert.Null(lab.Managers.Get(Anna).BlockingItem);
        Assert.IsType<AdvanceResult.Advanced>(gate.RequestAdvance(lab.Managers, lab.World));
        Assert.Equal(CommandLab.OpeningDay.AddDays(1), lab.World.CurrentDate);
    }

    [Fact]
    public void AnInformationItemNeverHoldsTheClock()
    {
        var lab = TwoHumansAndABot();
        lab.Managers.SetReady(Anna, true);
        lab.Managers.SetReady(Bram, true);
        lab.Post(Anna, Note());

        Assert.Null(lab.Managers.Get(Anna).BlockingItem);
        Assert.IsType<AdvanceResult.Advanced>(new ReadyGate().RequestAdvance(lab.Managers, lab.World));
    }

    [Fact]
    public void TheClockStaysHeldUntilTheLastOpenDecisionIsAnswered()
    {
        var lab = TwoHumansAndABot();
        var first = lab.Post(Anna, Decision());
        var second = lab.Post(Anna, Decision());

        lab.Resolve(Anna, first, "decline");
        Assert.NotNull(lab.Managers.Get(Anna).BlockingItem);

        lab.Resolve(Anna, second, "decline");
        Assert.Null(lab.Managers.Get(Anna).BlockingItem);
    }

    [Fact]
    public void AnAiManagersDecisionAlsoHoldsTheDayWhileAHumanPlays()
    {
        var lab = TwoHumansAndABot();
        lab.Managers.SetReady(Anna, true);
        lab.Managers.SetReady(Bram, true);
        lab.Post(Bot, Decision());

        var refused = Assert.IsType<AdvanceResult.Refused>(new ReadyGate().RequestAdvance(lab.Managers, lab.World));
        Assert.Equal(Bot, Assert.Single(refused.Refusal.WaitingFor).ManagerId);
    }

    [Fact]
    public void AnotherSystemsBlockingItemIsNotOverwrittenAndTheInboxTakesTheSlotWhenItFrees()
    {
        var lab = TwoHumansAndABot();
        lab.Managers.PostBlockingItem(Anna, new BlockingItem("negotiation"));
        var id = lab.Post(Anna, Decision());

        Assert.Equal("negotiation", lab.Managers.Get(Anna).BlockingItem?.Kind);

        lab.Managers.ClearBlockingItem(Anna);
        lab.Book.SyncBlocking(lab.Managers);
        Assert.Equal(InboxBook.BlockingKind, lab.Managers.Get(Anna).BlockingItem?.Kind);

        lab.Resolve(Anna, id, "accept");
        Assert.Null(lab.Managers.Get(Anna).BlockingItem);
    }

    [Fact]
    public void AnAnswerLeavesAnotherSystemsBlockingItemAlone()
    {
        var lab = TwoHumansAndABot();
        var id = lab.Post(Anna, Decision());
        lab.Managers.ClearBlockingItem(Anna);
        lab.Managers.PostBlockingItem(Anna, new BlockingItem("negotiation"));

        lab.Resolve(Anna, id, "accept");

        Assert.Equal("negotiation", lab.Managers.Get(Anna).BlockingItem?.Kind);
    }

    // --- Commands ---

    [Fact]
    public void AnAnswerRunsTheOwnersResolverAsPartOfTheCommandAndReportsEvents()
    {
        var lab = TwoHumansAndABot();
        var id = lab.Post(Anna, Decision());

        var accepted = Assert.IsType<CommandResult.Accepted>(lab.Resolve(Anna, id, "accept"));

        Assert.Equal(["inb:1:accept"], lab.Resolver.Executed);
        Assert.Equal("accept", lab.World.TeamNote("offer-inb:1"));
        Assert.Collection(
            accepted.Events,
            first =>
            {
                var resolved = Assert.IsType<InboxItemResolved>(first);
                Assert.Equal(id, resolved.ItemId);
                Assert.Equal("accept", resolved.OptionId);
                Assert.Equal(Anna, resolved.ManagerId);
            },
            second => Assert.IsType<OfferAnswered>(second));
        var item = lab.Book.Section.Find(id)!;
        Assert.Equal(InboxStatus.Resolved, item.Status);
        Assert.Equal("accept", item.ChosenOptionId);
        Assert.Single(lab.Dispatcher.Log.Entries);
    }

    [Fact]
    public void ARejectedAnswerChangesNothingAndIsNotLogged()
    {
        var lab = TwoHumansAndABot();
        var id = lab.Post(Anna, Decision());
        var note = lab.Post(Anna, Note());
        var before = lab.Hash();

        Assert.Equal(InboxKeys.OptionUnknown, Rejection(lab.Resolve(Anna, id, "haggle")));
        Assert.Equal(InboxKeys.ItemUnknown, Rejection(lab.Resolve(Anna, "inb:99", "accept")));
        Assert.Equal(InboxKeys.ItemUnknown, Rejection(lab.Resolve(Anna, "not-an-id", "accept")));
        Assert.Equal(InboxKeys.NotADecision, Rejection(lab.Resolve(Anna, note, "accept")));
        lab.Resolver.CannotCarryOut = true;
        Assert.Equal("inbox.test.offer.cannot", Rejection(lab.Resolve(Anna, id, "accept")));

        Assert.Equal(before, lab.Hash());
        Assert.Empty(lab.Dispatcher.Log.Entries);
        Assert.Empty(lab.Resolver.Executed);
        Assert.NotNull(lab.Managers.Get(Anna).BlockingItem);
    }

    [Fact]
    public void AnItemCanBeAnsweredOnlyOnce()
    {
        var lab = TwoHumansAndABot();
        var id = lab.Post(Anna, Decision());
        lab.Resolve(Anna, id, "accept");

        Assert.Equal(InboxKeys.ItemClosed, Rejection(lab.Resolve(Anna, id, "decline")));
        Assert.Single(lab.Resolver.Executed);
    }

    [Fact]
    public void AnInformationItemCanBeDismissedButADecisionCannot()
    {
        var lab = TwoHumansAndABot();
        var note = lab.Post(Anna, Note());
        var decision = lab.Post(Anna, Decision());

        Assert.Equal(InboxKeys.DecisionCannotBeDismissed, Rejection(lab.Dismiss(Anna, decision)));
        Assert.IsType<CommandResult.Accepted>(lab.Dismiss(Anna, note));

        Assert.Equal(InboxStatus.Dismissed, lab.Book.Section.Find(note)!.Status);
        Assert.Equal(InboxStatus.Open, lab.Book.Section.Find(decision)!.Status);
        Assert.Equal(InboxKeys.ItemClosed, Rejection(lab.Dismiss(Anna, note)));
    }

    // --- Per-manager isolation (INV-003) ---

    [Fact]
    public void AManagerCannotAnswerOrDismissAnotherManagersItemAndLearnsNothingFromTheTry()
    {
        var lab = TwoHumansAndABot();
        var decision = lab.Post(Anna, Decision());
        var note = lab.Post(Anna, Note());

        var foreign = Rejection(lab.Resolve(Bram, decision, "accept"));
        var missing = Rejection(lab.Resolve(Bram, "inb:99", "accept"));

        Assert.Equal(InboxKeys.ItemUnknown, foreign);
        Assert.Equal(missing, foreign);
        Assert.Equal(InboxKeys.ItemUnknown, Rejection(lab.Dismiss(Bram, note)));
        Assert.Equal(InboxStatus.Open, lab.Book.Section.Find(decision)!.Status);
        Assert.Equal(InboxStatus.Open, lab.Book.Section.Find(note)!.Status);
        Assert.Empty(lab.Resolver.Executed);
    }

    [Fact]
    public void AViewHoldsOnlyTheViewersOwnItemsAndTheDeveloperSeesAll()
    {
        var lab = TwoHumansAndABot();
        lab.Post(Anna, Decision());
        lab.Post(Bram, Note());
        lab.Post(Anna, Note());
        lab.Post(Bot, Decision());

        var anna = lab.Query.View(AccessContext.ForManager(new AccessManagerId("mgr-anna")));
        var bram = lab.Query.View(AccessContext.ForManager(new AccessManagerId("mgr-bram")));
        var bot = lab.Query.View(AccessContext.ForAi(new AccessManagerId("mgr-bot")));
        var developer = lab.Query.View(AccessContext.Developer);

        Assert.Equal(["inb:1", "inb:3"], anna.Items.Select(item => item.Id));
        Assert.Equal(["inb:2"], bram.Items.Select(item => item.Id));
        Assert.Equal(["inb:4"], bot.Items.Select(item => item.Id));
        Assert.All(anna.Items, item => Assert.Equal("mgr-anna", item.ManagerId));
        Assert.Equal(["inb:1", "inb:2", "inb:3", "inb:4"], developer.Items.Select(item => item.Id));
        Assert.Equal(1, anna.OpenDecisionCount);
        Assert.Equal(2, anna.OpenCount);
        Assert.Equal(0, bram.OpenDecisionCount);
    }

    [Fact]
    public void AViewOfAManagerWithNoItemsIsEmptyAndQueriesChangeNothing()
    {
        var lab = TwoHumansAndABot();
        lab.Post(Anna, Decision());
        var before = lab.Hash();

        var view = lab.Query.View(AccessContext.ForManager(new AccessManagerId("mgr-bram")));
        lab.Query.View(AccessContext.Developer);

        Assert.Empty(view.Items);
        Assert.Equal(before, lab.Hash());
    }

    // --- Validity dates and defaults ---

    [Fact]
    public void AnOfferIsValidThroughItsDateAndThenLapsesToItsDefaultDeterministically()
    {
        var lab = TwoHumansAndABot();
        var id = lab.Post(Anna, LapsingOffer(validDays: 3));

        Assert.Empty(lab.RunExpiry(day: 2));
        Assert.Empty(lab.RunExpiry(day: 3));
        Assert.Equal(InboxStatus.Open, lab.Book.Section.Find(id)!.Status);
        Assert.NotNull(lab.Managers.Get(Anna).BlockingItem);

        var results = lab.RunExpiry(day: 4);

        var accepted = Assert.IsType<CommandResult.Accepted>(Assert.Single(results));
        var item = lab.Book.Section.Find(id)!;
        Assert.Equal(InboxStatus.Expired, item.Status);
        Assert.Equal("decline", item.ChosenOptionId);
        Assert.Equal(Day(4), item.ClosedOn);
        Assert.Equal(["inb:1:decline"], lab.Resolver.Executed);
        var expired = Assert.IsType<InboxItemExpired>(accepted.Events[0]);
        Assert.Equal("decline", expired.OptionId);
        Assert.Null(lab.Managers.Get(Anna).BlockingItem);
        Assert.Empty(lab.RunExpiry(day: 5));
        Assert.Single(lab.Resolver.Executed);
    }

    [Fact]
    public void ExpiryGivesTheSameStateEveryTimeItRuns()
    {
        string Run()
        {
            var lab = TwoHumansAndABot();
            lab.Post(Anna, LapsingOffer(2));
            lab.Post(Bram, LapsingOffer(1));
            lab.Post(Anna, Note());
            lab.RunExpiry(day: 3);
            return lab.Hash();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void AnAnswerBeforeTheDateWinsOverTheDefault()
    {
        var lab = TwoHumansAndABot();
        var id = lab.Post(Anna, LapsingOffer(3));

        lab.Resolve(Anna, id, "accept", day: 2);
        lab.RunExpiry(day: 9);

        Assert.Equal(InboxStatus.Resolved, lab.Book.Section.Find(id)!.Status);
        Assert.Equal(["inb:1:accept"], lab.Resolver.Executed);
    }

    [Fact]
    public void ADecisionWithoutAValidityDateNeverExpires()
    {
        var lab = TwoHumansAndABot();
        var id = lab.Post(Anna, Decision());

        Assert.Empty(lab.RunExpiry(day: 400));

        Assert.Equal(InboxStatus.Open, lab.Book.Section.Find(id)!.Status);
        Assert.NotNull(lab.Managers.Get(Anna).BlockingItem);
    }

    [Fact]
    public void AnExpiryCommandBeforeTheDateIsRejected()
    {
        var lab = TwoHumansAndABot();
        var id = lab.Post(Anna, LapsingOffer(5));

        var result = lab.Submit(new ExpireInboxItemCommand { ManagerId = Anna, IssuedOn = ToDateOnly(Day(5)), ItemId = id });

        Assert.Equal(InboxKeys.NotYetLapsed, Rejection(result));
        Assert.Equal(InboxStatus.Open, lab.Book.Section.Find(id)!.Status);
    }

    [Fact]
    public void WhenTheOwnerCanNoLongerAcceptTheDefaultTheItemStillLapses()
    {
        var lab = TwoHumansAndABot();
        var id = lab.Post(Anna, LapsingOffer(1));
        lab.Resolver.CannotCarryOut = true;

        var accepted = Assert.IsType<CommandResult.Accepted>(Assert.Single(lab.RunExpiry(day: 2)));

        var item = lab.Book.Section.Find(id)!;
        Assert.Equal(InboxStatus.Expired, item.Status);
        Assert.Null(item.ChosenOptionId);
        Assert.Null(Assert.IsType<InboxItemExpired>(Assert.Single(accepted.Events)).OptionId);
        Assert.Empty(lab.Resolver.Executed);
        Assert.Null(lab.Managers.Get(Anna).BlockingItem);
    }

    [Fact]
    public void AnInformationItemWithAValidityDateLapsesWithoutAnyOption()
    {
        var lab = TwoHumansAndABot();
        var draft = new InboxItemDraft(NoteKind, "inbox.test.note.subject", null, null, Day(1), null);
        var id = lab.Post(Anna, draft);

        lab.RunExpiry(day: 2);

        var item = lab.Book.Section.Find(id)!;
        Assert.Equal(InboxStatus.Expired, item.Status);
        Assert.Null(item.ChosenOptionId);
        Assert.Empty(lab.Resolver.Executed);
    }

    [Fact]
    public void ExpiryIsQueuedInItemOrderForTheOwningManager()
    {
        var lab = TwoHumansAndABot();
        lab.Post(Bram, LapsingOffer(1));
        lab.Post(Anna, LapsingOffer(1));

        Assert.Equal(2, InboxExpiry.EnqueueDue(lab.Queue, lab.Book, Day(2)));

        var pending = lab.Queue.Pending.Cast<ExpireInboxItemCommand>().ToArray();
        Assert.Equal(["inb:1", "inb:2"], pending.Select(command => command.ItemId));
        Assert.Equal([Bram, Anna], pending.Select(command => command.ManagerId));
    }

    // --- Determinism and the world section ---

    [Fact]
    public void TheSameCommandSequenceTwiceGivesTheSameHash()
    {
        string Run()
        {
            var lab = TwoHumansAndABot();
            var a = lab.Post(Anna, Decision());
            lab.Post(Bram, Note());
            var offer = lab.Post(Anna, LapsingOffer(2));
            lab.Resolve(Anna, a, "accept", day: 1);
            lab.Resolve(Bram, "inb:1", "accept", day: 1);
            lab.RunExpiry(day: 4);
            return lab.Hash();
        }

        var first = Run();

        Assert.Equal(first, Run());
    }

    [Fact]
    public void TheInboxRidesInTheWorldStateAndComesBackFromIt()
    {
        var lab = TwoHumansAndABot();
        var id = lab.Post(Anna, Decision());
        lab.Post(Bram, LapsingOffer(2));
        lab.Resolve(Anna, id, "accept");
        var empty = WorldState.At(Today);

        var world = lab.Book.Into(empty);
        var again = InboxBook.From(world);

        Assert.NotEqual(empty.StateHash(), world.StateHash());
        Assert.Equal(world.StateHash(), again.Into(WorldState.At(Today)).StateHash());
        Assert.Equal(lab.Book.Section.Items.Count, again.Section.Items.Count);
        Assert.Equal(InboxStatus.Resolved, again.Section.Find(id)!.Status);
    }

    [Fact]
    public void AnInboxThatNeverHeldAnItemLeavesTheWorldHashAlone()
    {
        var empty = WorldState.At(Today);

        var world = new InboxBook().Into(empty);

        Assert.Equal(empty.StateHash(), world.StateHash());
        Assert.Empty(world.Sections);
    }

    [Fact]
    public void TheSectionHashSeesEveryFieldOfAnItem()
    {
        string Hash(Action<Lab> arrange)
        {
            var lab = TwoHumansAndABot();
            arrange(lab);
            return lab.Book.Into(WorldState.At(Today)).StateHash();
        }

        var baseline = Hash(lab => lab.Post(Anna, LapsingOffer(3)));

        Assert.Equal(baseline, Hash(lab => lab.Post(Anna, LapsingOffer(3))));
        Assert.NotEqual(baseline, Hash(lab => lab.Post(Anna, LapsingOffer(4))));
        Assert.NotEqual(baseline, Hash(lab => lab.Post(Bram, LapsingOffer(3))));
        Assert.NotEqual(baseline, Hash(lab => lab.Post(Anna, LapsingOffer(3), day: 1)));
        Assert.NotEqual(baseline, Hash(lab => lab.Post(Anna, Decision())));
        Assert.NotEqual(baseline, Hash(lab =>
        {
            var id = lab.Post(Anna, LapsingOffer(3));
            lab.Resolve(Anna, id, "accept");
        }));
        Assert.NotEqual(
            Hash(lab => lab.Post(Anna, new InboxItemDraft(NoteKind, "s", [new("a", "1")], null, null, null))),
            Hash(lab => lab.Post(Anna, new InboxItemDraft(NoteKind, "s", [new("a", "2")], null, null, null))));
    }

    [Fact]
    public void ArgumentOrderDoesNotChangeTheHash()
    {
        string Hash(params KeyValuePair<string, string>[] arguments)
        {
            var lab = TwoHumansAndABot();
            lab.Post(Anna, new InboxItemDraft(NoteKind, "s", arguments, null, null, null));
            return lab.Book.Into(WorldState.At(Today)).StateHash();
        }

        Assert.Equal(
            Hash(new("a", "1"), new("b", "2")),
            Hash(new("b", "2"), new("a", "1")));
    }

    private static string Rejection(CommandResult result) =>
        Assert.IsType<CommandResult.Rejected>(result).Reason.Key;
}
