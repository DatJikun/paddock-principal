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

/// <summary>Whether a career is open in this window, plus the year and seed the start screen offers.</summary>
public sealed record SessionView(bool Open, int SuggestedYear, string SuggestedSeed, string? ManagerId, string? Date, string? NoticeKey);

/// <summary>One existing team the player can take over. The name is the public one.</summary>
public sealed record PublicTeamView(string Id, string Name);

/// <summary>Teams present in <see cref="Year"/>, from public data only.</summary>
public sealed record TeamListView(int Year, IReadOnlyList<PublicTeamView> Teams);

/// <summary>Argument of <c>teams</c>.</summary>
public sealed record TeamsCall(string ManagerId, int Year);

/// <summary>One save in the window's save folder. <see cref="Name"/> is the file name without the extension.</summary>
public sealed record SaveSlotView(string Name, string Date, string TeamId, string ManagerName);

/// <summary>The saves the window can load.</summary>
public sealed record SaveListView(IReadOnlyList<SaveSlotView> Saves);

/// <summary>Argument of <c>startCareer</c>.</summary>
public sealed record StartCareerCall(
    string ManagerId,
    string Preset,
    int Year,
    string Fatality,
    string TeamId,
    string GivenName,
    string FamilyName,
    string Nationality,
    string Tilt,
    string Seed,
    string CareerName);

/// <summary>Argument of <c>loadCareer</c> and <c>saveCareer</c>. <see cref="Name"/> is a file name, not a path.</summary>
public sealed record SaveNameCall(string ManagerId, string Name);

/// <summary>A career that just opened. <see cref="NoticeKey"/> is set when people were generated because the history cache was missing.</summary>
public sealed record CareerOpenedView(string ManagerId, string Date, string TeamId, string Hash, string? NoticeKey);

/// <summary>One championship round on the placeholder calendar. Dates are ISO.</summary>
public sealed record CalendarRoundView(int Round, string LayoutId, string CircuitId, string RaceDate, string QualifyingDate, string PracticeDate);

/// <summary>The season's rounds, in round order.</summary>
public sealed record CalendarView(int Season, IReadOnlyList<CalendarRoundView> Rounds);

/// <summary>The next race whose race day is still ahead, or all null when the season has none left.</summary>
public sealed record NextRaceView(int? Round, string? LayoutId, string? CircuitId, string? Date);

/// <summary>One line of an empty or filled table. Points are the counted total, as text so a fraction is exact.</summary>
public sealed record StandingRowView(int Position, string Id, string Points);

/// <summary>Drivers' and constructors' tables under the season's points scale. Empty until a race has been scored.</summary>
public sealed record StandingsView(int Season, int Rounds, IReadOnlyList<int> PointsForPlace, IReadOnlyList<StandingRowView> Drivers, IReadOnlyList<StandingRowView> Constructors);

/// <summary>One staff contract on the player's team. Role is the staff role name.</summary>
public sealed record StaffMemberView(string PersonId, string Name, string Nationality, string Role, string End);

/// <summary>Staff the player employs. Hiring and the staff screen from the staff MVP are separate.</summary>
public sealed record OwnStaffView(IReadOnlyList<StaffMemberView> Own);

/// <summary>
/// A person on the market. <see cref="OrganizationId"/> is set for a contracted driver the team has a belief about,
/// and null for a free agent. Attributes are bands.
/// </summary>
public sealed record MarketPersonView(
    string PersonId,
    string Name,
    string Nationality,
    string? OrganizationId,
    string? FreeSince,
    IReadOnlyList<Paddock.Application.Contracts.KnownAttributeView> Known);

/// <summary>Free agents, plus contracted drivers this team has a belief about. Hidden truth stays out.</summary>
public sealed record MarketView(IReadOnlyList<MarketPersonView> FreeAgents, IReadOnlyList<MarketPersonView> Contracted);

/// <summary>Argument of <c>negotiateOpen</c>.</summary>
public sealed record NegotiateOpenCall(string ManagerId, string PersonId, string Subject, string? OrganizationId);

/// <summary>Argument of <c>negotiateOffer</c>.</summary>
public sealed record NegotiateOfferCall(string ManagerId, string NegotiationId, long Salary, int Years);

/// <summary>Argument of <c>negotiateAccept</c> and <c>negotiateWalk</c>.</summary>
public sealed record NegotiationIdCall(string ManagerId, string NegotiationId);

/// <summary>Argument of <c>negotiateRenew</c>.</summary>
public sealed record NegotiateRenewCall(string ManagerId, string ContractId, bool ExerciseOption, long Salary, int Years);

/// <summary>Argument of <c>sponsorBegin</c>.</summary>
public sealed record SponsorBeginCall(string ManagerId, string SponsorId, int Slot, string? OrganizationId);

/// <summary>Argument of <c>sponsorSign</c> and <c>sponsorWalk</c>.</summary>
public sealed record SponsorTalkCall(string ManagerId, string TalkId, string? OrganizationId);

/// <summary>Argument of <c>sponsorRespond</c>.</summary>
public sealed record SponsorRespondCall(string ManagerId, string OfferId, bool Accept, string? OrganizationId);

/// <summary>Argument of <c>developmentSplit</c>. Priorities use the command default when the page omits them.</summary>
public sealed record DevelopmentSplitCall(
    string ManagerId,
    int CurrentPercent,
    int AccountPercent,
    int NextYearPercent,
    int AeroPriority,
    int ChassisPriority,
    int ReliabilityPriority,
    int TyresPriority,
    string? OrganizationId);

/// <summary>Argument of <c>commitConcept</c>.</summary>
public sealed record ProjectCall(string ManagerId, string ProjectId, string? OrganizationId);

/// <summary>Argument of <c>approveConcept</c>. The six values are thousandths of the concept.</summary>
public sealed record ApproveConceptCall(
    string ManagerId,
    int AeroMilli,
    int PhilosophyMilli,
    int WindowMilli,
    int CoolingMilli,
    int TyreMilli,
    int IntegrationMilli,
    string? OrganizationId);

/// <summary>Argument of <c>scoutFocus</c>. A null handle scouts the whole pool.</summary>
public sealed record ScoutFocusCall(string ManagerId, string? PersonHandle);

/// <summary>Argument of <c>fundJunior</c>.</summary>
public sealed record FundJuniorCall(string ManagerId, string PersonHandle, string Programme);

/// <summary>Argument of <c>signPoolDriver</c>.</summary>
public sealed record SignPoolCall(string ManagerId, string PersonHandle, string Role);

/// <summary>Argument of <c>supplyPropose</c>.</summary>
public sealed record SupplyProposeCall(
    string ManagerId,
    string SupplierId,
    string Item,
    string Kind,
    int FirstSeason,
    long AnnualPriceCents,
    int Seasons,
    bool Exclusive,
    string NegotiationId,
    string? OrganizationId);

/// <summary>Argument of <c>supplyRespond</c>.</summary>
public sealed record SupplyRespondCall(string ManagerId, string NegotiationId, bool Accept, string? OrganizationId);
