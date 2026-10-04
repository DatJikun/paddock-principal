namespace Paddock.DataPipeline;

/// <summary>
/// A real driver's career arc in the shape the game uses for generated people
/// (<c>Paddock.Domain.People.CareerCurve</c>): growth from a start age to a peak, a plateau, then decline.
/// Ages are whole years (season minus birth year); levels are the game's 1-20 attribute scale.
/// <c>PeakLevel</c> is the talent ceiling (stars = level / 4).
/// <c>RetiredAtPeak</c> marks drivers who were still at their peak in their last season (Fangio): their
/// decline starts after the real career, not at the usual age.
/// </summary>
public sealed record CareerArc(
    int GrowthStartAge,
    int DebutAge,
    int PeakAge,
    double PeakLevel,
    int DeclineStartAge,
    int PlateauYears,
    double DeclineLevelsPerYear,
    int LastSeasonAge,
    bool RetiredAtPeak);

/// <summary>
/// Owner's rules (2026-10-04): every driver grows, settles, peaks, and only starts to fade at about 36-40.
/// The data decides how high a driver peaks and when he got there; the shape of the arc is the same for all,
/// because a relative dip late in a real career is often the car or a new teammate, not age.
/// </summary>
public static class RatingsCareerArc
{
    /// <summary>Growth starts this many years before the F1 debut (junior categories). ESTIMATE.</summary>
    public const int YearsOfGrowthBeforeDebut = 3;

    /// <summary>Earliest and latest peak age taken from the data; late debutants peak at their debut. ESTIMATE.</summary>
    public const int MinPeakAge = 25;

    public const int MaxPeakAge = 33;

    /// <summary>Usual decline start: each driver gets a fixed age in this range (owner: about 36-40).</summary>
    public const int DeclineStartMin = 36;

    public const int DeclineStartMax = 40;

    /// <summary>A last season within this many levels of the peak means the driver retired at his peak. ESTIMATE.</summary>
    public const double AtPeakTolerance = 1.0;

    /// <summary>Levels lost per year once decline starts. ESTIMATE.</summary>
    public const double DeclineLevelsPerYear = 0.75;

    /// <summary>
    /// Builds the arc from the driver's era-relative overall per season (overall = level × 5).
    /// Returns null without a birth year or with fewer than <see cref="RatingsCurveModel.MinSeasons"/> seasons.
    /// </summary>
    public static CareerArc? Build(IReadOnlyList<SeasonRating> bySeason, int? birthYear, string driverId)
    {
        ArgumentNullException.ThrowIfNull(bySeason);
        ArgumentNullException.ThrowIfNull(driverId);
        if (birthYear is not { } born || bySeason.Count < RatingsCurveModel.MinSeasons)
        {
            return null;
        }

        var seasons = bySeason.OrderBy(s => s.Season).ToList();
        var peakIdx = 0;
        for (var i = 1; i < seasons.Count; i++)
        {
            if (seasons[i].Overall > seasons[peakIdx].Overall)
            {
                peakIdx = i;
            }
        }

        var peakLevel = seasons[peakIdx].Overall / 5.0;
        var debutAge = seasons[0].Season - born;
        var lastAge = seasons[^1].Season - born;
        var dataPeakAge = seasons[peakIdx].Season - born;

        var peakAge = debutAge > MaxPeakAge
            ? debutAge
            : Math.Clamp(dataPeakAge, Math.Max(MinPeakAge, debutAge), MaxPeakAge);

        var retiredAtPeak = seasons[^1].Overall / 5.0 >= peakLevel - AtPeakTolerance;
        var declineStart = DeclineStartMin + (int)(StableHash(driverId) % (uint)(DeclineStartMax - DeclineStartMin + 1));
        if (retiredAtPeak && lastAge > declineStart)
        {
            declineStart = lastAge;
        }

        declineStart = Math.Max(declineStart, peakAge);
        var growthStart = Math.Max(1, Math.Min(debutAge - YearsOfGrowthBeforeDebut, peakAge - 1));

        return new CareerArc(
            GrowthStartAge: growthStart,
            DebutAge: debutAge,
            PeakAge: peakAge,
            PeakLevel: peakLevel,
            DeclineStartAge: declineStart,
            PlateauYears: declineStart - peakAge,
            DeclineLevelsPerYear: DeclineLevelsPerYear,
            LastSeasonAge: lastAge,
            RetiredAtPeak: retiredAtPeak);
    }

    // FNV-1a over the id's UTF-16 code units: the same driver always gets the same decline age (INV-002).
    private static uint StableHash(string text)
    {
        var hash = 2166136261u;
        foreach (var c in text)
        {
            hash ^= c;
            hash *= 16777619u;
        }

        return hash;
    }
}
