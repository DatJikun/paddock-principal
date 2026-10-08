using Paddock.Application.Career;
using Paddock.Application.Commands;

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
    string SuggestedSeed,
    IReadOnlyList<PresetView> Presets);

/// <summary>
/// What a preset sets on every axis, straight from <c>CareerConfig.FromPreset</c>, so the start screen shows the values it will
/// send and does not keep its own copy of them.
/// </summary>
public sealed record PresetView(
    string Name,
    string People,
    string Rules,
    string Ai,
    int History,
    int Randomness,
    string Fatality,
    bool NoNumbers);

/// <summary>
/// One existing team the player may take over, with what a principal may know of it before he does: the line-up, the engine,
/// the budget tier, last season's place when the authored order has it, and the position the board will ask for. The card
/// fields are empty or null when the world could not be previewed for this setup.
/// </summary>
public sealed record TeamOptionView(
    string Id,
    string Name,
    IReadOnlyList<TeamCardDriver> Drivers,
    TeamCardEngine? Engine,
    string? Budget,
    int? LastSeason,
    int? Expected,
    int? FieldSize);

/// <summary>Public teams of one season. <paramref name="Problem"/> is why the chosen setup cannot start (the refusal <c>newCareer</c> would give), or null.</summary>
public sealed record TeamListView(int Year, IReadOnlyList<TeamOptionView> Teams, TranslationMessage? Problem);

/// <summary>
/// Argument of <c>teams</c>. Year defaults to 1955 when omitted. The axes are the ones <c>newCareer</c> takes, read the same way:
/// the cards come from the world they would start, and a setup <c>newCareer</c> would refuse comes back as the problem.
/// </summary>
public sealed record TeamsCall(
    string ManagerId,
    int Year,
    string? Preset,
    string? People,
    string? Rules,
    string? Ai,
    int? History,
    int? Randomness,
    string? Fatality,
    bool? NoNumbers,
    ulong? Seed);

/// <summary>One save the page can load. CareerName is the principal's name; Date is the career date in it; SavedAt is when the file was written (UTC, ISO), which orders "continue".</summary>
public sealed record SaveListItem(string Name, string Date, string TeamId, string CareerName, string SavedAt);

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

/// <summary>Argument of <c>driver</c>: the person id of any driver the manager's team can read about.</summary>
public sealed record DriverCall(string ManagerId, string PersonId);

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

/// <summary>Starts the upgrade of one own facility. Kind is a facility id as the <c>infrastructure</c> query names it.</summary>
public sealed record UpgradeFacilityCall(string ManagerId, string OrganizationId, string Kind);

/// <summary>Rents the test track for one private test of the own team.</summary>
public sealed record BookTestCall(string ManagerId, string OrganizationId);

/// <summary>Cancels a booked private test that has not happened yet. <c>TestOn</c> is its day, <c>yyyy-MM-dd</c>.</summary>
public sealed record CancelTestCall(string ManagerId, string OrganizationId, string TestOn);

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
