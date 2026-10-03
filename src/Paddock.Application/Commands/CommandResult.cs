namespace Paddock.Application.Commands;

/// <summary>
/// Outcome of one command. <see cref="Rejected"/> does not change world state.
/// </summary>
public abstract record CommandResult
{
    private CommandResult()
    {
    }

    public sealed record Accepted(IReadOnlyList<IDomainEvent> Events) : CommandResult;

    public sealed record Rejected(TranslationMessage Reason) : CommandResult;
}
