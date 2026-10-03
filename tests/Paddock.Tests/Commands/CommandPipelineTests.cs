using Paddock.Application.Commands;
using Paddock.Application.Managers;
using Paddock.Application.World;

namespace Paddock.Tests.Commands;

public class CommandPipelineTests
{
    [Fact]
    public void RejectedCommandLeavesStateUntouchedAndIsNotLogged()
    {
        var (_, world, context) = CommandLab.Open((CommandLab.Anna, ManagerKind.Human, "Anna"));
        var before = world.ContentHash();
        var dispatcher = CommandLab.NotesAndNames();
        var queue = new CommandQueue();
        var queued = queue.Enqueue(new SetTeamNoteCommand
        {
            ManagerId = CommandLab.Anna,
            IssuedOn = CommandLab.OpeningDay,
            TeamId = "alfa",
            Note = "   ",
        });

        var result = dispatcher.Dispatch(queued, context);

        var rejected = Assert.IsType<CommandResult.Rejected>(result);
        Assert.Equal(SampleText.NoteEmpty, rejected.Reason.Key);
        Assert.Empty(rejected.Reason.Parameters);
        Assert.Equal(before, world.ContentHash());
        Assert.Null(world.TeamNote("alfa"));
        Assert.Empty(dispatcher.Log.Entries);
    }

    [Fact]
    public void UnknownCommandAndUnknownManagerDoNotChangeState()
    {
        var (_, world, context) = CommandLab.Open((CommandLab.Anna, ManagerKind.Human, "Anna"));
        var before = world.ContentHash();
        var dispatcher = CommandLab.Dispatcher(null, new SetTeamNoteHandler());
        var queue = new CommandQueue();
        var rename = queue.Enqueue(new RenameManagerCommand
        {
            ManagerId = CommandLab.Anna,
            IssuedOn = CommandLab.OpeningDay,
            NewName = "Annette",
        });
        var stranger = queue.Enqueue(new SetTeamNoteCommand
        {
            ManagerId = CommandLab.Bram,
            IssuedOn = CommandLab.OpeningDay,
            TeamId = "alfa",
            Note = "nope",
        });

        var unknownCommand = dispatcher.Dispatch(rename, context);
        var unknownManager = dispatcher.Dispatch(stranger, context);

        var commandRejection = Assert.IsType<CommandResult.Rejected>(unknownCommand);
        Assert.Equal(TranslationKeys.UnknownCommand, commandRejection.Reason.Key);
        Assert.Equal(nameof(RenameManagerCommand), commandRejection.Reason.Parameters["command"]);

        var managerRejection = Assert.IsType<CommandResult.Rejected>(unknownManager);
        Assert.Equal(TranslationKeys.ManagerUnknown, managerRejection.Reason.Key);
        Assert.Equal(CommandLab.Bram.Value, managerRejection.Reason.Parameters["managerId"]);
        Assert.Equal(before, world.ContentHash());
        Assert.Empty(dispatcher.Log.Entries);
        Assert.Equal("Anna", world.ManagerName(CommandLab.Anna.Value));
    }

    [Fact]
    public void ARejectedCommandDoesNotStopLaterCommands()
    {
        var (managers, world, context) = CommandLab.Open(
            (CommandLab.Anna, ManagerKind.Human, "Anna"),
            (CommandLab.Bram, ManagerKind.Human, "Bram"));
        var dispatcher = CommandLab.NotesAndNames();
        var queue = new CommandQueue();
        queue.Enqueue(new SetTeamNoteCommand
        {
            ManagerId = CommandLab.Anna,
            IssuedOn = CommandLab.OpeningDay,
            TeamId = "alfa",
            Note = "kept",
        });
        queue.Enqueue(new SetTeamNoteCommand
        {
            ManagerId = CommandLab.Bram,
            IssuedOn = CommandLab.OpeningDay,
            TeamId = "  ",
            Note = "dropped",
        });
        queue.Enqueue(new RenameManagerCommand
        {
            ManagerId = CommandLab.Anna,
            IssuedOn = CommandLab.OpeningDay,
            NewName = "Annette",
        });

        var results = dispatcher.DispatchAll(queue, context);

        Assert.IsType<CommandResult.Accepted>(results[0]);
        Assert.IsType<CommandResult.Rejected>(results[1]);
        Assert.IsType<CommandResult.Accepted>(results[2]);
        Assert.Equal("kept", world.TeamNote("alfa"));
        Assert.Equal("Annette", managers.Get(CommandLab.Anna).DisplayName);
        Assert.Equal("Annette", world.ManagerName(CommandLab.Anna.Value));
        Assert.Equal([1L, 3L], dispatcher.Log.Entries.Select(command => command.SubmissionNumber).ToArray());
    }

