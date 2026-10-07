using Paddock.Data.Authored;
using Paddock.Data.Historical;

namespace Paddock.DataPipeline;

/// <summary>
/// ESTIMATE numbers of the starting-data step. They turn a car effect into the 0..100 strength the world starts a car at, and give a
/// team with no effect a start. Calibrated by eye against the authored 1954–1955 strengths (35..78), not measured.
/// </summary>
public static class StartingDataEstimates
{
    /// <summary>The strength of a car with the season's mean effect.</summary>
    public const double StrengthMean = 52;

    /// <summary>Strength points per standard deviation of the season's car effects.</summary>
    public const double StrengthPerSd = 12;

    public const double StrengthMin = 30;

    public const double StrengthMax = 85;

    /// <summary>A team with no effect starts at this quantile of the season's car strengths (all fitted cars): below the middle, above the back.</summary>
    public const double NewEntrantQuantile = 0.25;

    /// <summary>The new-entrant strength of a season with no car effects at all.</summary>
    public const double NewEntrantFallback = 42;

    /// <summary>How many seasons back a constructor with no effect may borrow its lineage's latest effect.</summary>
    public const int EarlierSeasonLookback = 3;

    /// <summary>From this season every car of a constructor scores in its order; before it only the best car of each race (ESTIMATE of the old rules).</summary>
    public const int AllCarsScoreFrom = 1979;
}

/// <summary>
/// Builds <see cref="StartingDataReport"/> from the normalized results, the ratings car effects, the lineage map and the authored
/// engine book. Pure and deterministic: the same input gives the same report, ordered by season and constructor id.
/// </summary>
public static class StartingDataBuilder
{
    public const string Notes =
        "ESTIMATE. Starting car strength from the ratings car effect of each constructor-season (z-score within the season, "
        + "mapped to 0..100), the constructors' order from the race points of each season, and the engine from the authored engine "
        + "book. Built from the local Jolpica cache, which stays out of the repo (PP-041). The authored files override it.";

    public static StartingDataReport Build(
        IReadOnlyList<HistoricalRace> races,
        IReadOnlyList<HistoricalResult> results,
        IReadOnlyList<CarEffect> carEffects,
        IReadOnlyList<EngineEntry> engines,
        ConstructorLineageMap lineage,
        int fromYear,
        int toYear)
    {
        ArgumentNullException.ThrowIfNull(races);
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(carEffects);
        ArgumentNullException.ThrowIfNull(engines);
        ArgumentNullException.ThrowIfNull(lineage);

        var effects = new Dictionary<(string Key, int Season), double>();
        foreach (var effect in carEffects)
        {
            effects[(effect.Key, effect.Season)] = effect.Effect;
        }

        var scales = carEffects
            .GroupBy(effect => effect.Season)
            .ToDictionary(group => group.Key, group => SeasonScale.Of(group.Select(effect => effect.Effect)));

        var championshipRaces = races
            .Where(race => !race.IsIndianapolis500)
            .Select(race => (race.Season, race.Round))
            .ToHashSet();

        var seasons = new List<StartingSeason>();
        for (var season = fromYear; season <= toYear; season++)
        {
            var rows = results
                .Where(row => row.Season == season && championshipRaces.Contains((row.Season, row.Round)))
                .ToList();
            var raced = rows.Select(row => row.ConstructorId).Distinct(StringComparer.Ordinal);
            var roster = engines.Where(entry => entry.Year == season).Select(entry => entry.ConstructorId);
            var ids = raced.Concat(roster).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();

            var withEffect = new List<(string Id, double Effect, double Strength)>();
            foreach (var id in ids)
            {
                if (effects.TryGetValue((lineage.Resolve(id, season), season), out var effect))
                {
                    withEffect.Add((id, effect, scales[season].Strength(effect)));
                }
            }

            var newEntrant = NewEntrantStrength(carEffects
                .Where(effect => effect.Season == season)
                .Select(effect => scales[season].Strength(effect.Effect))
                .ToList());
            var constructors = new List<StartingConstructor>(ids.Count);
            foreach (var id in ids)
            {
                var (supplier, engine) = EngineOf(engines, id, season);
                var known = withEffect.FirstOrDefault(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
                if (known.Id is not null)
                {
                    constructors.Add(new StartingConstructor(id, Round(known.Strength), StartingStrengthSources.CarEffect, Round(known.Effect, 4), supplier, engine));
                    continue;
                }

                if (Earlier(effects, scales, lineage, id, season) is double borrowed)
                {
                    constructors.Add(new StartingConstructor(id, Round(borrowed), StartingStrengthSources.EarlierSeason, null, supplier, engine));
                    continue;
                }

                constructors.Add(new StartingConstructor(id, Round(newEntrant), StartingStrengthSources.NewEntrant, null, supplier, engine));
            }

            seasons.Add(new StartingSeason(season, Round(newEntrant), constructors, Standings(rows, season)));
        }

        return new StartingDataReport(StartingDataLoader.SchemaVersion, Notes, fromYear, toYear, seasons);
    }

    /// <summary>
    /// The constructors' order of one season from its championship rows: per race the best car's points before
    /// <see cref="StartingDataEstimates.AllCarsScoreFrom"/>, every car's points from then; ties broken by the best finish, then the id.
    /// Dropped scores are not applied (ESTIMATE): the order only sets the budget tier, top, second to third, or the rest.
    /// </summary>
    public static IReadOnlyList<StartingStanding> Standings(IReadOnlyList<HistoricalResult> rows, int season)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var allCars = season >= StartingDataEstimates.AllCarsScoreFrom;
        return rows
            .GroupBy(row => row.ConstructorId, StringComparer.Ordinal)
            .Select(team =>
            {
                var points = team
                    .GroupBy(row => row.Round)
                    .Sum(race => allCars ? race.Sum(row => row.Points) : race.Max(row => row.Points));
                var best = team.Where(row => row.IsClassified).Select(row => row.Position).DefaultIfEmpty(int.MaxValue).Min();
                return (Id: team.Key, Points: points, Best: best);
            })
            .OrderByDescending(team => team.Points)
            .ThenBy(team => team.Best)
            .ThenBy(team => team.Id, StringComparer.Ordinal)
            .Select((team, index) => new StartingStanding(team.Id, index + 1, team.Points))
            .ToList();
    }

