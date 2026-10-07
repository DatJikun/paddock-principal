using System.Collections.Immutable;
using System.Globalization;
using Paddock.Domain.World;

namespace Paddock.Simulation.Racing.Points;

/// <summary>Catalog dimension <c>fastest_lap_point</c>.</summary>
public enum FastestLapRule
{
    /// <summary>No point.</summary>
    None,

    /// <summary>One point for the fastest lap, wherever the car finished, split between tied cars.</summary>
    OnePointSharedIfTied,

    /// <summary>One point if the car is classified in the top ten.</summary>
    OnePointIfTopTen,

    /// <summary>As <see cref="OnePointIfTopTen"/>, and only if the winner completed at least half the scheduled laps.</summary>
    OnePointIfTopTenAndHalfDistance,
}

/// <summary>Catalog dimension <c>shared_drive_points</c>.</summary>
public enum SharedDriveRule
{
    /// <summary>The drivers of one car split its points equally.</summary>
    SharedEqually,

    /// <summary>
    /// A shared drive scores no points: every driver of a car with more than one driver gets nothing.
    /// The car's points are still computed and still go to its constructor (ESTIMATE: whether the constructor
    /// should keep them is not stated by the data; confirm).
    /// </summary>
    NotShared,
}

/// <summary>Catalog dimension <c>constructors_points_counting</c>.</summary>
public enum ConstructorCounting
{
    /// <summary>The era data has no official constructors' title. <see cref="PointsRules.For"/> turns it into a classification anyway (#264).</summary>
    NoChampionship,

    /// <summary>Only the best-placed classified car of a constructor scores in a race.</summary>
    BestFinishingCarOnly,

    /// <summary>Every classified car scores for its constructor.</summary>
    AllCars,
}

/// <summary>Catalog dimension <c>classification_threshold</c>.</summary>
public enum ClassificationRule
{
    /// <summary>
    /// The data does not know the distance requirement. FALLBACK (ESTIMATE): a car is classified exactly when
    /// it was running at the flag (<see cref="FinishStatus.Classified"/>), however many laps down.
    /// </summary>
    RunningAtFlag,

    /// <summary>A car must have covered at least 90 percent of the winner's laps, rounded down to a whole lap.</summary>
    NinetyPercentOfWinnerLaps,
}

/// <summary>
/// Knobs that the catalog does not carry. Both defaults are ESTIMATES.
/// </summary>
/// <param name="ConstructorsApplyResultsCounting">
/// Whether the constructors' title drops results by the same <c>results_counted</c> rule as the drivers' title.
/// The catalog dimension speaks of "a driver's worst finishes" only. The implementer's recollection is that the
/// constructors' table dropped results in the same seasons (NOT in the data; verify).
/// </param>
/// <param name="CarsPerConstructor">
/// Cars a constructor may enter, used only for the largest points a constructor could still take in a round
/// when every car scores. The catalog has no entry limit that is a number.
/// </param>
public sealed record PointsRulesOptions(bool ConstructorsApplyResultsCounting = true, int CarsPerConstructor = 2);

/// <summary>
/// The points rules of one season, built from the regulation dimensions of a <see cref="RuleSet"/>.
/// Period boundaries come from the data; nothing here knows a year.
/// </summary>
public sealed record PointsRules
{
    private PointsRules()
    {
    }

    public int Season { get; private init; }

    /// <summary>Points by finishing position: index 0 is the winner. Positions beyond the table score nothing.</summary>
    public ImmutableArray<int> PositionPoints { get; private init; }

    public FastestLapRule FastestLap { get; private init; }

    public SharedDriveRule SharedDrive { get; private init; }

    public ConstructorCounting ConstructorCounting { get; private init; }

    public ResultsCountingRule ResultsCounting { get; private init; } = ResultsCountingRule.All;

    public bool DoublePointsFinale { get; private init; }

    public ClassificationRule Classification { get; private init; }

    public bool ConstructorsApplyResultsCounting { get; private init; }

    public int CarsPerConstructor { get; private init; }

    /// <summary>
    /// True when the fastest-lap point also counts for the constructor. Derived from the data: the
    /// "one point shared if tied" era says that in 1958-59 the point did not go to constructors, and that is the
    /// only stretch where it overlaps a constructors' title.
    /// </summary>
    public bool FastestLapCountsForConstructors =>
        FastestLap != FastestLapRule.None && FastestLap != FastestLapRule.OnePointSharedIfTied;

