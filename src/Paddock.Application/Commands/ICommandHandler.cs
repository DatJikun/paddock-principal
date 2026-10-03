namespace Paddock.Application.Commands;

public interface ICommandHandler
{
    Type CommandType { get; }

    TranslationMessage? Validate(ICommand command, CommandContext context);

    IReadOnlyList<IDomainEvent> Execute(ICommand command, CommandContext context);
}
