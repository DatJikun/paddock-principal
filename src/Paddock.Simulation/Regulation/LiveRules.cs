using System.Globalization;

namespace Paddock.Simulation.Regulation;

/// <summary>
/// What a rule value is like on the five axes teams and the FIA argue about, each from -1 to 1 (#275). These are ESTIMATES, not
/// facts: the sim does not read them. They only decide who likes a change (the vote) and what pressure makes the FIA bring it.
/// The effect on the race itself comes from the race engine reading the new value.
/// </summary>
/// <param name="Modern">How modern the value is; progressives like it, traditionalists do not.</param>
/// <param name="Equal">How much it narrows the gap between a rich and a poor team; weak teams and egalitarians like it.</param>
/// <param name="Show">How much of a spectacle it makes; gimmicky teams like it.</param>
/// <param name="Safety">How much safer it makes racing.</param>
/// <param name="Cheap">How much cheaper it makes running a team.</param>
public readonly record struct ValueTraits(double Modern = 0, double Equal = 0, double Show = 0, double Safety = 0, double Cheap = 0)
{
    public static ValueTraits operator -(ValueTraits to, ValueTraits from) => new(
        to.Modern - from.Modern,
        to.Equal - from.Equal,
        to.Show - from.Show,
        to.Safety - from.Safety,
        to.Cheap - from.Cheap);

    /// <summary>The size of a change: the sum of the absolute differences on every axis.</summary>
    public double Magnitude => Math.Abs(Modern) + Math.Abs(Equal) + Math.Abs(Show) + Math.Abs(Safety) + Math.Abs(Cheap);
}

/// <summary>
/// One rule a vote can change: the dimension, the values the vote may move it to, and the translation key that says in plain
/// words what it does in the race.
/// </summary>
/// <param name="DimensionId">Id in the regulation catalog.</param>
/// <param name="Values">The values a vote may choose. Narrower than the catalog where a value needs data the race does not have.</param>
/// <param name="EffectKey">Translation key of the effect in the race.</param>
/// <param name="ReadBy">The part of the race engine that reads the dimension, for the documentation.</param>
public sealed record LiveRule(string DimensionId, IReadOnlyList<string> Values, string EffectKey, string ReadBy);

/// <summary>
/// The catalogue of rules that can be proposed and voted (#275 scope 8): only dimensions the race engine already simulates.
/// A dimension that is wanted but not simulated is not here; the pull request lists it as "not supported yet". Adding a
/// dimension means adding it here with the value traits and a test that shows an adopted change changes a race.
/// </summary>
public static class LiveRules
{
    private static readonly LiveRule[] Rules =
    [
        new(
            "points_scale",
            ["top5_8_6_4_3_2", "top6_8_6_4_3_2_1", "top6_9_6_4_3_2_1", "top6_10_6_4_3_2_1", "top8_10_8_6_5_4_3_2_1", "top10_25_18_15_12_10_8_6_4_2_1"],
            "regulation.effect.points_scale",
            "PointsRules"),
        new(
            "fastest_lap_point",
            ["one_point_shared_if_tied", "none", "one_point_if_top_10", "one_point_if_top_10_and_half_distance"],
            "regulation.effect.fastest_lap_point",
            "PointsRules"),
        new(
            "double_points_finale",
            ["yes", "no"],
            "regulation.effect.double_points_finale",
            "PointsRules"),
        new(
            "results_counted",
            ["best_4", "best_5", "best_6", "best_10", "best_11", "all"],
            "regulation.effect.results_counted",
            "PointsRules"),
        new(
            "constructors_points_counting",
            ["best_finishing_car_only", "all_cars"],
            "regulation.effect.constructors_points_counting",
            "PointsRules"),
        new(
            "qualifying_format",
            ["two_session_best_time", "saturday_twelve_lap", "one_lap_friday_and_saturday", "one_lap_saturday_pair", "one_lap_2005_mixed", "knockout_q1_q2_q3"],
            "regulation.effect.qualifying_format",
            "QualifyingRules"),
        new(
            "safety_car",
            ["none", "physical", "physical_and_vsc"],
            "regulation.effect.safety_car",
            "NeutralisationPlanner"),
        new(
            "race_distance_rule",
            ["min_300km_or_two_hours", "standard_305km", "least_laps_over_305km", "least_laps_over_305km_monaco_260"],
            "regulation.effect.race_distance_rule",
            "RaceDistance"),
        new(
            "refuelling",
            ["allowed", "banned"],
            "regulation.effect.refuelling",
            "FuelRegime"),
    ];

    /// <summary>Ids of the dimensions a vote may change, in catalogue order.</summary>
    public static IReadOnlyList<LiveRule> All => Rules;

    public static LiveRule? Find(string dimensionId)
    {
        foreach (var rule in Rules)
        {
            if (string.Equals(rule.DimensionId, dimensionId, StringComparison.Ordinal))
            {
                return rule;
            }
        }

        return null;
    }

    public static bool IsLive(string dimensionId) => Find(dimensionId) is not null;

