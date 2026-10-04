namespace Paddock.Data.Historical;

/// <summary>
/// The part of <c>data/cache/reports/ratings.json</c> (written by <c>Paddock.DataPipeline ratings</c>) that the world
/// needs: one entry per rated driver. Unknown members of the report are ignored when reading. Property names are the
/// report's, so a rename there must be mirrored here (a test serialises the real report type and reads it back).
/// </summary>
public sealed record HistoricalRatingsDocument(IReadOnlyList<HistoricalDriverRating> DriverRatings);

/// <summary>
/// A rated driver. <see cref="RatingBySeason"/> is the era-relative overall (1-100, level x 5) per raced season.
/// <see cref="Arc"/> is null for short careers and drivers without a birth year.
/// </summary>
public sealed record HistoricalDriverRating(
    string DriverId,
    IReadOnlyList<HistoricalSeasonRating> RatingBySeason,
    HistoricalCareerArc? Arc);

public sealed record HistoricalSeasonRating(int Season, int Overall);

/// <summary>The shared career arc of a real driver (ages in whole years, <c>PeakLevel</c> on the game's 1-20 scale).</summary>
public sealed record HistoricalCareerArc(
    int GrowthStartAge,
    int DebutAge,
    int PeakAge,
    double PeakLevel,
    int DeclineStartAge,
    int PlateauYears,
    double DeclineLevelsPerYear,
    int LastSeasonAge,
    bool RetiredAtPeak);
