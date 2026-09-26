using System.Globalization;
using System.Text.Json;

namespace Paddock.DataPipeline;

/// <summary>
/// Points-counting rules for one season, read from <c>data/authored/regulations/f1_timeline.json</c>.
/// <see cref="WinPoints"/> and <see cref="FastestLapBonus"/> are only the ceiling for
/// "could the leader still be caught". Counted championship points come from the
/// <c>points</c> already stored on each Jolpica result (half-points and fastest-lap
/// points included), not from reapplying the scale.
/// </summary>
public sealed record SeasonPointsRule(
    int WinPoints,
    int FastestLapBonus,
    bool DoublePointsFinale,
    ResultsQuota Quota);

public abstract record ResultsQuota;

public sealed record AllResults : ResultsQuota;

public sealed record BestResults(int Count) : ResultsQuota;

/// <summary>
/// Best <paramref name="KeepFirst"/> scores from rounds 1 through <paramref name="FirstHalfLastRound"/>,
/// plus best <paramref name="KeepSecond"/> scores from the later rounds.
/// </summary>
public sealed record SplitResults(int FirstHalfLastRound, int KeepFirst, int KeepSecond) : ResultsQuota;

public static class ChampionshipRules
{
    // Open-ended timeline rows ("to": null) are the rules still in force. Materialize them
    // through 2100 so a later season fails only when the file truly has a gap.
    private const int OpenEndedThrough = 2100;

    // Split seasons name the quota in the timeline value, and the notes name which rounds
    // form each half. The half cut is not a separate field, so it lives here and is checked
    // against the timeline value at load. 1979's note says only "four from each half";
    // the same Wikipedia page cited on that row says rounds 1–7 and 8–15.
    private static readonly Dictionary<int, SplitSpec> SplitSeasons = new()
    {
        [1967] = new("best_9_split_5_and_4", FirstHalfLastRound: 6, KeepFirst: 5, KeepSecond: 4),
        [1968] = new("best_10_split_5_and_5", 6, 5, 5),
        [1969] = new("best_9_split_5_and_4", 6, 5, 4),
        [1970] = new("best_11_split_6_and_5", 7, 6, 5),
        [1971] = new("best_9_split_5_and_4", 6, 5, 4),
        [1972] = new("best_10_split_5_and_5_of_6", 6, 5, 5),
        [1973] = new("best_13_split_7_and_6", 8, 7, 6),
        [1974] = new("best_13_split_7_and_6", 8, 7, 6),
        [1975] = new("best_12_split_6_and_6", 7, 6, 6),
        [1976] = new("best_14_split_7_and_7", 8, 7, 7),
        [1977] = new("best_15_split_8_and_7", 9, 8, 7),
        [1978] = new("best_14_split_7_and_7", 8, 7, 7),
        [1979] = new("best_8_split_4_and_4", 7, 4, 4),
        [1980] = new("best_10_split_5_and_5_of_7", 7, 5, 5),
    };

    private static readonly Dictionary<string, int> WinPointsByScale = new(StringComparer.Ordinal)
    {
        ["top5_8_6_4_3_2"] = 8,
        ["top6_8_6_4_3_2_1"] = 8,
        ["top6_9_6_4_3_2_1"] = 9,
        ["top6_10_6_4_3_2_1"] = 10,
        ["top8_10_8_6_5_4_3_2_1"] = 10,
        ["top10_25_18_15_12_10_8_6_4_2_1"] = 25,
    };

    private static readonly Dictionary<string, int> FastestLapBonusByValue = new(StringComparer.Ordinal)
    {
        ["one_point_shared_if_tied"] = 1,
        ["none"] = 0,
        ["one_point_if_top_10"] = 1,
        ["one_point_if_top_10_and_half_distance"] = 1,
    };

    private static readonly Lazy<IReadOnlyDictionary<int, SeasonPointsRule>> Rules = new(Load);

    public static SeasonPointsRule ForSeason(int season)
    {
        if (!Rules.Value.TryGetValue(season, out var rule))
        {
            throw new InvalidDataException($"No championship points rule for season {season.ToString(CultureInfo.InvariantCulture)}.");
        }

        return rule;
    }

    public static decimal CountRounds(IReadOnlyList<(int Round, decimal Points)> rounds, SeasonPointsRule rule)
    {
        ArgumentNullException.ThrowIfNull(rounds);
        ArgumentNullException.ThrowIfNull(rule);
        return rule.Quota switch
        {
            AllResults => rounds.Sum(round => round.Points),
            BestResults best => Top(rounds.Select(round => round.Points), best.Count),
            SplitResults split => Top(rounds.Where(round => round.Round <= split.FirstHalfLastRound).Select(round => round.Points), split.KeepFirst)
                + Top(rounds.Where(round => round.Round > split.FirstHalfLastRound).Select(round => round.Points), split.KeepSecond),
            _ => throw new InvalidOperationException("Unknown results quota."),
        };
    }