    [Fact]
    public void DecisionTraceRunsForAiOnlyAndBeforeExecute()
    {
        var (_, world, context) = CommandLab.Open(
            (CommandLab.Anna, ManagerKind.Human, "Anna"),
            (CommandLab.Bot, ManagerKind.Ai, "Bot"));
        var trace = new RecordingTrace();
        var dispatcher = CommandLab.NotesAndNames(trace);
        var queue = new CommandQueue();
        var human = queue.Enqueue(new SetTeamNoteCommand
        {
            ManagerId = CommandLab.Anna,
            IssuedOn = CommandLab.OpeningDay,
            TeamId = "alfa",
            Note = "human",
        });
        Assert.IsType<CommandResult.Accepted>(dispatcher.Dispatch(human, context));
        Assert.Empty(trace.HashesAtTrace);

        var ai = queue.Enqueue(new SetTeamNoteCommand
        {
            ManagerId = CommandLab.Bot,
            IssuedOn = CommandLab.OpeningDay,
            TeamId = "alfa",
            Note = "ai",
        });
        var hashBeforeAiExecute = world.ContentHash();
        var accepted = dispatcher.Dispatch(ai, context);

        Assert.IsType<CommandResult.Accepted>(accepted);
        Assert.Equal([ai.SubmissionNumber], trace.SubmissionNumbers);
        Assert.Equal([hashBeforeAiExecute], trace.HashesAtTrace);
        Assert.Equal("ai", world.TeamNote("alfa"));
        Assert.NotEqual(hashBeforeAiExecute, world.ContentHash());

        var rejected = dispatcher.Dispatch(
            queue.Enqueue(new SetTeamNoteCommand
            {
                ManagerId = CommandLab.Bot,
                IssuedOn = CommandLab.OpeningDay,
                TeamId = "alfa",
                Note = "",
            }),
            context);
        Assert.IsType<CommandResult.Rejected>(rejected);
        Assert.Single(trace.SubmissionNumbers);
    }

    [Fact]
    public void DefaultDecisionTraceDoesNotBlockAnAiCommand()
    {
        var (_, world, context) = CommandLab.Open((CommandLab.Bot, ManagerKind.Ai, "Bot"));
        var before = world.ContentHash();
        var dispatcher = CommandLab.NotesAndNames();
        var queued = new CommandQueue().Enqueue(new SetTeamNoteCommand
        {
            ManagerId = CommandLab.Bot,
            IssuedOn = CommandLab.OpeningDay,
            TeamId = "alfa",
            Note = "ai-note",
        });

        var result = dispatcher.Dispatch(queued, context);

        var accepted = Assert.IsType<CommandResult.Accepted>(result);
        var note = Assert.IsType<TeamNoteSet>(Assert.Single(accepted.Events));
        Assert.Equal("ai-note", note.Note);
        Assert.Equal(CommandLab.Bot, note.ManagerId);
        Assert.NotEqual(before, world.ContentHash());
    }

    [Fact]
    public void DispatchRejectsACommandTheQueueHasNotNumbered()
    {
        var (_, _, context) = CommandLab.Open((CommandLab.Anna, ManagerKind.Human, "Anna"));
        var dispatcher = CommandLab.NotesAndNames();
        var command = new SetTeamNoteCommand
        {
            ManagerId = CommandLab.Anna,
            IssuedOn = CommandLab.OpeningDay,
            TeamId = "alfa",
            Note = "x",
        };

        Assert.Throws<InvalidOperationException>(() => dispatcher.Dispatch(command, context));
    }

    [Fact]
    public void TwoManagersReplayToTheSameStateHash()
    {
        var first = RunCareer();
        var second = RunCareer();

        Assert.Equal(first.Hash, second.Hash);
        Assert.Equal(first.Order, second.Order);
        Assert.Equal("early-arrival-later-day", first.Note);
        Assert.Equal("Annette", first.AnnaName);
        Assert.Equal(new DateOnly(1950, 5, 14), first.Date);

        var replay = Replay(first.Log);
        Assert.Equal(first.Hash, replay.Hash);
        Assert.Equal(first.Note, replay.Note);
        Assert.Equal(first.AnnaName, replay.AnnaName);
        Assert.Equal(first.Date, replay.Date);
        Assert.Equal(first.Order, replay.Order);
    }