    /// <summary>The engine the world would fit: the first row of the season by supplier and engine name, as the world initializer orders them.</summary>
    private static (string? Supplier, string? Engine) EngineOf(IReadOnlyList<EngineEntry> engines, string constructorId, int season)
    {
        var entry = engines
            .Where(row => row.Year == season
                && string.Equals(row.ConstructorId, constructorId, StringComparison.Ordinal)
                && !string.Equals(row.Supplier.Trim(), "unknown", StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => row.Supplier, StringComparer.Ordinal)
            .ThenBy(row => row.EngineName, StringComparer.Ordinal)
            .FirstOrDefault();
        return entry is null ? (null, null) : (entry.Supplier.Trim(), entry.EngineName);
    }

    private static double? Earlier(
        Dictionary<(string Key, int Season), double> effects,
        Dictionary<int, SeasonScale> scales,
        ConstructorLineageMap lineage,
        string id,
        int season)
    {
        // The lineage key of the start season: a renamed team borrows the effect its lineage had under the old name.
        var key = lineage.Resolve(id, season);
        for (var back = 1; back <= StartingDataEstimates.EarlierSeasonLookback; back++)
        {
            var earlier = season - back;
            if (effects.TryGetValue((key, earlier), out var effect))
            {
                return scales[earlier].Strength(effect);
            }
        }

        return null;
    }

    private static double NewEntrantStrength(List<double> strengths)
    {
        if (strengths.Count == 0)
        {
            return StartingDataEstimates.NewEntrantFallback;
        }

        strengths.Sort();
        var index = (int)Math.Floor(StartingDataEstimates.NewEntrantQuantile * (strengths.Count - 1));
        return strengths[index];
    }

    private static double Round(double value, int digits = 1) => Math.Round(value, digits, MidpointRounding.AwayFromZero);

    private readonly record struct SeasonScale(double Mean, double Sd)
    {
        public static SeasonScale Of(IEnumerable<double> values)
        {
            var list = values.ToList();
            var mean = list.Average();
            var variance = list.Sum(value => (value - mean) * (value - mean)) / list.Count;
            return new SeasonScale(mean, Math.Sqrt(variance));
        }

        public double Strength(double effect)
        {
            var z = Sd > 1e-9 ? (effect - Mean) / Sd : 0d;
            return Math.Clamp(
                StartingDataEstimates.StrengthMean + (StartingDataEstimates.StrengthPerSd * z),
                StartingDataEstimates.StrengthMin,
                StartingDataEstimates.StrengthMax);
        }
    }
}
