using Paddock.Domain.Time;

namespace Paddock.Domain.Board;

/// <summary>
/// The numbers of reputation, board confidence, dismissal and the job search (T45, PP-050). DESIGN section 15 is qualitative only,
/// so EVERY number here is an uncalibrated ESTIMATE, picked so the rules behave sensibly in tests and to be calibrated with
/// the playable season (T48). Reputation and confidence are stored as integer tenths of a point (0 to 1000) so the state hash
/// never depends on floating point; the player only ever sees whole points (PP-013).
/// </summary>
public static class BoardEstimates
{
    /// <summary>ESTIMATE: the top of the reputation and confidence scales, in tenths (100 points).</summary>
    public const int MaxTenths = 1000;

    /// <summary>ESTIMATE: a new manager starts at 30 points (a small name).</summary>
    public const int InitialReputationTenths = 300;

    /// <summary>ESTIMATE: the middle of the reputation scale, which shifts nobody's prestige. 50 points.</summary>
    public const int NeutralReputationTenths = 500;

    /// <summary>ESTIMATE: a board starts at 60 points of confidence in the principal it has.</summary>
    public const int InitialConfidenceTenths = 600;

    // ---- Protection of a new principal (PP-050: at least the first full season, longer with reputation) ----

    /// <summary>ESTIMATE: extra days of protection per reputation point, so 0 to 300 days on top of the first full season.</summary>
    public const int ProtectionDaysPerReputationPoint = 3;

    // ---- Reviews: confidence moves after every race ----

    /// <summary>ESTIMATE: the board target is 55 points at the expected position (a board is mildly satisfied by meeting expectations).</summary>
    public const int TargetAtExpectationTenths = 550;

    /// <summary>ESTIMATE: the board target swings by 45 points between the worst and the best possible position against expectation.</summary>
    public const int TargetSwingTenths = 450;

    /// <summary>ESTIMATE: share of the gap between confidence and the target that closes at each review.</summary>
    public const double ReviewSmoothing = 0.25;

    /// <summary>ESTIMATE: confidence lost at a review when cash is below zero (3 points).</summary>
    public const int NegativeCashPenaltyTenths = 30;

    /// <summary>ESTIMATE: confidence lost at a review when cash fell by more than a quarter since the last one (1.5 points).</summary>
    public const int FallingCashPenaltyTenths = 15;

    /// <summary>ESTIMATE: the share of cash that a fall must exceed to count as falling.</summary>
    public const double FallingCashShare = 0.25;

    // ---- Dismissal ----

    /// <summary>ESTIMATE: an average board (patience 50) dismisses below 40 points; the most impatient below 50, the most patient below 30.</summary>
    public const int DismissBelowAtPatience50Tenths = 400;

    /// <summary>ESTIMATE: the dismissal threshold moves by 2 tenths for every point of patience.</summary>
    public const int DismissThresholdTenthsPerPatiencePoint = 2;

    /// <summary>ESTIMATE: consecutive reviews below the threshold that dismiss, at patience 0. Patient boards wait up to 4 more.</summary>
    public const int ReviewsToDismissBase = 4;

    /// <summary>ESTIMATE: a board is patient from 0 to 100; this is the patience of a board with no profile to read.</summary>
    public const int DefaultPatience = 50;

    /// <summary>ESTIMATE: patience is 40 plus the age of the organization in years, capped at 80.</summary>
    public const int PatienceBase = 40;

    public const int PatienceMaxAge = 40;

    /// <summary>ESTIMATE: severance is this share of a season of principal pay.</summary>
    public const double SeveranceShare = 0.5;

    /// <summary>ESTIMATE: the stars of principal pay the severance reference uses.</summary>
    public const double SeveranceStars = 3.0;

    // ---- Objectives the board grants ----

    /// <summary>ESTIMATE: share of the last position against the budget rank when both are known (last position counts 60%).</summary>
    public const double LastPositionWeight = 0.6;

    /// <summary>ESTIMATE: a multi-year objective asks for this many places better than the season one.</summary>
    public const int MultiYearPlacesBetter = 2;

    /// <summary>ESTIMATE: years of a multi-year objective.</summary>
    public const int MultiYearSeasons = 3;

    /// <summary>ESTIMATE: confidence change when the season objective is met (15 points) or failed.</summary>
    public const int SeasonObjectiveTenths = 150;

    /// <summary>ESTIMATE: confidence change when the multi-year objective is met (20 points) or failed.</summary>
    public const int MultiYearObjectiveTenths = 200;

    // ---- Reputation ----

    /// <summary>ESTIMATE: reputation tenths gained at a season end for beating the expected position by the whole field (scaled by the margin).</summary>
    public const int SeasonResultTenthsAtFullMargin = 80;

    /// <summary>ESTIMATE: bonus for winning the constructors' title (6 points).</summary>
    public const int TitleTenths = 60;

    /// <summary>ESTIMATE: loss at a season end with cash below zero (4 points): mismanaged money.</summary>
    public const int MismanagedMoneyTenths = 40;

    /// <summary>ESTIMATE: the most the optional people-development fact adds at a season end, 3 points.</summary>
    public const int PeopleDevelopmentCapTenths = 30;

    /// <summary>ESTIMATE: loss when dismissed (8 points).</summary>
    public const int DismissalTenths = 80;

    /// <summary>ESTIMATE: loss when resigning (3 points).</summary>
    public const int ResignationTenths = 30;

    // ---- Job search ----

    /// <summary>ESTIMATE: days after the dismissal before the first ordinary offer.</summary>
    public const int FirstOfferDelayDays = 14;

    /// <summary>ESTIMATE: days between two ordinary offers.</summary>
    public const int OfferIntervalDays = 14;

    /// <summary>ESTIMATE: how long an offer stays valid (it lapses as declined).</summary>
    public const int OfferValidDays = 30;

    /// <summary>ESTIMATE: the longest a dismissed player goes without any offer (45 days). A minimum-quality offer arrives by then whatever the reputation.</summary>
    public const int GuaranteeWindowDays = 45;

    /// <summary>ESTIMATE: the most offers a player holds open at once.</summary>
    public const int MaxOpenOffers = 3;

    /// <summary>ESTIMATE: reputation the least prestigious team asks of a new principal, 20 points.</summary>
    public const int RequiredReputationFloorTenths = 200;

    /// <summary>ESTIMATE: extra reputation the most prestigious team asks (so 80 points in all).</summary>
    public const int RequiredReputationSpanTenths = 600;

    /// <summary>ESTIMATE: a team whose board has under 65 points of confidence in its AI principal, and whose principal is no longer protected, would take another.</summary>
    public const int NeedsPrincipalBelowTenths = 650;

    // ---- Hook into negotiations (T39) ----

    /// <summary>ESTIMATE: the prestige of a team shifts by this much per 100 points of its principal's reputation above or below 50 (so 0.15 at the extremes).</summary>
    public const double ReputationPrestigeSwing = 0.3;

    /// <summary>The end of the first full season after an appointment. Appointed on the first day of a season it is that season.</summary>
    public static GameDate FirstFullSeasonEnd(GameDate appointed) =>
        GameDate.SeasonEnd(appointed.IsSeasonStart ? appointed.Year : appointed.Year + 1);

    public static int Clamp(int tenths) => Math.Clamp(tenths, 0, MaxTenths);
}
