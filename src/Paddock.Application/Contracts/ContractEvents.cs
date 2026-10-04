using Paddock.Application.Commands;
using Paddock.Application.Managers;
using Paddock.Domain.Contracts;

namespace Paddock.Application.Contracts;

/// <summary>A negotiation was opened.</summary>
public sealed record NegotiationOpened(ManagerId ManagerId, DateOnly OccurredOn, string NegotiationId, string PersonId) : IDomainEvent
{
    public string TypeId => "negotiation.opened";
}

/// <summary>An offer was made. <paramref name="WasNudge"/> is true when it was no real change and cost interest.</summary>
public sealed record OfferSubmitted(ManagerId ManagerId, DateOnly OccurredOn, string NegotiationId, int Round, bool WasNudge) : IDomainEvent
{
    public string TypeId => "negotiation.offer.submitted";
}

/// <summary>A negotiation ended without a contract (the manager walked away, or it was closed by a command).</summary>
public sealed record NegotiationEnded(ManagerId ManagerId, DateOnly OccurredOn, string NegotiationId, NegotiationStatus Status) : IDomainEvent
{
    public string TypeId => "negotiation.ended";
}

/// <summary>A contract was signed from a negotiation.</summary>
public sealed record ContractSigned(ManagerId ManagerId, DateOnly OccurredOn, string NegotiationId, string ContractId) : IDomainEvent
{
    public string TypeId => "contract.signed";
}

/// <summary>An employer ended a contract early and paid <paramref name="Compensation"/>.</summary>
public sealed record ContractTerminated(ManagerId ManagerId, DateOnly OccurredOn, string ContractId, long Compensation) : IDomainEvent
{
    public string TypeId => "contract.terminated";
}

/// <summary>The team used its extension option, so the contract runs longer on the same terms.</summary>
public sealed record OptionExercised(ManagerId ManagerId, DateOnly OccurredOn, string ContractId, DateOnly NewEnd) : IDomainEvent
{
    public string TypeId => "contract.optionExercised";
}

/// <summary>Type ids of the facts the day handlers emit. Machine identifiers, not player text. The marker is an id.</summary>
public static class ContractEventTypes
{
    /// <summary>Scheduled for the day a person answers an offer. Marker: <c>neg:{n}#{round}</c>.</summary>
    public const string Respond = "negotiation.respond";

    public const string Countered = "negotiation.countered";

    public const string PersonAgreed = "negotiation.personAgreed";

    public const string Considering = "negotiation.considering";

    public const string Refused = "negotiation.refused";

    public const string Lost = "negotiation.lost";

    public const string Lapsed = "negotiation.lapsed";

    /// <summary>A contract was signed without a manager's command (the proposer is an AI manager). Marker: contract id.</summary>
    public const string Signed = "contract.signed";

    /// <summary>The renewal prompt went out. Marker: contract id.</summary>
    public const string RenewalPrompt = "contract.renewalPrompt";

    /// <summary>A person extended the contract with an option they held. Marker: contract id.</summary>
    public const string OptionExercised = "contract.optionExercised";

    /// <summary>A person left through an exit clause. Marker: contract id.</summary>
    public const string ExitExercised = "contract.exitExercised";

    /// <summary>A person has no contract the day after one ended: a free agent, listed publicly. Marker: person id.</summary>
    public const string FreeAgent = "contract.freeAgent";
}
