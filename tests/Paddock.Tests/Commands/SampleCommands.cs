using Paddock.Application.Commands;
using Paddock.Application.Managers;
using Paddock.Application.World;

namespace Paddock.Tests.Commands;

internal static class SampleText
{
    public const string NoteEmpty = "command.setTeamNote.empty";

    public const string TeamRequired = "command.setTeamNote.teamRequired";

    public const string NameEmpty = "command.renameManager.empty";
}

internal sealed record SetTeamNoteCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string TeamId { get; init; }

    public required string Note { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

internal sealed record RenameManagerCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string NewName { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

internal sealed record TeamNoteSet(ManagerId ManagerId, DateOnly OccurredOn, string TeamId, string Note) : IDomainEvent
{
    public string TypeId => "team-note-set";
}

internal sealed record ManagerRenamed(ManagerId ManagerId, DateOnly OccurredOn, string NewName) : IDomainEvent
{
    public string TypeId => "manager-renamed";
}

internal sealed class SetTeamNoteHandler : CommandHandler<SetTeamNoteCommand>
{
    protected override TranslationMessage? ValidateTyped(SetTeamNoteCommand command, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.TeamId))
        {
            return TranslationMessage.Of(SampleText.TeamRequired);
        }

        if (string.IsNullOrWhiteSpace(command.Note))
        {
            return TranslationMessage.Of(SampleText.NoteEmpty);
        }

        return null;
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(SetTeamNoteCommand command, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.TeamId) || string.IsNullOrWhiteSpace(command.Note))
        {
            throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        }

        World(context).SetTeamNote(command.TeamId, command.Note);
        return [new TeamNoteSet(command.ManagerId, command.IssuedOn, command.TeamId, command.Note)];
    }

    private static StubWorldState World(CommandContext context)
    {
        if (context.World is not StubWorldState world)
        {
            throw new InvalidOperationException(
                "Sample commands mutate StubWorldState. T16's world state needs its own handlers.");
        }

        return world;
    }
}

internal sealed class RenameManagerHandler : CommandHandler<RenameManagerCommand>
{
    protected override TranslationMessage? ValidateTyped(RenameManagerCommand command, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.NewName))
        {
            return TranslationMessage.Of(SampleText.NameEmpty);
        }

        return null;
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(RenameManagerCommand command, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.NewName))
        {
            throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        }

        context.Managers.Rename(command.ManagerId, command.NewName);
        World(context).SetManagerName(command.ManagerId.Value, command.NewName);
        return [new ManagerRenamed(command.ManagerId, command.IssuedOn, command.NewName)];
    }

    private static StubWorldState World(CommandContext context)
    {
        if (context.World is not StubWorldState world)
        {
            throw new InvalidOperationException(
                "Sample commands mutate StubWorldState. T16's world state needs its own handlers.");
        }

        return world;
    }
}
