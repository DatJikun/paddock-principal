using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Inbox;
using Paddock.Domain.World;

namespace Paddock.Application.Contracts;

/// <summary>
/// Carries out a manager's answer to a person's response (kind <see cref="ContractEngine.ResponseKind"/>): accept or sign,
/// walk away, or revise (keep negotiating, which only closes the item). It runs inside the inbox command, so the change is
/// that command's change (INV-001). Only the item's own manager reaches it, and the same checks as the direct commands apply.
/// </summary>
public sealed class NegotiationResponseResolver : IInboxResolver
{
    public string Kind => ContractEngine.ResponseKind;

    public TranslationMessage? Validate(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var engine = ContractEngine.From(context);
        var manager = new ManagerId(item.ManagerId);
        var negotiationId = item.Arguments[ContractEngine.NegotiationArgument];
        var today = InboxBook.ToGameDate(context.World.CurrentDate);
        switch (optionId)
        {
            case ContractEngine.OptionAccept:
            case ContractEngine.OptionSign:
                return engine.ValidateAccept(manager, negotiationId, today);
            case ContractEngine.OptionWalk:
                return engine.ValidateWalk(manager, negotiationId);
            case ContractEngine.OptionRevise:
                var negotiation = engine.Own(manager, negotiationId);
                if (negotiation is null)
                {
                    return TranslationMessage.Of(ContractKeys.UnknownNegotiation);
                }

                return negotiation.Status == Domain.Contracts.NegotiationStatus.Countered
                    ? null
                    : TranslationMessage.Of(ContractKeys.WrongStatus, ("status", negotiation.Status.ToString()));
            default:
                return TranslationMessage.Of(InboxKeys.OptionUnknown);
        }
    }

    public IReadOnlyList<IDomainEvent> Execute(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var engine = ContractEngine.From(context);
        var manager = new ManagerId(item.ManagerId);
        var negotiationId = item.Arguments[ContractEngine.NegotiationArgument];
        var today = InboxBook.ToGameDate(context.World.CurrentDate);
        switch (optionId)
        {
            case ContractEngine.OptionAccept:
            case ContractEngine.OptionSign:
                var negotiation = engine.Own(manager, negotiationId)
                    ?? throw new InvalidOperationException("Execute ran for an item that should have been rejected.");
                return engine.Sign(negotiation, today, item.Id);
            case ContractEngine.OptionWalk:
                return engine.WalkAway(manager, negotiationId, today, item.Id);
            default:
                return [];
        }
    }
}

/// <summary>
/// Carries out the employer's answer to the renewal prompt (kind <see cref="ContractEngine.RenewalKind"/>): renew opens a renewal
/// negotiation with the terms of the current contract as the first offer; release lets the contract run out.
/// </summary>
public sealed class RenewalPromptResolver : IInboxResolver
{
    public string Kind => ContractEngine.RenewalKind;

    public TranslationMessage? Validate(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        switch (optionId)
        {
            case ContractEngine.OptionRelease:
                return null;
            case ContractEngine.OptionExtend:
                var extender = ContractEngine.From(context);
                return extender.ValidateExtend(
                    new ManagerId(item.ManagerId),
                    ContractIdOf(item),
                    InboxBook.ToGameDate(context.World.CurrentDate));
            case ContractEngine.OptionRenew:
                var engine = ContractEngine.From(context);
                var today = InboxBook.ToGameDate(context.World.CurrentDate);
                var contract = engine.FindContract(ContractIdOf(item));
                if (contract is null)
                {
                    return TranslationMessage.Of(ContractKeys.UnknownContract);
                }

                return engine.ValidateRenew(
                    new ManagerId(item.ManagerId),
                    contract.Id,
                    false,
                    engine.DefaultRenewalOffer(contract, today),
                    null,
                    today);
            default:
                return TranslationMessage.Of(InboxKeys.OptionUnknown);
        }
    }

    public IReadOnlyList<IDomainEvent> Execute(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var engine = ContractEngine.From(context);
        var today = InboxBook.ToGameDate(context.World.CurrentDate);
        var manager = new ManagerId(item.ManagerId);
        switch (optionId)
        {
            case ContractEngine.OptionExtend:
                return engine.Extend(manager, ContractIdOf(item), today);
            case ContractEngine.OptionRenew:
                var contract = engine.FindContract(ContractIdOf(item))
                    ?? throw new InvalidOperationException("Execute ran for an item that should have been rejected.");
                return engine.OpenRenewal(manager, contract.Id, engine.DefaultRenewalOffer(contract, today), null, today);
            default:
                return [];
        }
    }

    /// <summary>
    /// When the default could not be carried out (the budget, the person, or a renewal already signed), the manager is told that
    /// the contract runs out: a prompt never lapses without a word.
    /// </summary>
    public IReadOnlyList<IDomainEvent> OnExpired(InboxItem item, string? appliedOptionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        if (appliedOptionId is not null)
        {
            return [];
        }

        var engine = ContractEngine.From(context);
        var contract = engine.FindContract(ContractIdOf(item));
        if (contract is not null)
        {
            engine.PostContractNotice(new ManagerId(item.ManagerId), contract, ContractKeys.NoticeExtendFailed, InboxBook.ToGameDate(context.World.CurrentDate));
        }

        return [];
    }

    internal static ContractId ContractIdOf(InboxItem item) => ContractIdOf(item.Arguments[ContractEngine.ContractArgument]);

    internal static ContractId ContractIdOf(string text) =>
        text.StartsWith("con:", StringComparison.Ordinal) && long.TryParse(text.AsSpan(4), out var sequence)
            ? ContractId.Generated(sequence)
            : default;
}

/// <summary>
/// Carries out the answer to the grouped renewal of staff who are not key staff (kind <see cref="ContractEngine.RenewalGroupKind"/>):
/// extend everyone on current terms, or let them all run out. Each person is checked on their own: the ones who agree and fit the
/// budget are extended, and the manager is told about the rest. The default applies after the deadline, so the group never holds the clock.
/// </summary>
public sealed class RenewalGroupResolver : IInboxResolver
{
    public string Kind => ContractEngine.RenewalGroupKind;

    public TranslationMessage? Validate(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        return optionId is ContractEngine.OptionExtend or ContractEngine.OptionRelease
            ? null
            : TranslationMessage.Of(InboxKeys.OptionUnknown);
    }

    public IReadOnlyList<IDomainEvent> Execute(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        if (optionId != ContractEngine.OptionExtend)
        {
            return [];
        }

        var engine = ContractEngine.From(context);
        var manager = new ManagerId(item.ManagerId);
        var today = InboxBook.ToGameDate(context.World.CurrentDate);
        var events = new List<IDomainEvent>();
        foreach (var text in item.Arguments[ContractEngine.ContractsArgument].Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var id = RenewalPromptResolver.ContractIdOf(text);
            if (engine.ValidateExtend(manager, id, today) is null)
            {
                events.AddRange(engine.Extend(manager, id, today));
            }
            else if (engine.FindContract(id) is Contract contract)
            {
                engine.PostContractNotice(manager, contract, ContractKeys.NoticeExtendFailed, today);
            }
        }

        return events;
    }
}
