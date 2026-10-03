using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.World;
using Paddock.Domain.Inbox;
using Paddock.Domain.Time;
using Paddock.Tests.Commands;

namespace Paddock.Tests.Inbox;

/// <summary>
/// Fixtures for the inbox tests. The kinds, keys and values here are synthetic test data: no real kind of inbox item
/// exists yet, so none of this is player text and none is a calibrated number.
/// </summary>
internal static class InboxKit
{
    public const string OfferKind = "test.offer";

    public const string NoteKind = "test.note";

    public static readonly GameDate Today = new(1950, 5, 13);

    public static GameDate Day(int offset) => Today.AddDays(offset);

    /// <summary>A decision that never lapses.</summary>
    public static InboxItemDraft Decision() => new(
        OfferKind,
        "inbox.test.offer.subject",
        [new("amount", "100"), new("who", "Fixture Motors")],
        [
            new InboxOption("accept", "inbox.test.offer.accept", "inbox.test.offer.acceptConsequence"),
            new InboxOption("decline", "inbox.test.offer.decline", "inbox.test.offer.declineConsequence"),
        ],
        null,
        null);

    /// <summary>An offer that lapses after <paramref name="validDays"/> days and declines by itself.</summary>
    public static InboxItemDraft LapsingOffer(int validDays) => new(
        OfferKind,
        "inbox.test.offer.subject",
        [new("amount", "100")],
        [
            new InboxOption("accept", "inbox.test.offer.accept", "inbox.test.offer.acceptConsequence"),
            new InboxOption("decline", "inbox.test.offer.decline", "inbox.test.offer.declineConsequence"),
        ],
        Day(validDays),
        "decline");

    public static InboxItemDraft Note() => new(NoteKind, "inbox.test.note.subject", [new("detail", "x")], null, null, null);

    public sealed class Lab
    {
        public Lab(params (ManagerId Id, ManagerKind Kind, string Name)[] managers)
        {
            (Managers, World, _) = CommandLab.Open(managers);
            Resolver = new TestResolver();
            var resolvers = new InboxResolvers();
            resolvers.Register(Resolver);
            Book = new InboxBook(resolvers);
            Context = new CommandContext(World, Managers, Book);
            Dispatcher = new CommandDispatcher();
            Dispatcher.Register(new ResolveInboxItemHandler());
            Dispatcher.Register(new DismissInboxItemHandler());
            Dispatcher.Register(new ExpireInboxItemHandler());
            Queue = new CommandQueue();
            Query = new InboxQuery(Book);
        }

        public ManagerRegistry Managers { get; }

        public StubWorldState World { get; }

        public TestResolver Resolver { get; }

        public InboxBook Book { get; }

        public CommandContext Context { get; }

        public CommandDispatcher Dispatcher { get; }

        public CommandQueue Queue { get; }

        public InboxQuery Query { get; }

        public string Hash() => Book.Into(Paddock.Domain.World.WorldState.At(Today)).StateHash()
            + ":" + World.ContentHash();

        public CommandResult Submit(ICommand command)
        {
            Queue.Enqueue(command);
            return Dispatcher.DispatchAll(Queue, Context).Single();
        }

        public CommandResult Resolve(ManagerId manager, string itemId, string optionId, int day = 0) =>
            Submit(new ResolveInboxItemCommand
            {
                ManagerId = manager,
                IssuedOn = ToDateOnly(Day(day)),
                ItemId = itemId,
                OptionId = optionId,
            });

        public CommandResult Dismiss(ManagerId manager, string itemId, int day = 0) =>
            Submit(new DismissInboxItemCommand { ManagerId = manager, IssuedOn = ToDateOnly(Day(day)), ItemId = itemId });

        public string Post(ManagerId manager, InboxItemDraft draft, int day = 0) =>
            Book.Post(Managers, manager, draft, Day(day)).ItemId;

        /// <summary>The expiry step a host runs each morning: queue the commands, then dispatch them.</summary>
        public IReadOnlyList<CommandResult> RunExpiry(int day)
        {
            InboxExpiry.EnqueueDue(Queue, Book, Day(day));
            return Dispatcher.DispatchAll(Queue, Context);
        }
    }

    public static DateOnly ToDateOnly(GameDate date) => new(date.Year, date.Month, date.Day);

    /// <summary>The owner of the test kind: records what it was asked to do and writes the answer into the stub world.</summary>
    public sealed class TestResolver : IInboxResolver
    {
        public string Kind => OfferKind;

        public List<string> Executed { get; } = [];

        /// <summary>When set, the owner cannot carry out any option (for example the money is gone).</summary>
        public bool CannotCarryOut { get; set; }

        public TranslationMessage? Validate(InboxItem item, string optionId, CommandContext context) =>
            CannotCarryOut ? TranslationMessage.Of("inbox.test.offer.cannot") : null;

        public IReadOnlyList<IDomainEvent> Execute(InboxItem item, string optionId, CommandContext context)
        {
            Executed.Add(item.Id + ":" + optionId);
            ((StubWorldState)context.World).SetTeamNote("offer-" + item.Id, optionId);
            return [new OfferAnswered(new ManagerId(item.ManagerId), context.World.CurrentDate, item.Id, optionId)];
        }
    }

    public sealed record OfferAnswered(ManagerId ManagerId, DateOnly OccurredOn, string ItemId, string OptionId) : IDomainEvent
    {
        public string TypeId => "test.offer-answered";
    }
}
