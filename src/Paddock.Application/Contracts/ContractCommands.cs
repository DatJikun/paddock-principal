using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Contracts;
using Paddock.Domain.World;

namespace Paddock.Application.Contracts;

/// <summary>
/// A manager opens a negotiation with a person about a driver seat or a key staff role. The manager has to run
/// <see cref="Organization"/>. A person under contract can be approached only in the last year of it (or when an exit clause
/// has triggered); the current employer uses <see cref="RenewContractCommand"/>.
/// </summary>
public sealed record OpenNegotiationCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required OrganizationId Organization { get; init; }

    public required PersonId Person { get; init; }

    public required NegotiationSubject Subject { get; init; }

    /// <summary>The last day the offer stands. Null means <see cref="NegotiationEstimates.DefaultDeadlineDays"/> days from today.</summary>
    public DateOnly? Deadline { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>
/// A manager makes an offer in a negotiation. It uses a round; an offer that is no real change from the last one is a nudge
/// and costs the person's interest. The person answers after a delay, and the answer comes as an inbox item.
/// </summary>
public sealed record SubmitOfferCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string NegotiationId { get; init; }

    public required OfferTerms Terms { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>
/// A manager accepts the terms the person holds to (a counter, or the offer itself once the person agreed). The contract is signed.
/// </summary>
public sealed record AcceptCounterOfferCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string NegotiationId { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>A manager ends a negotiation.</summary>
public sealed record WalkAwayCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string NegotiationId { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>
/// The employer renews a contract. Either it uses the extension option it holds (<see cref="ExerciseOption"/>, the contract runs
/// on for the option's years on the same terms), or it opens a renewal negotiation with the first offer
/// (<see cref="Offer"/>). The renewed contract starts the day after the old one ends.
/// </summary>
public sealed record RenewContractCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required ContractId Contract { get; init; }

    public bool ExerciseOption { get; init; }

    public OfferTerms? Offer { get; init; }

    public DateOnly? Deadline { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>
/// The employer ends a contract early and pays <see cref="Compensation"/>, at least half of the remaining salary
/// (ESTIMATE, <see cref="NegotiationEstimates.TerminationShare"/>). The contract ends today.
/// </summary>
public sealed record TerminateContractCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required ContractId Contract { get; init; }

    public required long Compensation { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

public sealed class OpenNegotiationHandler : CommandHandler<OpenNegotiationCommand>
{
    protected override TranslationMessage? ValidateTyped(OpenNegotiationCommand command, CommandContext context) =>
        ContractEngine.From(context).ValidateOpen(
            command.ManagerId,
            command.Organization,
            command.Person,
            command.Subject,
            InboxBook.ToGameDate(command.IssuedOn),
            command.Deadline,
            null);

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(OpenNegotiationCommand command, CommandContext context) =>
        ContractEngine.From(context)
            .Open(command.ManagerId, command.Organization, command.Person, command.Subject, InboxBook.ToGameDate(command.IssuedOn), command.Deadline, null)
            .Events;
}

public sealed class SubmitOfferHandler : CommandHandler<SubmitOfferCommand>
{
    protected override TranslationMessage? ValidateTyped(SubmitOfferCommand command, CommandContext context) =>
        ContractEngine.From(context).ValidateSubmit(command.ManagerId, command.NegotiationId, command.Terms, InboxBook.ToGameDate(command.IssuedOn));

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(SubmitOfferCommand command, CommandContext context) =>
        ContractEngine.From(context).Submit(command.ManagerId, command.NegotiationId, command.Terms, InboxBook.ToGameDate(command.IssuedOn));
}

public sealed class AcceptCounterOfferHandler : CommandHandler<AcceptCounterOfferCommand>
{
    protected override TranslationMessage? ValidateTyped(AcceptCounterOfferCommand command, CommandContext context) =>
        ContractEngine.From(context).ValidateAccept(command.ManagerId, command.NegotiationId, InboxBook.ToGameDate(command.IssuedOn));

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(AcceptCounterOfferCommand command, CommandContext context)
    {
        var engine = ContractEngine.From(context);
        var negotiation = engine.Own(command.ManagerId, command.NegotiationId)
            ?? throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        return engine.Sign(negotiation, InboxBook.ToGameDate(command.IssuedOn));
    }
}

public sealed class WalkAwayHandler : CommandHandler<WalkAwayCommand>
{
    protected override TranslationMessage? ValidateTyped(WalkAwayCommand command, CommandContext context) =>
        ContractEngine.From(context).ValidateWalk(command.ManagerId, command.NegotiationId);

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(WalkAwayCommand command, CommandContext context) =>
        ContractEngine.From(context).WalkAway(command.ManagerId, command.NegotiationId, InboxBook.ToGameDate(command.IssuedOn));
}

public sealed class RenewContractHandler : CommandHandler<RenewContractCommand>
{
    protected override TranslationMessage? ValidateTyped(RenewContractCommand command, CommandContext context) =>
        ContractEngine.From(context).ValidateRenew(
            command.ManagerId,
            command.Contract,
            command.ExerciseOption,
            command.Offer,
            command.Deadline,
            InboxBook.ToGameDate(command.IssuedOn));

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(RenewContractCommand command, CommandContext context)
    {
        var engine = ContractEngine.From(context);
        var today = InboxBook.ToGameDate(command.IssuedOn);
        if (command.ExerciseOption)
        {
            return engine.RenewByOption(command.ManagerId, command.Contract, today);
        }

        return engine.OpenRenewal(command.ManagerId, command.Contract, command.Offer!, command.Deadline, today);
    }
}

public sealed class TerminateContractHandler : CommandHandler<TerminateContractCommand>
{
    protected override TranslationMessage? ValidateTyped(TerminateContractCommand command, CommandContext context) =>
        ContractEngine.From(context).ValidateTerminate(command.ManagerId, command.Contract, command.Compensation, InboxBook.ToGameDate(command.IssuedOn));

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(TerminateContractCommand command, CommandContext context) =>
        ContractEngine.From(context).Terminate(command.ManagerId, command.Contract, command.Compensation, InboxBook.ToGameDate(command.IssuedOn));
}

/// <summary>Registers the six contract handlers and the inbox resolvers they need.</summary>
public static class ContractRegistration
{
    public static void Register(CommandDispatcher dispatcher, InboxResolvers resolvers)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(resolvers);
        dispatcher.Register(new OpenNegotiationHandler());
        dispatcher.Register(new SubmitOfferHandler());
        dispatcher.Register(new AcceptCounterOfferHandler());
        dispatcher.Register(new WalkAwayHandler());
        dispatcher.Register(new RenewContractHandler());
        dispatcher.Register(new TerminateContractHandler());
        resolvers.Register(new NegotiationResponseResolver());
        resolvers.Register(new RenewalPromptResolver());
        resolvers.Register(new RenewalGroupResolver());
        resolvers.Register(new RaiseDemandResolver());
    }
}
