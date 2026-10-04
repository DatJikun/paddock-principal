using Paddock.Application.Commands;
using Paddock.Application.Managers;
using Paddock.Simulation.Time;

namespace Paddock.Application.Board;

/// <summary>Type ids of the facts the board emits. Machine identifiers, not player text.</summary>
public static class BoardEventTypes
{
    /// <summary>A principal (a manager or an AI principal) was dismissed after the board lost confidence.</summary>
    public const string ManagerDismissed = "board.managerDismissed";

    /// <summary>A manager resigned from a team.</summary>
    public const string ManagerResigned = "board.managerResigned";

    /// <summary>A new principal runs a team: an AI principal hired from the candidates, or a manager who took a job.</summary>
    public const string ManagerAppointed = "board.managerAppointed";

    /// <summary>A job offer reached a dismissed manager.</summary>
    public const string JobOffered = "board.jobOffered";
}

/// <summary>
/// What happened to one principal of one team. <see cref="Subject"/> is a manager id or a person id (<see cref="Kind"/> says which),
/// <see cref="Other"/> the other party (the one who replaced them, or who they replaced), <see cref="Archetype"/> the archetype the
/// new AI principal rerolled to (empty for a human), <see cref="Amount"/> the severance. Emitted by the day clock and wrapped as an
/// event by the commands; it is never scheduled, so the event queue hash needs no form for it.
/// </summary>
public sealed record BoardFactPayload : EventPayload
{
    public BoardFactPayload(string organization, string subject, string kind, string other, string archetype, string reasonKey, long amount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organization);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(other);
        ArgumentNullException.ThrowIfNull(archetype);
        ArgumentNullException.ThrowIfNull(reasonKey);
        Organization = organization;
        Subject = subject;
        Kind = kind;
        Other = other;
        Archetype = archetype;
        ReasonKey = reasonKey;
        Amount = amount;
    }

    public string Organization { get; }

    public string Subject { get; }

    /// <summary>"Human" or "Ai".</summary>
    public string Kind { get; }

    public string Other { get; }

    public string Archetype { get; }

    public string ReasonKey { get; }

    public long Amount { get; }
}

/// <summary>A board fact as the domain event of a command.</summary>
public sealed record BoardFactEvent(ManagerId ManagerId, DateOnly OccurredOn, string TypeId, BoardFactPayload Fact) : IDomainEvent;

/// <summary>One thing the board did, before it is wrapped for the day clock or a command.</summary>
public sealed record BoardFact(string TypeId, BoardFactPayload Payload);
