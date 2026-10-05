namespace Paddock.Desktop.Bridge;

/// <summary>Whether a career is open. The start screen reads this before any other query.</summary>
public sealed record SessionView(
    bool Started,
    string ManagerId,
    string? Date,
    string? OrganizationId,
    string? OrganizationName,
    string? PeopleNoticeKey,
    int SuggestedYear,
    string SuggestedSeed);

/// <summary>One existing team the player may take over.</summary>
public sealed record TeamOptionView(string Id, string Name);

/// <summary>Public teams of one season.</summary>
public sealed record TeamListView(int Year, IReadOnlyList<TeamOptionView> Teams);

/// <summary>Argument of <c>teams</c>. Year defaults to 1955 when omitted; the record carries it because the page always sends it.</summary>
public sealed record TeamsCall(string ManagerId, int Year);

/// <summary>One save the page can load.</summary>
public sealed record SaveListItem(string Name, string Date, string TeamId);

/// <summary>Saves in the career folder.</summary>
public sealed record SaveListView(IReadOnlyList<SaveListItem> Saves);

/// <summary>
/// Starts a career. Preset defaults to Chaos and the year to 1955. Axes override the preset when set.
/// People fall back to generated names when the local cache is missing.
/// </summary>
public sealed record NewCareerCall(
    string ManagerId,
    string TeamId,
    string GivenName,
    string FamilyName,
    string Nationality,
    string Tilt,
    string? Preset,
    int? Year,
    ulong? Seed,
    string? Name,
    string? People,
    string? Rules,
    string? Ai,
    int? History,
    int? Randomness,
    string? Fatality,
    bool? NoNumbers);

/// <summary>What <c>newCareer</c> and <c>loadCareer</c> return.</summary>
public sealed record CareerStartedView(
    string ManagerId,
    string Date,
    string OrganizationId,
    string Hash,
    string? PeopleNoticeKey);

/// <summary>Loads a save by file name or by a path ending in <c>.paddock</c>.</summary>
public sealed record LoadCareerCall(string ManagerId, string Path);

/// <summary>Saves the morning. <see cref="Name"/> is a file name, not a directory.</summary>
public sealed record SaveCareerCall(string ManagerId, string Name);

/// <summary>A save that was written. The hash is the world hash.</summary>
public sealed record CareerSavedView(string Name, string Hash);

/// <summary>Argument of <c>raceResult</c>. Both null means the latest finished round.</summary>
public sealed record RaceResultCall(string ManagerId, int? Season, int? Round);

/// <summary>Argument of <c>track</c>. A null layout means the next race's layout.</summary>
public sealed record TrackCall(string ManagerId, string? LayoutId);

/// <summary>The player's own staff, plus rival names and roles.</summary>
public sealed record StaffListView(IReadOnlyList<Paddock.Application.Staff.StaffPersonView> People);

/// <summary>Opens a negotiation. Subject is <c>driver</c> or <c>staff:Role</c>.</summary>
public sealed record OpenNegotiationCall(string ManagerId, string OrganizationId, string PersonId, string Subject, string? Deadline);

/// <summary>An offer. Money is whole currency units. Seat, option and exit are optional.</summary>
public sealed record SubmitOfferCall(
    string ManagerId,
    string NegotiationId,
    long Salary,
    long PointsBonus,
    long WinBonus,
    long TitleBonus,
    int Years,
    string? Seat,
    string? OptionHolder,
    int? OptionYears,
    int? ExitWorseThan);

/// <summary>Accepts the counter, or walks away, or signs a renewal's current terms.</summary>
public sealed record NegotiationIdCall(string ManagerId, string NegotiationId);

/// <summary>Renews a contract, either by exercising the option or by opening with an offer.</summary>
public sealed record RenewContractCall(
    string ManagerId,
    string ContractId,
    bool ExerciseOption,
    long? Salary,
    long? PointsBonus,
    long? WinBonus,
    long? TitleBonus,
    int? Years,
    string? Seat);

/// <summary>Sponsor talks. Slot is 1 to 3.</summary>
public sealed record BeginSponsorCall(string ManagerId, string OrganizationId, string SponsorId, int Slot);

/// <summary>Signs or leaves the current sponsor talks.</summary>
public sealed record SponsorTalkCall(string ManagerId, string OrganizationId, string TalkId);

/// <summary>Answers a sponsor's offer.</summary>
public sealed record SponsorOfferCall(string ManagerId, string OrganizationId, string OfferId, bool Accept);

/// <summary>The development split and the four area priorities.</summary>
public sealed record DevelopmentSplitCall(
    string ManagerId,
    string OrganizationId,
    int CurrentPercent,
    int AccountPercent,
    int NextYearPercent,
    int AeroPriority,
    int ChassisPriority,
    int ReliabilityPriority,
    int TyresPriority);

/// <summary>Commits one concept to production.</summary>
public sealed record CommitConceptCall(string ManagerId, string OrganizationId, string ProjectId);

/// <summary>Scout focus. A null handle is the whole pool.</summary>
public sealed record ScoutFocusCall(string ManagerId, string? PersonHandle);

/// <summary>Pays for one junior programme. Programme is <c>CheapSlow</c> or <c>ExpensiveFast</c>.</summary>
public sealed record FundJuniorCall(string ManagerId, string PersonHandle, string Programme);

/// <summary>Starts a pool signing. Role is <c>Test</c> or <c>Junior</c>.</summary>
public sealed record SignPoolCall(string ManagerId, string PersonHandle, string Role);

/// <summary>Offers a supplier a deal. Kind is <c>Works</c>, <c>Partner</c>, <c>Customer</c> or <c>LastYearEngine</c>.</summary>
public sealed record SupplyProposalCall(
    string ManagerId,
    string OrganizationId,
    string SupplierId,
    string Item,
    string Kind,
    int FirstSeason,
    long AnnualPriceCents,
    int Seasons,
    bool Exclusive,
    string? NegotiationId);

/// <summary>Accepts or refuses a supplier's counter.</summary>
public sealed record SupplyResponseCall(string ManagerId, string OrganizationId, string NegotiationId, bool Accept);