    /// <summary>
    /// Most a driver can add in <paramref name="round"/>: a win, a fastest-lap point when
    /// that season paid one, doubled only when this round is the season finale in 2014.
    /// </summary>
    public static decimal MaximumForRound(SeasonPointsRule rule, int round, int seasonFinalRound)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var points = (decimal)(rule.WinPoints + rule.FastestLapBonus);
        if (rule.DoublePointsFinale && round == seasonFinalRound)
        {
            points *= 2;
        }

        return points;
    }

    private static decimal Top(IEnumerable<decimal> scores, int keep)
    {
        return scores.OrderByDescending(score => score).Take(keep).Sum();
    }

    private static Dictionary<int, SeasonPointsRule> Load()
    {
        var path = Path.Combine(JolpicaCache.FindRepoRoot(), "data", "authored", "regulations", "f1_timeline.json");
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"Missing regulations timeline: {path}");
        }

        var scales = new Dictionary<int, string>();
        var laps = new Dictionary<int, string>();
        var doubles = new Dictionary<int, string>();
        var counted = new Dictionary<int, string>();
        using (var document = JsonDocument.Parse(File.ReadAllText(path)))
        {
            foreach (var row in document.RootElement.EnumerateArray())
            {
                var dimension = Required(row, "dimension");
                var target = dimension switch
                {
                    "points_scale" => scales,
                    "fastest_lap_point" => laps,
                    "double_points_finale" => doubles,
                    "results_counted" => counted,
                    _ => null,
                };
                if (target is null)
                {
                    continue;
                }

                var from = row.GetProperty("from").GetInt32();
                var toElement = row.GetProperty("to");
                var to = toElement.ValueKind == JsonValueKind.Null ? OpenEndedThrough : toElement.GetInt32();
                var value = Required(row, "value");
                for (var year = from; year <= to; year++)
                {
                    if (!target.TryAdd(year, value))
                    {
                        throw new InvalidDataException($"Overlapping {dimension} for {year.ToString(CultureInfo.InvariantCulture)}.");
                    }
                }
            }
        }

        var rules = new Dictionary<int, SeasonPointsRule>();
        for (var year = 1950; year <= OpenEndedThrough; year++)
        {
            if (!scales.TryGetValue(year, out var scale)
                || !laps.TryGetValue(year, out var lap)
                || !doubles.TryGetValue(year, out var doubled)
                || !counted.TryGetValue(year, out var quota))
            {
                throw new InvalidDataException($"Championship rules do not cover {year.ToString(CultureInfo.InvariantCulture)}.");
            }

            if (!WinPointsByScale.TryGetValue(scale, out var win))
            {
                throw new InvalidDataException($"Unknown points_scale '{scale}' in {year.ToString(CultureInfo.InvariantCulture)}.");
            }

            if (!FastestLapBonusByValue.TryGetValue(lap, out var bonus))
            {
                throw new InvalidDataException($"Unknown fastest_lap_point '{lap}' in {year.ToString(CultureInfo.InvariantCulture)}.");
            }

            var doubleFinale = doubled switch
            {
                "yes" => true,
                "no" => false,
                _ => throw new InvalidDataException($"Unknown double_points_finale '{doubled}' in {year.ToString(CultureInfo.InvariantCulture)}."),
            };

            rules[year] = new SeasonPointsRule(win, bonus, doubleFinale, QuotaFor(year, quota));
        }

        return rules;
    }

    private static ResultsQuota QuotaFor(int year, string value)
    {
        if (value.Contains("split", StringComparison.Ordinal))
        {
            if (!SplitSeasons.TryGetValue(year, out var spec) || spec.Value != value)
            {
                throw new InvalidDataException(
                    $"No half-season cut for {year.ToString(CultureInfo.InvariantCulture)} results_counted '{value}'.");
            }

            return new SplitResults(spec.FirstHalfLastRound, spec.KeepFirst, spec.KeepSecond);
        }

        if (SplitSeasons.ContainsKey(year))
        {
            throw new InvalidDataException(
                $"Season {year.ToString(CultureInfo.InvariantCulture)} was expected to be a split quota, timeline says '{value}'.");
        }

        if (value == "all")
        {
            return new AllResults();
        }

        const string Prefix = "best_";
        if (value.StartsWith(Prefix, StringComparison.Ordinal)
            && int.TryParse(value.AsSpan(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var keep)
            && keep > 0)
        {
            return new BestResults(keep);
        }

        throw new InvalidDataException($"Unknown results_counted '{value}' in {year.ToString(CultureInfo.InvariantCulture)}.");
    }

    private static string Required(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(value.GetString()))
        {
            throw new InvalidDataException($"Regulations timeline is missing {name}.");
        }

        return value.GetString()!;
    }

    private readonly record struct SplitSpec(string Value, int FirstHalfLastRound, int KeepFirst, int KeepSecond);
}