    /// <summary>Builds the rules from the dimensions <c>points_scale</c>, <c>fastest_lap_point</c>, <c>shared_drive_points</c>,
    /// <c>constructors_points_counting</c>, <c>results_counted</c>, <c>double_points_finale</c> and <c>classification_threshold</c>.</summary>
    /// <exception cref="FormatException">A dimension holds a value this engine does not know.</exception>
    public static PointsRules For(RuleSet rules, PointsRulesOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        options ??= new PointsRulesOptions();
        if (options.CarsPerConstructor < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "CarsPerConstructor must be at least 1.");
        }

        return new PointsRules
        {
            Season = rules.Season,
            PositionPoints = ParseScale(rules.Value("points_scale")),
            FastestLap = rules.Value("fastest_lap_point") switch
            {
                "none" => FastestLapRule.None,
                "one_point_shared_if_tied" => FastestLapRule.OnePointSharedIfTied,
                "one_point_if_top_10" => FastestLapRule.OnePointIfTopTen,
                "one_point_if_top_10_and_half_distance" => FastestLapRule.OnePointIfTopTenAndHalfDistance,
                var other => throw Unknown("fastest_lap_point", other),
            },
            SharedDrive = rules.Value("shared_drive_points") switch
            {
                "shared_equally" => SharedDriveRule.SharedEqually,
                "not_shared" => SharedDriveRule.NotShared,
                var other => throw Unknown("shared_drive_points", other),
            },
            ConstructorCounting = rules.Value("constructors_points_counting") switch
            {
                // The game always runs a constructors' classification (#264): the era data may say there was no official title,
                // but the board, the standings and the UI need a table. Best finishing car is the rule the first title used in 1958.
                "no_championship" => ConstructorCounting.BestFinishingCarOnly,
                "best_finishing_car_only" => ConstructorCounting.BestFinishingCarOnly,
                "all_cars" => ConstructorCounting.AllCars,
                var other => throw Unknown("constructors_points_counting", other),
            },
            ResultsCounting = ResultsCountingRule.Parse(rules.Value("results_counted")),
            DoublePointsFinale = rules.Value("double_points_finale") switch
            {
                "yes" => true,
                "no" => false,
                var other => throw Unknown("double_points_finale", other),
            },
            Classification = rules.Value("classification_threshold") switch
            {
                "unknown" => ClassificationRule.RunningAtFlag,
                "ninety_percent_of_winner_laps_rounded_down" => ClassificationRule.NinetyPercentOfWinnerLaps,
                var other => throw Unknown("classification_threshold", other),
            },
            ConstructorsApplyResultsCounting = options.ConstructorsApplyResultsCounting,
            CarsPerConstructor = options.CarsPerConstructor,
        };
    }

    /// <summary>Points for a finishing position (1 is the winner), before fastest-lap and double points.</summary>
    public int PointsForPosition(int position) =>
        position >= 1 && position <= PositionPoints.Length ? PositionPoints[position - 1] : 0;

    /// <summary>The most one driver can take from a single round: a win with the fastest-lap point where there is one.</summary>
    public decimal MaxDriverPointsPerRound(bool isFinalRound) =>
        (PositionPoints[0] + (FastestLap == FastestLapRule.None ? 0 : 1)) * RoundMultiplier(isFinalRound);

    /// <summary>The most one constructor can take from a single round, with <see cref="CarsPerConstructor"/> cars where every car scores.</summary>
    public decimal MaxConstructorPointsPerRound(bool isFinalRound)
    {
        var cars = ConstructorCounting switch
        {
            ConstructorCounting.NoChampionship => 0,
            ConstructorCounting.BestFinishingCarOnly => 1,
            _ => Math.Min(CarsPerConstructor, PositionPoints.Length),
        };
        var points = 0;
        for (var i = 0; i < cars; i++)
        {
            points += PositionPoints[i];
        }

        if (cars > 0 && FastestLapCountsForConstructors)
        {
            points++;
        }

        return points * RoundMultiplier(isFinalRound);
    }

    internal decimal RoundMultiplier(bool isFinalRound) => DoublePointsFinale && isFinalRound ? 2m : 1m;

    private static ImmutableArray<int> ParseScale(string value)
    {
        // Format: top<N>_<p1>_<p2>_..._<pN>, e.g. top6_9_6_4_3_2_1.
        var parts = value.Split('_');
        if (parts.Length < 2
            || !parts[0].StartsWith("top", StringComparison.Ordinal)
            || !int.TryParse(parts[0].AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            || count < 1
            || parts.Length != count + 1)
        {
            throw Unknown("points_scale", value);
        }

        var builder = ImmutableArray.CreateBuilder<int>(count);
        for (var i = 1; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var points))
            {
                throw Unknown("points_scale", value);
            }

            builder.Add(points);
        }

        return builder.MoveToImmutable();
    }

    private static FormatException Unknown(string dimension, string value) =>
        new($"Unknown value '{value}' for regulation dimension '{dimension}'.");
}
