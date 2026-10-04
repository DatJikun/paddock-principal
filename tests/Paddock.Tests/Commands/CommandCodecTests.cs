using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Codec;

namespace Paddock.Tests.Commands;

/// <summary>The save codecs of commands and managers, and the submission counter restore (issue #123).</summary>
public class CommandCodecTests
{
    private static readonly DateOnly Day = new(1955, 6, 1);

    [Fact]
    public void EveryInboxCommandRoundTripsWithItsManagerDateAndNumber()
    {
        var manager = new ManagerId("human:1");
        ICommand[] commands =
        [
            new ResolveInboxItemCommand { ManagerId = manager, IssuedOn = Day, ItemId = "inb:7", OptionId = "accept \"now\"", SubmissionNumber = 4 },
            new DismissInboxItemCommand { ManagerId = manager, IssuedOn = Day, ItemId = "inb:8", SubmissionNumber = 5 },
            new ExpireInboxItemCommand { ManagerId = new ManagerId("ai:paddock"), IssuedOn = Day.AddDays(1), ItemId = "inb:9", SubmissionNumber = 6 },
        ];

        foreach (var command in commands)
        {
            var encoded = CommandCodec.Production.Encode(command);
            var decoded = CommandCodec.Production.Decode(encoded.Tag, encoded.Text, command.ManagerId, command.SubmissionNumber, command.IssuedOn);
            Assert.Equal(command, decoded);
            Assert.Equal(command.GetType(), decoded.GetType());
        }

        Assert.Equal("inbox.resolve/1", CommandCodec.Production.Encode(commands[0]).Tag);
        Assert.Equal("""{"itemId":"inb:8"}""", CommandCodec.Production.Encode(commands[1]).Text);
    }

