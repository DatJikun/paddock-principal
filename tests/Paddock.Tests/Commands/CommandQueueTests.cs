using Paddock.Application.Commands;
using Paddock.Application.Managers;

namespace Paddock.Tests.Commands;

public class CommandQueueTests
{
    [Fact]
    public void OrdersByIssuedDayThenSubmissionNumber()
    {
        var queue = new CommandQueue();
        var laterDay = queue.Enqueue(Note(CommandLab.Bram, new DateOnly(1950, 5, 14), "later-day"));
        var earlierDay = queue.Enqueue(Note(CommandLab.Anna, new DateOnly(1950, 5, 13), "earlier-day"));
        var sameDay = queue.Enqueue(Note(CommandLab.Bram, new DateOnly(1950, 5, 13), "same-day"));

        Assert.Equal(1, laterDay.SubmissionNumber);
        Assert.Equal(2, earlierDay.SubmissionNumber);
        Assert.Equal(3, sameDay.SubmissionNumber);

        var pending = queue.Pending;
        Assert.Equal([2L, 3L, 1L], pending.Select(command => command.SubmissionNumber).ToArray());
        Assert.Equal(
            ["earlier-day", "same-day", "later-day"],
            pending.Cast<SetTeamNoteCommand>().Select(command => command.Note).ToArray());
    }

    [Fact]
    public void SubmissionNumbersKeepIncreasingAfterDequeue()
    {
        var queue = new CommandQueue();
        queue.Enqueue(Note(CommandLab.Anna, CommandLab.OpeningDay, "one"));
        queue.Enqueue(Note(CommandLab.Bram, CommandLab.OpeningDay, "two"));
        var firstBatch = queue.DequeueAll();
        Assert.Equal([1L, 2L], firstBatch.Select(command => command.SubmissionNumber).ToArray());
        Assert.Empty(queue.Pending);

        var third = queue.Enqueue(Note(CommandLab.Anna, CommandLab.OpeningDay, "three"));
        Assert.Equal(3, third.SubmissionNumber);
    }

    [Fact]
    public void EnqueueRejectsACommandThatAlreadyHasASubmissionNumber()
    {
        var queue = new CommandQueue();
        var queued = queue.Enqueue(Note(CommandLab.Anna, CommandLab.OpeningDay, "one"));
        Assert.Throws<InvalidOperationException>(() => queue.Enqueue(queued));
    }

    [Fact]
    public void EnqueueRejectsAMissingManagerId()
    {
        var queue = new CommandQueue();
        var command = new SetTeamNoteCommand
        {
            ManagerId = default,
            IssuedOn = CommandLab.OpeningDay,
            TeamId = "alfa",
            Note = "one",
        };

        Assert.Throws<ArgumentException>(() => queue.Enqueue(command));
    }

    private static SetTeamNoteCommand Note(ManagerId managerId, DateOnly issuedOn, string note)
    {
        return new SetTeamNoteCommand
        {
            ManagerId = managerId,
            IssuedOn = issuedOn,
            TeamId = "alfa",
            Note = note,
        };
    }
}
