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
        if (optionId != ContractEngine.OptionRenew)
        {
            return [];
        }

        var engine = ContractEngine.From(context);
        var today = InboxBook.ToGameDate(context.World.CurrentDate);
        var contract = engine.FindContract(ContractIdOf(item))
            ?? throw new InvalidOperationException("Execute ran for an item that should have been rejected.");
        return engine.OpenRenewal(new ManagerId(item.ManagerId), contract.Id, engine.DefaultRenewalOffer(contract, today), null, today);
    }

    private static ContractId ContractIdOf(InboxItem item)
    {
        var text = item.Arguments[ContractEngine.ContractArgument];
        return text.StartsWith("con:", StringComparison.Ordinal) && long.TryParse(text.AsSpan(4), out var sequence)
            ? ContractId.Generated(sequence)
            : default;
    }
}
