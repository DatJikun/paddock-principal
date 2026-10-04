namespace Paddock.DataPipeline;

/// <summary>Field level of one season: mean and standard deviation of the reference drivers' raw skill.</summary>
public readonly record struct SeasonField(int Season, double Mean, double Sd, int Count);

/// <summary>
/// Era-relative scale (owner's decision, 2026-10-04): a driver is rated against the field of his own time,
/// not against drivers of other decades. The game never races Fangio against Hamilton, so a season's ratings
/// only need to be right inside that season's field.
///
/// Each raw season skill <c>s</c> becomes <c>z = (s - mean) / sd</c> of that season's reference field.
/// The reference field is the ranked set (largest teammate component, enough duels), so one-race amateurs of
/// the 1950s do not inflate the spread. Seasons with a thin field borrow the neighbouring seasons
/// (<see cref="WindowYears"/> on each side). Dominance still shows: a driver who stood further above his
/// contemporaries gets a higher z.
/// </summary>
public static class RatingsEraScale
{
    /// <summary>Seasons on each side pooled into a season's field. ESTIMATE.</summary>
    public const int WindowYears = 1;

    /// <summary>Smallest pooled field accepted before the window widens further. ESTIMATE.</summary>
    public const int MinFieldSize = 8;

    /// <summary>Lower bound on a field's sd so a tiny, uniform field cannot blow up z. ESTIMATE.</summary>
    public const double MinSd = 0.25;

    public static IReadOnlyDictionary<int, SeasonField> Fields(IEnumerable<FittedDriverMetrics> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var bySeason = new SortedDictionary<int, List<double>>();
        foreach (var m in reference)
        {
            foreach (var (season, skill, _) in m.Seasons)
            {
                if (!bySeason.TryGetValue(season, out var list))
                {
                    list = [];
                    bySeason[season] = list;
                }

                list.Add(skill);
            }
        }

        var fields = new Dictionary<int, SeasonField>();
        if (bySeason.Count == 0)
        {
            return fields;
        }

        var first = bySeason.Keys.First();
        var last = bySeason.Keys.Last();
        foreach (var season in bySeason.Keys)
        {
            var window = WindowYears;
            List<double> pooled;
            do
            {
                pooled = [];
                for (var y = Math.Max(first, season - window); y <= Math.Min(last, season + window); y++)
                {
                    if (bySeason.TryGetValue(y, out var values))
                    {
                        pooled.AddRange(values);
                    }
                }

                window++;
            }
            while (pooled.Count < MinFieldSize && (season - window >= first || season + window <= last));

            var mean = pooled.Average();
            var variance = pooled.Sum(v => (v - mean) * (v - mean)) / pooled.Count;
            fields[season] = new SeasonField(season, mean, Math.Max(MinSd, Math.Sqrt(variance)), pooled.Count);
        }

        return fields;
    }

    /// <summary>
    /// Rewrites every driver's seasons onto the era-relative scale and recomputes the 3-season peak on it.
    /// Seasons outside <paramref name="fields"/> (no reference driver that year) keep the nearest season's field.
    /// </summary>
    public static Dictionary<string, FittedDriverMetrics> ToRelative(
        IReadOnlyDictionary<string, FittedDriverMetrics> metrics,
        IReadOnlyDictionary<int, SeasonField> fields)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(fields);
        var result = new Dictionary<string, FittedDriverMetrics>(metrics.Count, StringComparer.Ordinal);
        foreach (var (driverId, m) in metrics)
        {
            var seasons = m.Seasons
                .Select(s =>
                {
                    var f = FieldFor(fields, s.Season);
                    return (s.Season, (s.Skill - f.Mean) / f.Sd, s.Se / f.Sd);
                })
                .ToList();
            var (peak, peakSe, peakYears, shortCareer) = RatingsModel.CalculatePeak(seasons);
            result[driverId] = m with
            {
                CareerPeak = peak,
                PeakSe = peakSe,
                PeakYears = peakYears,
                ShortCareer = shortCareer,
                Seasons = seasons,
            };
        }

        return result;
    }

    /// <summary>
    /// Teammate duels at which a driver's era-relative values keep half their size (shrinkage toward the
    /// field average). Few duels mean a noisy estimate: a 40-duel career keeps a third of its lead over the field,
    /// a 400-duel career over 80 percent. ESTIMATE, set on the real run (2026-10-04, Castellotti check).
    /// </summary>
    public const double ShrinkHalfDuels = 80.0;

    /// <summary>Pulls every season of a thinly measured driver toward the field average (z = 0).</summary>
    public static Dictionary<string, FittedDriverMetrics> Shrink(IReadOnlyDictionary<string, FittedDriverMetrics> metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        var result = new Dictionary<string, FittedDriverMetrics>(metrics.Count, StringComparer.Ordinal);
        foreach (var (driverId, m) in metrics)
        {
            var factor = m.TotalDuels / (m.TotalDuels + ShrinkHalfDuels);
            var seasons = m.Seasons.Select(s => (s.Season, s.Skill * factor, s.Se * factor)).ToList();
            var (peak, peakSe, peakYears, shortCareer) = RatingsModel.CalculatePeak(seasons);
            result[driverId] = m with
            {
                CareerPeak = peak,
                PeakSe = peakSe,
                PeakYears = peakYears,
                ShortCareer = shortCareer,
                Seasons = seasons,
            };
        }

        return result;
    }

    private static SeasonField FieldFor(IReadOnlyDictionary<int, SeasonField> fields, int season)
    {
        if (fields.TryGetValue(season, out var field))
        {
            return field;
        }

        if (fields.Count == 0)
        {
            return new SeasonField(season, 0.0, 1.0, 0);
        }

        return fields.Values
            .OrderBy(f => Math.Abs(f.Season - season))
            .ThenBy(f => f.Season)
            .First();
    }
}
