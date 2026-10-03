namespace Paddock.Application.Commands;

/// <summary>
/// Single host queue. Pending commands are ordered by <see cref="ICommand.IssuedOn"/>,
/// then by <see cref="ICommand.SubmissionNumber"/>. Submission numbers increase by one
/// for each accepted enqueue and are never reused, including after <see cref="DequeueAll"/>.
/// The queue does not drop commands dated after the current day; T16's tick decides
/// which day is resolved. Not thread-safe (one host, no network in this task).
/// </summary>
public sealed class CommandQueue
{
    private readonly List<ICommand> _pending = [];
    private long _nextSubmissionNumber = 1;

    public int Count => _pending.Count;

    public IReadOnlyList<ICommand> Pending => OrderedCopy();

    public ICommand Enqueue(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!command.ManagerId.IsAssigned)
        {
            throw new ArgumentException("A command requires a manager id.", nameof(command));
        }

        if (command.SubmissionNumber != 0)
        {
            throw new InvalidOperationException("Submission numbers are assigned by the queue.");
        }

        var number = _nextSubmissionNumber;
        if (number <= 0 || number == long.MaxValue)
        {
            throw new InvalidOperationException("The submission counter is exhausted.");
        }

        var queued = command.WithSubmissionNumber(number);
        if (queued is null)
        {
            throw new InvalidOperationException("Command did not accept the assigned submission number.");
        }

        if (queued.SubmissionNumber != number
            || queued.ManagerId != command.ManagerId
            || queued.IssuedOn != command.IssuedOn)
        {
            throw new InvalidOperationException("Command did not keep its identity when accepting a submission number.");
        }

        _nextSubmissionNumber = number + 1;
        _pending.Add(queued);
        return queued;
    }

    public IReadOnlyList<ICommand> DequeueAll()
    {
        var ordered = OrderedCopy();
        _pending.Clear();
        return ordered;
    }

    private ICommand[] OrderedCopy()
    {
        var ordered = new ICommand[_pending.Count];
        for (var i = 0; i < _pending.Count; i++)
        {
            ordered[i] = _pending[i];
        }

        Array.Sort(ordered, Compare);
        return ordered;
    }

    private static int Compare(ICommand left, ICommand right)
    {
        var byDate = left.IssuedOn.CompareTo(right.IssuedOn);
        if (byDate != 0)
        {
            return byDate;
        }

        return left.SubmissionNumber.CompareTo(right.SubmissionNumber);
    }
}