    [Fact]
    public void EveryCommandOfTheApplicationHasACodecEntry()
    {
        // A system that adds a command adds its entry to CommandCodec.Production; this fails until it does.
        var commands = typeof(ICommand).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ICommand).IsAssignableFrom(type))
            .ToHashSet();
        Assert.NotEmpty(commands);
        Assert.Equal(commands, CommandCodec.Production.SavedTypes.ToHashSet());
    }

    [Fact]
    public void ACommandWithoutAnEntryCannotBeSaved()
    {
        var command = new RenameManagerCommand { ManagerId = new ManagerId("human:1"), IssuedOn = Day, NewName = "x", SubmissionNumber = 1 };
        var failure = Assert.Throws<InvalidOperationException>(() => CommandCodec.Production.Encode(command));
        Assert.Contains(nameof(RenameManagerCommand), failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("inbox.resolve/2")]
    [InlineData("inbox.resolve")]
    [InlineData("ResolveInboxItemCommand")]
    [InlineData("Paddock.Application.Inbox.ResolveInboxItemCommand")]
    public void AnUnknownTagOrVersionFailsLoudly(string tag)
    {
        var failure = Assert.Throws<UnknownTagException>(
            () => CommandCodec.Production.Decode(tag, """{"itemId":"x","optionId":"y"}""", new ManagerId("human:1"), 1, Day));
        Assert.Equal(tag, failure.Tag);
        Assert.Equal("command", failure.Kind);
    }

    [Fact]
    public void AMalformedBodyOrNumberIsRefused()
    {
        var manager = new ManagerId("human:1");
        Assert.IsAssignableFrom<InvalidDataException>(Record.Exception(
            () => CommandCodec.Production.Decode("inbox.resolve/1", """{"itemId":"x"}""", manager, 1, Day)));
        Assert.IsAssignableFrom<InvalidDataException>(Record.Exception(
            () => CommandCodec.Production.Decode("inbox.dismiss/1", """{"itemId":"x","extra":"y"}""", manager, 1, Day)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CommandCodec.Production.Decode("inbox.dismiss/1", """{"itemId":"x"}""", manager, 0, Day));
    }

    [Fact]
    public void TwoEntriesCannotShareATagOrAType()
    {
        var entry = CommandCodecEntry.For<DismissInboxItemCommand>("a/1", _ => "{}", (_, manager, issued) => new DismissInboxItemCommand { ManagerId = manager, IssuedOn = issued, ItemId = "x" });
        var sameTag = CommandCodecEntry.For<ExpireInboxItemCommand>("a/1", _ => "{}", (_, manager, issued) => new ExpireInboxItemCommand { ManagerId = manager, IssuedOn = issued, ItemId = "x" });
        var sameType = CommandCodecEntry.For<DismissInboxItemCommand>("b/1", _ => "{}", (_, manager, issued) => new DismissInboxItemCommand { ManagerId = manager, IssuedOn = issued, ItemId = "x" });

        Assert.Throws<ArgumentException>(() => new CommandCodec([entry, sameTag]));
        Assert.Throws<ArgumentException>(() => new CommandCodec([entry, sameType]));
        Assert.Single(new CommandCodec([entry]).SavedTypes);
    }

    [Fact]
    public void AQueueRestoredFromTheSavedCounterNeverReusesANumber()
    {
        var queue = new CommandQueue();
        var first = queue.Enqueue(new SetTeamNoteCommand { ManagerId = new ManagerId("human:1"), IssuedOn = Day, TeamId = "t", Note = "n" });
        queue.Enqueue(new SetTeamNoteCommand { ManagerId = new ManagerId("human:1"), IssuedOn = Day, TeamId = "t", Note = "n" });
        queue.DequeueAll();
        Assert.Equal(1, first.SubmissionNumber);
        Assert.Equal(3, queue.NextSubmissionNumber);

        var restored = new CommandQueue(queue.NextSubmissionNumber);
        var next = restored.Enqueue(new SetTeamNoteCommand { ManagerId = new ManagerId("human:1"), IssuedOn = Day, TeamId = "t", Note = "n" });
        Assert.Equal(3, next.SubmissionNumber);
        Assert.Throws<ArgumentOutOfRangeException>(() => new CommandQueue(0));
    }

    [Fact]
    public void ManagerKindsUseFixedWordsAndAnUnknownWordFailsLoudly()
    {
        Assert.Equal("Human", ManagerCodec.EncodeKind(ManagerKind.Human));
        Assert.Equal("Ai", ManagerCodec.EncodeKind(ManagerKind.Ai));
        Assert.Equal(ManagerKind.Human, ManagerCodec.DecodeKind("Human"));
        Assert.Equal(ManagerKind.Ai, ManagerCodec.DecodeKind("Ai"));
        foreach (var word in new[] { "human", "AI", "Bot", "" })
        {
            var failure = Assert.Throws<UnknownTagException>(() => ManagerCodec.DecodeKind(word));
            Assert.Equal(word, failure.Tag);
        }

        Assert.Throws<InvalidOperationException>(() => ManagerCodec.EncodeKind((ManagerKind)99));
    }

    [Fact]
    public void ARegistryRestoresInOrderWithBlockingItemsAndNobodyReady()
    {
        var registry = new ManagerRegistry();
        registry.Register(new ManagerId("ai:paddock"), ManagerKind.Ai, "AI");
        registry.Register(new ManagerId("human:1"), ManagerKind.Human, "Ada");
        registry.PostBlockingItem(new ManagerId("human:1"), new BlockingItem("inbox.decision"));
        registry.SetReady(new ManagerId("ai:paddock"), true);

        var rows = registry.All.Select(manager => (
            manager.Id.Value,
            ManagerCodec.EncodeKind(manager.Kind),
            manager.DisplayName,
            manager.BlockingItem?.Kind)).ToArray();
        var restored = ManagerCodec.Restore(rows);

        Assert.Equal(["ai:paddock", "human:1"], restored.All.Select(manager => manager.Id.Value));
        Assert.Equal(ManagerKind.Human, restored.KindOf(new ManagerId("human:1")));
        Assert.Equal("Ada", restored.Get(new ManagerId("human:1")).DisplayName);
        Assert.Equal("inbox.decision", restored.Get(new ManagerId("human:1")).BlockingItem?.Kind);
        Assert.Null(restored.Get(new ManagerId("ai:paddock")).BlockingItem);
        Assert.All(restored.All, manager => Assert.False(manager.IsReady));
        Assert.Throws<UnknownTagException>(() => ManagerCodec.Restore([("x", "Robot", "X", null)]));
        Assert.Throws<InvalidOperationException>(() => ManagerCodec.Restore([("x", "Ai", "X", null), ("x", "Ai", "X", null)]));
    }
}
