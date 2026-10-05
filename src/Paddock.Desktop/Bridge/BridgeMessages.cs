namespace Paddock.Desktop.Bridge;

/// <summary>Argument of every query and of <c>advanceDay</c>. The id is the human manager the view belongs to (INV-003).</summary>
public sealed record ManagerCall(string ManagerId);

/// <summary>Argument of <c>resolveInbox</c>.</summary>
public sealed record ResolveInboxCall(string ManagerId, string ItemId, string OptionId);

/// <summary>Argument of <c>dismissInbox</c>.</summary>
public sealed record DismissInboxCall(string ManagerId, string ItemId);

/// <summary>
/// The top bar: the career date, the team's cash in integer cents, and whatever is holding the clock.
/// Cash is null when this manager does not run a team with open books.
/// </summary>
public sealed record ShellView(
    string ManagerId,
    string Date,
    long? CashCents,
    int InboxOpen,
    int InboxDecisions,
    string? BlockingKind,
    string? DecisionItemId,
    string? DecisionSubjectKey,
    string? OrganizationId,
    string? OrganizationName);

/// <summary>The team this manager runs, as the shell and the team screen both read it.</summary>
public sealed record OwnTeamView(string? OrganizationId, string? Name, long? CashCents);

/// <summary>One of the manager's own drivers. Seat is the contract's seat name. End is an ISO date.</summary>
public sealed record OwnDriverView(string PersonId, string Name, string Nationality, string Seat, string End);

/// <summary>A person with no contract, as this manager may see them. Attributes stay out until a later screen.</summary>
public sealed record MarketDriverView(string PersonId, string Name, string Nationality, string? FreeSince);

/// <summary>Own drivers, plus people with no contract the manager is allowed to see.</summary>
public sealed record DriversView(IReadOnlyList<OwnDriverView> Own, IReadOnlyList<MarketDriverView> Market);

/// <summary>A day that actually moved. The date is the new morning, ISO.</summary>
public sealed record AdvanceDayView(string Date);

/// <summary>A command the host accepted. The world changed only through that command (INV-001).</summary>
public sealed record CommandAck(bool Accepted);
