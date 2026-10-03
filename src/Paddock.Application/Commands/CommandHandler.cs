namespace Paddock.Application.Commands;

public abstract class CommandHandler<TCommand> : ICommandHandler
    where TCommand : ICommand
{
    public Type CommandType => typeof(TCommand);

    public TranslationMessage? Validate(ICommand command, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        return ValidateTyped(Cast(command), context);
    }

    public IReadOnlyList<IDomainEvent> Execute(ICommand command, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        return ExecuteTyped(Cast(command), context);
    }

    protected abstract TranslationMessage? ValidateTyped(TCommand command, CommandContext context);

    protected abstract IReadOnlyList<IDomainEvent> ExecuteTyped(TCommand command, CommandContext context);

    private static TCommand Cast(ICommand command)
    {
        if (command is TCommand typed)
        {
            return typed;
        }

        throw new ArgumentException($"Expected {typeof(TCommand).Name}.", nameof(command));
    }
}
