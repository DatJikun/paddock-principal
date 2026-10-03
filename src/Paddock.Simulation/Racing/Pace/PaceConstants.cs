namespace Paddock.Simulation.Racing.Pace;

/// <summary>
/// Every tunable number of the lap-time model, in one place. EVERY value here is an ESTIMATE: nothing has been
/// calibrated against real data (calibration is a separate task). Changing a value changes lap times, so it is a
/// gameplay-balance change and not a refactor.
/// </summary>
public static class PaceConstants
{
    // ---- Scales -------------------------------------------------------------------------------------------

    /// <summary>ESTIMATE: car and driver ratings live on 0..100.</summary>
    public const double RatingMin = 0d;

    /// <summary>ESTIMATE: upper end of the 0..100 rating scale.</summary>
    public const double RatingMax = 100d;

    /// <summary>ESTIMATE: a rating of 50 is "the reference": it adds exactly 0 s in the car-fit and driver layers.</summary>
    public const double RatingReference = 50d;

    // ---- Layer 1: track base ------------------------------------------------------------------------------

    /// <summary>
    /// ESTIMATE: era-typical average lap speed in km/h for a neutral circuit, as (season, km/h) anchors.
    /// Between anchors the value is interpolated with a smoothstep (continuous, no jumps at era borders, and
    /// monotone between two anchors); outside the first/last anchor it is held flat.
    /// </summary>
    public static readonly (int Season, double Value)[] EraAverageSpeedKph =
    [
        (1950, 150d),
        (1965, 175d),
        (1975, 190d),
        (1988, 205d),
        (2000, 215d),
        (2020, 225d),
    ];

    /// <summary>
    /// ESTIMATE: multiplicative effect of a layout character tag on average speed (1.0 = none). Tags are the
    /// ones used by data/authored/tracks/circuits.json; unknown tags are ignored. Factors multiply.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, double> CharacterSpeedFactor =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["high_speed"] = 1.06d, // ESTIMATE
            ["flowing"] = 1.03d, // ESTIMATE
            ["oval"] = 1.10d, // ESTIMATE
            ["technical"] = 0.95d, // ESTIMATE
            ["street"] = 0.92d, // ESTIMATE
            ["bumpy"] = 0.98d, // ESTIMATE
        };

    /// <summary>ESTIMATE: lower clamp of the combined character speed factor.</summary>
    public const double CharacterSpeedFactorMin = 0.80d;

    /// <summary>ESTIMATE: upper clamp of the combined character speed factor.</summary>
    public const double CharacterSpeedFactorMax = 1.15d;

    // ---- Layer 2: car fit ---------------------------------------------------------------------------------

    /// <summary>
    /// ESTIMATE: how much each track profile bucket (key in <c>TrackLayout.ProfileWeights</c>) rewards each car
    /// attribute, in the order (Power, Downforce, MechanicalGrip, Braking). Each row sums to 1. Reliability does
    /// not affect pace (it feeds the failure model). Unknown profile keys are ignored.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, double[]> CarFitMatrix =
        new Dictionary<string, double[]>(StringComparer.Ordinal)
        {
            ["straights"] = [0.80d, 0.00d, 0.10d, 0.10d], // ESTIMATE
            ["high_speed"] = [0.30d, 0.60d, 0.10d, 0.00d], // ESTIMATE
            ["low_speed"] = [0.10d, 0.20d, 0.60d, 0.10d], // ESTIMATE
            ["braking"] = [0.10d, 0.00d, 0.30d, 0.60d], // ESTIMATE
        };

    /// <summary>
    /// ESTIMATE: seconds gained per car-fit point above <see cref="RatingReference"/>, as a fraction of the base
    /// lap time, as (season, fraction) anchors. Larger in early eras (cars differed more), smaller later.
    /// Same interpolation rules as <see cref="EraAverageSpeedKph"/>.
    /// </summary>
    public static readonly (int Season, double Value)[] CarFitSensitivity =
    [
        (1950, 0.0012d),
        (1970, 0.0009d),
        (1990, 0.0006d),
        (2010, 0.0004d),
        (2026, 0.0003d),
    ];

    /// <summary>
    /// ESTIMATE: downforce cap (0..100 scale) of an era, as (season, cap) anchors, used only by
    /// <see cref="EraPerformanceLimits.EstimateFor"/>. The authoritative cap travels in the lap input.
    /// </summary>
    public static readonly (int Season, double Value)[] DownforceCap =
    [
        (1950, 15d),
        (1970, 40d),
        (1985, 70d),
        (2000, 85d),
        (2020, 95d),
    ];

    // ---- Layer 3: driver ----------------------------------------------------------------------------------

    /// <summary>ESTIMATE: seconds gained per driver-pace point above the reference, as a fraction of base lap time.</summary>
    public const double DriverSensitivity = 0.0003d;

    /// <summary>ESTIMATE: hidden track-affinity term is clamped to +/- this many seconds ("small").</summary>
    public const double MaxAffinitySeconds = 0.25d;

    // ---- Layer 4: fuel, tyres, traffic, dirty air --------------------------------------------------------

    /// <summary>ESTIMATE: seconds lost per kg of fuel on board, per lap (era-independent).</summary>
    public const double FuelSecondsPerKg = 0.035d;

    /// <summary>ESTIMATE: a car closer than this many seconds behind another starts losing time to traffic.</summary>
    public const double TrafficGapThresholdSeconds = 1.5d;

    /// <summary>ESTIMATE: seconds lost when right behind another car (gap 0). Falls linearly to 0 at the threshold.</summary>
    public const double TrafficMaxLossSeconds = 0.6d;

    /// <summary>ESTIMATE: seconds lost at dirty-air level 1.0 (linear in the level).</summary>
    public const double DirtyAirMaxLossSeconds = 0.5d;

    // ---- Layer 5: wetness ---------------------------------------------------------------------------------

    /// <summary>ESTIMATE: fraction of base lap time lost at wetness 1.0 with the right tyres (before composure). Quadratic in wetness.</summary>
    public const double WetBaseFraction = 0.08d;

    /// <summary>ESTIMATE: extra fraction of base lap time lost at wetness 1.0 and tyre mismatch 1.0. Quadratic in (wetness * mismatch).</summary>
    public const double WetMismatchFraction = 0.20d;

    /// <summary>ESTIMATE: wet-penalty multiplier at composure 0 (large).</summary>
    public const double WetComposureMultiplierAtZero = 2.0d;

    /// <summary>ESTIMATE: wet-penalty multiplier at composure 100 (small, but not zero).</summary>
    public const double WetComposureMultiplierAtMax = 0.5d;

    // ---- Layer 6: noise -----------------------------------------------------------------------------------

    /// <summary>ESTIMATE: standard deviation of lap-to-lap noise, in seconds, for consistency 0.</summary>
    public const double NoiseSigmaAtZeroConsistency = 0.60d;

    /// <summary>ESTIMATE: standard deviation of lap-to-lap noise, in seconds, for consistency 100.</summary>
    public const double NoiseSigmaAtMaxConsistency = 0.08d;
}