    [Fact]
    public void RenameIsWhatTheReadyGateShowsWhileWaiting()
    {
        var (managers, world, context) = CommandLab.Open(
            (CommandLab.Anna, ManagerKind.Human, "Anna"),
            (CommandLab.Bram, ManagerKind.Human, "Bram"));
        var dispatcher = CommandLab.NotesAndNames();
        var queued = new CommandQueue().Enqueue(new RenameManagerCommand
        {
            ManagerId = CommandLab.Anna,
            IssuedOn = CommandLab.OpeningDay,
            NewName = "Annette",
        });
        Assert.IsType<CommandResult.Accepted>(dispatcher.Dispatch(queued, context));
        managers.SetReady(CommandLab.Bram, true);

        var result = new ReadyGate().RequestAdvance(managers, world);

        var refused = Assert.IsType<AdvanceResult.Refused>(result);
        var waiting = Assert.Single(refused.Refusal.WaitingFor);
        Assert.Equal(CommandLab.Anna, waiting.ManagerId);
        Assert.Equal("Annette", waiting.DisplayName);
        Assert.Equal("Annette", waiting.Reason.Parameters["manager"]);
        Assert.Equal(TranslationKeys.WaitingFor, waiting.Reason.Key);
        Assert.Equal(CommandLab.OpeningDay, world.CurrentDate);
    }

    private static Career RunCareer()
    {
        var (managers, world, context) = CommandLab.Open(
            (CommandLab.Anna, ManagerKind.Human, "Anna"),
            (CommandLab.Bram, ManagerKind.Human, "Bram"));
        var queue = new CommandQueue();
        var dispatcher = CommandLab.NotesAndNames();

        queue.Enqueue(new SetTeamNoteCommand
        {
            ManagerId = CommandLab.Bram,
            IssuedOn = new DateOnly(1950, 5, 14),
            TeamId = "alfa",
            Note = "early-arrival-later-day",
        });
        queue.Enqueue(new SetTeamNoteCommand
        {
            ManagerId = CommandLab.Anna,
            IssuedOn = new DateOnly(1950, 5, 13),
            TeamId = "alfa",
            Note = "later-arrival-earlier-day",
        });
        queue.Enqueue(new RenameManagerCommand
        {
            ManagerId = CommandLab.Anna,
            IssuedOn = new DateOnly(1950, 5, 13),
            NewName = "Annette",
        });
        queue.Enqueue(new SetTeamNoteCommand
        {
            ManagerId = CommandLab.Bram,
            IssuedOn = new DateOnly(1950, 5, 13),
            TeamId = "alfa",
            Note = "same-day-after-rename",
        });

        var results = dispatcher.DispatchAll(queue, context);
        Assert.All(results, result => Assert.IsType<CommandResult.Accepted>(result));
        Assert.Equal([2L, 3L, 4L, 1L], dispatcher.Log.Entries.Select(command => command.SubmissionNumber).ToArray());

        managers.SetReady(CommandLab.Anna, true);
        managers.SetReady(CommandLab.Bram, true);
        Assert.IsType<AdvanceResult.Advanced>(new ReadyGate().RequestAdvance(managers, world));

        return new Career(
            world.ContentHash(),
            dispatcher.Log.Entries.ToArray(),
            world.TeamNote("alfa"),
            world.ManagerName(CommandLab.Anna.Value),
            world.CurrentDate);
    }

    private static Career Replay(IReadOnlyList<ICommand> log)
    {
        var (managers, world, context) = CommandLab.Open(
            (CommandLab.Anna, ManagerKind.Human, "Anna"),
            (CommandLab.Bram, ManagerKind.Human, "Bram"));
        var dispatcher = CommandLab.NotesAndNames();
        var results = dispatcher.Replay(log, context);
        Assert.All(results, result => Assert.IsType<CommandResult.Accepted>(result));

        managers.SetReady(CommandLab.Anna, true);
        managers.SetReady(CommandLab.Bram, true);
        Assert.IsType<AdvanceResult.Advanced>(new ReadyGate().RequestAdvance(managers, world));

        return new Career(
            world.ContentHash(),
            dispatcher.Log.Entries.ToArray(),
            world.TeamNote("alfa"),
            world.ManagerName(CommandLab.Anna.Value),
            world.CurrentDate);
    }

    private sealed record Career(
        string Hash,
        IReadOnlyList<ICommand> Log,
        string? Note,
        string? AnnaName,
        DateOnly Date)
    {
        public IReadOnlyList<long> Order => Log.Select(command => command.SubmissionNumber).ToArray();
    }

    private sealed class RecordingTrace : IDecisionTrace
    {
        public List<long> SubmissionNumbers { get; } = [];

        public List<string> HashesAtTrace { get; } = [];

        public void Record(ICommand command, IWorldState world)
        {
            SubmissionNumbers.Add(command.SubmissionNumber);
            HashesAtTrace.Add(world.ContentHash());
        }
    }
}