    /// <summary>True when the dimension is live and the vote may move it to <paramref name="value"/>.</summary>
    public static bool Allows(string dimensionId, string value) =>
        Find(dimensionId) is { } rule && rule.Values.Contains(value);

    /// <summary>
    /// The traits of one value (ESTIMATES). A value that is not in the table has none (all zero), which is what the history of an
    /// era that has no entry reads as; a change between two such values makes no difference and is not offered.
    /// </summary>
    public static ValueTraits TraitsOf(string dimensionId, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (dimensionId == "points_scale")
        {
            return ScaleTraits(value);
        }

        return (dimensionId, value) switch
        {
            ("fastest_lap_point", "one_point_shared_if_tied") => new(Modern: -0.4, Show: 0.2),
            ("fastest_lap_point", "none") => new(Modern: 0.3, Show: -0.3),
            ("fastest_lap_point", "one_point_if_top_10") => new(Modern: 0.4, Show: 0.5),
            ("fastest_lap_point", "one_point_if_top_10_and_half_distance") => new(Modern: 0.5, Show: 0.3),

            ("double_points_finale", "yes") => new(Modern: 0.2, Equal: 0.3, Show: 0.8),
            ("double_points_finale", "no") => default,

            ("results_counted", "best_4") => new(Modern: -0.8, Equal: 0.2),
            ("results_counted", "best_5") => new(Modern: -0.6, Equal: 0.15),
            ("results_counted", "best_6") => new(Modern: -0.4, Equal: 0.1),
            ("results_counted", "best_10") => new(Modern: 0.3),
            ("results_counted", "best_11") => new(Modern: 0.5),
            ("results_counted", "all") => new(Modern: 1.0, Equal: -0.2),

            ("constructors_points_counting", "best_finishing_car_only") => new(Modern: -0.3, Equal: 0.3),
            ("constructors_points_counting", "all_cars") => new(Modern: 0.4, Equal: -0.2),

            ("qualifying_format", "two_session_best_time") => new(Modern: -0.6, Show: -0.2),
            ("qualifying_format", "saturday_twelve_lap") => new(Modern: -0.4, Show: -0.1),
            ("qualifying_format", "one_lap_friday_and_saturday") => new(Modern: 0.2, Show: 0.3),
            ("qualifying_format", "one_lap_saturday_pair") => new(Modern: 0.3, Show: 0.4),
            ("qualifying_format", "one_lap_2005_mixed") => new(Modern: 0.4, Show: 0.4),
            ("qualifying_format", "knockout_q1_q2_q3") => new(Modern: 1.0, Equal: 0.3, Show: 1.0),

            ("safety_car", "none") => new(Modern: -1.0, Equal: -0.3, Show: -0.2, Safety: -1.0),
            ("safety_car", "physical") => new(Modern: 0.2, Equal: 0.4, Show: 0.4, Safety: 0.5),
            ("safety_car", "physical_and_vsc") => new(Modern: 1.0, Equal: 0.5, Show: 0.3, Safety: 1.0),

            ("race_distance_rule", "min_300km_or_two_hours") => new(Modern: -0.8, Safety: 0.1, Cheap: 0.3),
            ("race_distance_rule", "standard_305km") => default,
            ("race_distance_rule", "least_laps_over_305km") => default,
            ("race_distance_rule", "least_laps_over_305km_monaco_260") => new(Modern: 0.3, Cheap: 0.3),

            ("refuelling", "allowed") => new(Modern: 0.3, Show: 0.5, Safety: -0.6, Cheap: -0.5),
            ("refuelling", "banned") => new(Modern: -0.2, Show: -0.3, Safety: 0.6, Cheap: 0.5),

            _ => default,
        };
    }

    /// <summary>A change is worth a vote only when it moves some trait: two values with the same traits make no difference to anyone.</summary>
    public static bool HasEffect(string dimensionId, string from, string to) =>
        (TraitsOf(dimensionId, to) - TraitsOf(dimensionId, from)).Magnitude >= RegulationEstimates.MinimumEffect;

    /// <summary>
    /// ESTIMATE: a deeper points table rewards the middle of the field and is the newer habit, and a table that gives the winner a
    /// smaller share of the points narrows the gap between the top and the rest. The traits follow the scoring places and that share.
    /// </summary>
    private static ValueTraits ScaleTraits(string value)
    {
        const string prefix = "top";
        var parts = value.Split('_');
        if (parts.Length < 3
            || !parts[0].StartsWith(prefix, StringComparison.Ordinal)
            || !int.TryParse(parts[0].AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var depth))
        {
            return default;
        }

        var points = new List<int>(parts.Length - 1);
        for (var i = 1; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                return default;
            }

            points.Add(number);
        }

        var winnerShare = points[0] / (double)points.Sum();
        var depthScale = Math.Clamp((depth - 6) / 4.0, -1.0, 1.0);
        var equal = Math.Clamp((depthScale * 0.6) + ((0.33 - winnerShare) * 5.0), -1.0, 1.0);
        return new ValueTraits(Modern: depthScale, Equal: equal);
    }
}
