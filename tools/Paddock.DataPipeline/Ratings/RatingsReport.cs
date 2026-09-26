namespace Paddock.DataPipeline;

public sealed record RatingsReportDocument(
    RatingsFitSummary FitSummary,
    IReadOnlyList<DriverRankingEntry> AllTimeTop50,
    IReadOnlyList<DriverRankingEntry> AllRankedDrivers,
    IReadOnlyList<DecadeTopEntry> TopByDecade,
    IReadOnlyList<ReferenceComparisonReport> ReferenceComparisons,
    IReadOnlyList<DriverDataSummary> InsufficientDataDrivers,
    IReadOnlyList<DisconnectedDriverSummary> DisconnectedDrivers);

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
    double Lambda0);

public sealed record DriverRankingEntry(
    int Rank,
    string DriverId,
    string Name,
    double CareerPeak,
    double PeakSe,
    string PeakYears,
    int TotalDuels,
    bool ShortCareer);

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
