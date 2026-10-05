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

/// <summary>One row of the championship table. Points are the counted total for the era.</summary>
public sealed record StandingRowView(int Position, string Id, double Points);

/// <summary>Drivers' and constructors' tables for the season in progress. Empty until the first race.</summary>
public sealed record StandingsView(
    int Season,
    int RoundsCompleted,
    int TotalRounds,
    IReadOnlyList<StandingRowView> Drivers,
    IReadOnlyList<StandingRowView> Constructors);

/// <summary>One car in a race result. Public facts only.</summary>
public sealed record RaceResultRowView(int Position, bool Classified, string DriverId, string TeamId, string Points);

/// <summary>The classification of the last race the career ran.</summary>
public sealed record RaceResultView(int Season, int Round, string LayoutId, IReadOnlyList<RaceResultRowView> Rows);

/// <summary>One line of a race report: a translation key and the values that fill it.</summary>
public sealed record RaceReportLineView(string Key, IReadOnlyDictionary<string, string> Args);

/// <summary>One section of a race report.</summary>
public sealed record RaceReportSectionView(RaceReportLineView Title, IReadOnlyList<RaceReportLineView> Lines);

/// <summary>The narrative of the last race. It is not stored in the save; only the latest race is still in memory.</summary>
public sealed record RaceReportView(
    int Season,
    int Round,
    string LayoutId,
    RaceReportLineView Title,
    IReadOnlyList<RaceReportSectionView> Sections);

/// <summary>A day that actually moved. The date is the new morning, ISO.</summary>
public sealed record AdvanceDayView(string Date);

/// <summary>A command the host accepted. The world changed only through that command (INV-001).</summary>
public sealed record CommandAck(bool Accepted);
