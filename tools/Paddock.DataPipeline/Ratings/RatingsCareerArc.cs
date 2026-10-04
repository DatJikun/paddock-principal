namespace Paddock.DataPipeline;

/// <summary>
/// A real driver's career arc in the same shape the game uses for generated people
/// (<c>Paddock.Domain.People.CareerCurve</c>): growth from a start age, a peak, a plateau, then decline,
/// plus where the real career ended. Ages are whole years (season minus birth year).
/// <c>PeakOverall</c> is the era-relative overall at the peak, i.e. the talent ceiling the game starts from.
/// <c>DeclineIsEstimate</c> is true when the real career ended too soon after the peak to measure a decline
/// (the default rate is used instead).
/// </summary>
public sealed record CareerArc(
    int GrowthStartAge,
    int DebutAge,
    int PeakAge,
    int PeakOverall,
    int PlateauYears,
    int DeclineMilliPerYear,
    bool DeclineIsEstimate,
    int LastSeasonAge);

public static class RatingsCareerArc
{
    /// <summary>Growth starts this many years before the F1 debut (junior categories). ESTIMATE.</summary>
    public const int YearsOfGrowthBeforeDebut = 3;

    /// <summary>A season within this many overall points of the peak still counts as plateau. ESTIMATE.</summary>
    public const int PlateauTolerance = 3;

    /// <summary>Decline used when the career gives no post-plateau seasons to measure. ESTIMATE.</summary>
    public const int DefaultDeclineMilliPerYear = 2000;

    /// <summary>Smallest decline written out; the domain requires a positive rate. ESTIMATE.</summary>
    public const int MinDeclineMilliPerYear = 250;

    /// <summary>
    /// Builds the arc from the driver's era-relative overall per season (smoothed where a curve exists).
    /// Returns null without a birth year or with fewer than <see cref="RatingsCurveModel.MinSeasons"/> seasons.
    /// </summary>
    public static CareerArc? Build(IReadOnlyList<SeasonRating> bySeason, int? birthYear)
    {
        ArgumentNullException.ThrowIfNull(bySeason);
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

        var peak = seasons[peakIdx];

        // Plateau: consecutive seasons after the peak that stay within the tolerance.
        var plateauEnd = peakIdx;
        while (plateauEnd + 1 < seasons.Count && seasons[plateauEnd + 1].Overall >= peak.Overall - PlateauTolerance)
        {
            plateauEnd++;
        }

        var plateauYears = seasons[plateauEnd].Season - peak.Season;

        // Decline: overall points lost per year from the end of the plateau to the last season.
        var last = seasons[^1];
        var plateauSeason = seasons[plateauEnd];
        var declineIsEstimate = true;
        var declineMilli = DefaultDeclineMilliPerYear;
        if (last.Season > plateauSeason.Season && last.Overall < plateauSeason.Overall)
        {
            var perYear = (double)(plateauSeason.Overall - last.Overall) / (last.Season - plateauSeason.Season);
            declineMilli = Math.Max(MinDeclineMilliPerYear, (int)Math.Round(perYear * 1000.0, MidpointRounding.AwayFromZero));
            declineIsEstimate = false;
        }

        var debutAge = seasons[0].Season - born;
        var peakAge = peak.Season - born;
        var growthStart = Math.Max(1, Math.Min(debutAge - YearsOfGrowthBeforeDebut, peakAge - 1));

        return new CareerArc(
            GrowthStartAge: growthStart,
            DebutAge: debutAge,
            PeakAge: peakAge,
            PeakOverall: peak.Overall,
            PlateauYears: plateauYears,
            DeclineMilliPerYear: declineMilli,
            DeclineIsEstimate: declineIsEstimate,
            LastSeasonAge: last.Season - born);
    }
}
