namespace Paddock.DataPipeline;

public sealed record RatingsReportDocument(
    RatingsFitSummary FitSummary,
    IReadOnlyList<DriverRankingEntry> AllTimeTop50,
    IReadOnlyList<DriverRankingEntry> AllRankedDrivers,
    IReadOnlyList<DecadeTopEntry> TopByDecade,
    IReadOnlyList<ReferenceComparisonReport> ReferenceComparisons,
    IReadOnlyList<DriverDataSummary> InsufficientDataDrivers,
    IReadOnlyList<DisconnectedDriverSummary> DisconnectedDrivers,
    IReadOnlyList<DecadeCarEffects> CarEffectsByDecade,
    IReadOnlyList<DriverRatingEntry> DriverRatings);

public sealed record RatingsFitSummary(
    int RacesCount,
    int TotalDuels,
    int RaceDuels,
    int QualifyingDuels,
    int DriverSeasonsCount,
    int Iterations,
    bool Converged,
    double MaxGradient,
    int ComponentsCount,
    int LargestComponentSize,
    int FromYear,
    int ToYear,
    double WRace,
    double WQuali,
    double LambdaTime,
    double Lambda0,
    int CrossRaceDuels,
    int CrossQualifyingDuels,
    int CarSeasonsCount,
    double LambdaC0,
    double LambdaCTime,
    double? WCross,
    double LambdaCurve);

public sealed record DriverRankingEntry(
    int Rank,
    string DriverId,
    string Name,
    double CareerPeak,
    double PeakSe,
    string PeakYears,
    int TotalDuels,
    bool ShortCareer,
    double PeakValue,
    int Overall,
    double Stars);

/// <summary>Constructor-season car effect from the fit (<c>Key</c> is the lineage key).</summary>
public sealed record CarEffect(string Key, int Season, double Effect, double Se);

public sealed record CarEffectEntry(string ConstructorKey, double MeanEffect, int SeasonsCount);

public sealed record DecadeCarEffects(
    int DecadeStart,
    IReadOnlyList<CarEffectEntry> Top,
    IReadOnlyList<CarEffectEntry> Bottom);

public sealed record SeasonRating(int Season, double Value, int Overall);

/// <summary>
/// Overall 1-100 and stars 0-5 are percentile mappings with guessed anchors (see <see cref="RatingsMapping"/>).
/// All values are era-relative (z against each season's field, <see cref="RatingsEraScale"/>).
/// <c>RatingBySeason</c> maps each season's (smoothed) value to an overall over all ranked driver-seasons.
/// <c>Arc</c> is the career arc in the game's shape; null without a birth year or with a short career.
/// </summary>
public sealed record DriverRatingEntry(
    int Rank,
    string DriverId,
    string Name,
    int Overall,
    double Stars,
    double Percentile,
    double PeakValue,
    CareerCurve? Curve,
    IReadOnlyList<SeasonRating> RatingBySeason,
    CareerArc? Arc = null);

/// <summary>Everything produced by one fit; used by the report builder and by tests.</summary>
public sealed record RatingsModelRun(
    IReadOnlyList<Duel> Duels,
    IReadOnlyList<DriverSeasonParam> Parameters,
    OptimizationResult Fit,
    IReadOnlyDictionary<string, FittedDriverMetrics> DriverMetrics,
    IReadOnlyList<CarEffect> CarEffects);

public sealed record DecadeTopEntry(
    int DecadeStart,
    IReadOnlyList<DecadeDriverEntry> Drivers);

public sealed record DecadeDriverEntry(
    int Rank,
    string DriverId,
    string Name,
    double MeanSkill,
    int SeasonsCount,
    int CareerDuels);

public sealed record ReferenceComparisonReport(
    string RankingId,
    string RankingTitle,
    int OverlapCount,
    double? SpearmanCorrelation,
    IReadOnlyList<DisagreementEntry> TopDisagreements);

public sealed record DisagreementEntry(
    string DriverId,
    string Name,
    int TheirRank,
    int OurRank,
    int Difference);

public sealed record DriverDataSummary(
    string DriverId,
    string Name,
    int TotalDuels,
    int SeasonsCount);

public sealed record DisconnectedDriverSummary(
    string DriverId,
    string Name,
    int ComponentIndex,
    int ComponentSize,
    int TotalDuels);

public sealed record ReferenceRankingDocument(
    string Id,
    string Title,
    int? YearPublished,
    string? Method,
    string? MethodNote,
    string? Source,
    string? Confidence,
    IReadOnlyList<ReferenceRankingEntry> Entries);

public sealed record ReferenceRankingEntry(
    int Rank,
    string Name,
    string DriverId);
