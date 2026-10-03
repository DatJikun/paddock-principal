namespace Paddock.Application.Commands;

/// <summary>
/// In-memory record of accepted commands, in execution order.
/// Persistence is T19; this log exists so a fresh state can replay the same sequence (INV-002).
/// </summary>
public sealed class CommandLog
{
    private readonly List<ICommand> _entries = [];

    public IReadOnlyList<ICommand> Entries => _entries;

    public void Append(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.SubmissionNumber <= 0)
        {
            throw new ArgumentException("Only numbered commands belong in the log.", nameof(command));
        }

        _entries.Add(command);
    }
}
