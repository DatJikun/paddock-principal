using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Domain.Inbox;

namespace Paddock.Application.Racing;

/// <summary>
/// Resolves a team manager's stand-in driver decision for an upcoming race (PP-061, #219).
/// </summary>
public sealed class StandInResolver : IInboxResolver
{
    public const string Kind = "racing.standin";
    public const string OptionSkip = "skip";

    string IInboxResolver.Kind => Kind;

    public TranslationMessage? Validate(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);

        if (optionId == OptionSkip || item.Draft.Options.Any(o => o.Id == optionId))
        {
            return null;
        }

        return TranslationMessage.Of(InboxKeys.OptionUnknown);
    }

    public IReadOnlyList<IDomainEvent> Execute(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        // The resolved option is recorded on the InboxSection as ChosenOptionId by ResolveInboxItemHandler.
        return [];
    }
}
