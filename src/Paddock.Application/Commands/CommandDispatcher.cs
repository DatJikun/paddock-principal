using Paddock.Application.Managers;

namespace Paddock.Application.Commands;

/// <summary>
/// Pipeline for one command: known manager, known handler, validate, AI decision-trace hook, execute, log.
/// A rejected command does not change state and is not logged. The trace hook runs only for <see cref="ManagerKind.Ai"/>.
/// </summary>
public sealed class CommandDispatcher
{
    private readonly Dictionary<Type, ICommandHandler> _handlers = [];
    private readonly IDecisionTrace _trace;
    private readonly CommandLog _log;

    public CommandDispatcher(IDecisionTrace? decisionTrace = null, CommandLog? log = null)
    {
        _trace = decisionTrace ?? new NoOpDecisionTrace();
        _log = log ?? new CommandLog();
    }

    public CommandLog Log => _log;

    public void Register(ICommandHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(handler.CommandType);
        if (!_handlers.TryAdd(handler.CommandType, handler))
        {
            throw new InvalidOperationException($"A handler for {handler.CommandType.Name} is already registered.");
        }
    }

    public CommandResult Dispatch(ICommand command, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        if (command.SubmissionNumber <= 0)
        {
            throw new InvalidOperationException("Dispatch only commands that the queue has numbered.");
        }

        if (!context.Managers.Contains(command.ManagerId))
        {
            return new CommandResult.Rejected(
                TranslationMessage.Of(TranslationKeys.ManagerUnknown, ("managerId", command.ManagerId.Value)));
        }

        if (!_handlers.TryGetValue(command.GetType(), out var handler))
        {
            return new CommandResult.Rejected(
                TranslationMessage.Of(TranslationKeys.UnknownCommand, ("command", command.GetType().Name)));
        }

        var rejection = handler.Validate(command, context);
        if (rejection is not null)
        {
            return new CommandResult.Rejected(rejection);
        }

        if (context.Managers.KindOf(command.ManagerId) == ManagerKind.Ai)
        {
            _trace.Record(command, context.World);
        }

        var events = handler.Execute(command, context);
        ArgumentNullException.ThrowIfNull(events);
        var copy = events.ToArray();
        _log.Append(command);
        return new CommandResult.Accepted(copy);
    }

    public IReadOnlyList<CommandResult> DispatchAll(CommandQueue queue, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(context);
        var batch = queue.DequeueAll();
        var results = new List<CommandResult>(batch.Count);
        foreach (var command in batch)
        {
            results.Add(Dispatch(command, context));
        }

        return results;
    }

    public IReadOnlyList<CommandResult> Replay(IReadOnlyList<ICommand> commands, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(context);
        var results = new List<CommandResult>(commands.Count);
        foreach (var command in commands)
        {
            results.Add(Dispatch(command, context));
        }

        return results;
    }
}
