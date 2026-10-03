namespace Paddock.Simulation.Racing.Pits;

/// <summary>
/// Every tunable number of the pit-stop model and the strategist, in one place.
/// ESTIMATE: every value in this file is a guess made to give plausible behaviour. None is calibrated and none is
/// a verified historical fact (the regulation data has no pit-lane, crew or stop-time dimensions); a later
/// calibration task is meant to replace them (TECH, "Numbers are estimates until calibrated"). Stationary tyre-change
/// and refuelling times are not here: they belong to T30 (<c>Tyres.FuelModel</c>) and are reused as they are.
/// </summary>
public static class PitConstants
{
    // ---- Pit lane ----

    /// <summary>ESTIMATE: first season with a pit-lane speed limit. Before it the lane is driven at <see cref="UnlimitedLaneKph"/>.</summary>
    public const int PitLaneLimitFirstSeason = 1993;

    /// <summary>ESTIMATE: pit-lane speed limit from <see cref="PitLaneLimitFirstSeason"/>, km/h.</summary>
    public const double PitLaneLimitKph = 80;

    /// <summary>ESTIMATE: typical speed in the pit lane before there was a limit, km/h.</summary>
    public const double UnlimitedLaneKph = 110;

    /// <summary>ESTIMATE: pit lane length used when the caller has no track data, metres (the lane length is a track property that is not in the data yet).</summary>
    public const double DefaultLaneLengthMetres = 400;

    /// <summary>ESTIMATE: average racing speed over the same stretch of track, km/h; the lane loss is the lane time less the time at this speed.</summary>
    public const double DefaultRacingSpeedKph = 160;

    /// <summary>ESTIMATE: seconds lost on the entry and the exit (braking, turning in, accelerating, line deviation) on top of the lane speed loss.</summary>
    public const double EntryExitSeconds = 6;

    // ---- Stop rules ----

    /// <summary>ESTIMATE: first season of a minimum stationary time. A synthetic rule for the modern era; the data has no such dimension.</summary>
    public const int MinimumStopFirstSeason = 2010;

    /// <summary>ESTIMATE: minimum stationary time from <see cref="MinimumStopFirstSeason"/>, seconds.</summary>
    public const double MinimumStopSeconds = 2.0;

    /// <summary>ESTIMATE: standard pit crew size (people working on the car) by season, smoothstep between anchors.</summary>
    public static readonly IReadOnlyList<(int Season, double Value)> StandardCrewSizeAnchors =
    [
        (1950, 4),
        (1975, 6),
        (1995, 14),
        (2010, 20),
    ];

    // ---- Service time ----

    /// <summary>ESTIMATE: seconds to change a driver in a shared car (get out, get in, belts).</summary>
    public const double DriverChangeSeconds = 12;

    /// <summary>ESTIMATE: a crew at quality 0 is this share slower than an average one (quality 50); quality 100 is the same share faster.</summary>
    public const double CrewSpeedSpread = 0.15;

    /// <summary>ESTIMATE: service time scales with (standard crew / actual crew) to this power.</summary>
    public const double CrewSizeExponent = 0.5;

    /// <summary>ESTIMATE: lower bound of the crew size factor on service time.</summary>
    public const double CrewSizeFactorMin = 0.75;

    /// <summary>ESTIMATE: upper bound of the crew size factor on service time.</summary>
    public const double CrewSizeFactorMax = 1.5;

    // ---- Variance, slow stops and errors ----

    /// <summary>ESTIMATE: fixed part of the standard deviation of the crew variance, seconds.</summary>
    public const double VarianceBaseSdSeconds = 0.15;

    /// <summary>ESTIMATE: share of the service time added to the variance standard deviation.</summary>
    public const double VarianceSdFraction = 0.04;

    /// <summary>ESTIMATE: variance standard deviation multiplier at crew quality 0.</summary>
    public const double VarianceScaleAtQuality0 = 1.5;

    /// <summary>ESTIMATE: variance standard deviation multiplier at crew quality 100.</summary>
    public const double VarianceScaleAtQuality100 = 0.5;

    /// <summary>ESTIMATE: the variance draw is cut at this many standard deviations.</summary>
    public const double VarianceCutSigmas = 2.5;

    /// <summary>ESTIMATE: the service time (before faults) is never shorter than this share of its nominal value.</summary>
    public const double MinServiceShare = 0.7;

    /// <summary>ESTIMATE: probability of a slow stop or an error at crew quality 0.</summary>
    public const double FaultProbabilityAtQuality0 = 0.15;

    /// <summary>ESTIMATE: probability of a slow stop or an error at crew quality 100.</summary>
    public const double FaultProbabilityAtQuality100 = 0.01;

    /// <summary>ESTIMATE: share of faults that are errors (the rest are slow stops).</summary>
    public const double ErrorShareOfFaults = 0.25;

    /// <summary>ESTIMATE: extra seconds of a slow stop, lower end.</summary>
    public const double SlowStopMinSeconds = 1.5;

    /// <summary>ESTIMATE: extra seconds of a slow stop, upper end.</summary>
    public const double SlowStopMaxSeconds = 5;

    /// <summary>ESTIMATE: extra seconds of an error (stuck wheel nut, stall), lower end.</summary>
    public const double ErrorMinSeconds = 6;

    /// <summary>ESTIMATE: extra seconds of an error, upper end.</summary>
    public const double ErrorMaxSeconds = 20;

    // ---- Strategist: pace modes ----

    /// <summary>ESTIMATE: seconds per lap gained in push mode.</summary>
    public const double PushPaceGainSeconds = 0.25;

    /// <summary>ESTIMATE: tyre wear speed in push mode, as a multiple of standard.</summary>
    public const double PushWearFactor = 1.35;

    /// <summary>ESTIMATE: fuel burn in push mode, as a multiple of standard.</summary>
    public const double PushBurnFactor = 1.04;

    /// <summary>ESTIMATE: tyre wear speed in save mode, as a multiple of standard (the pace loss and burn saving are T30's fuel-saving numbers).</summary>
    public const double SaveWearFactor = 0.80;

    // ---- Strategist: scoring ----

    /// <summary>ESTIMATE: share of extra fuel loaded at a stop over the computed need of the stint.</summary>
    public const double RefuelSafetyMargin = 0.03;

    /// <summary>ESTIMATE: seconds added to a plan that breaks a rule or cannot finish (fuel, mandatory compounds); large enough to never win when a legal plan exists.</summary>
    public const double InfeasiblePlanPenaltySeconds = 1000;

    /// <summary>ESTIMATE: a stop is assumed to cost one place when the car behind is closer than the stop loss; one place is worth this many seconds.</summary>
    public const double PlaceValueSeconds = 2.0;

    /// <summary>ESTIMATE: laps a driver can drive before tiring.</summary>
    public const int DriverFatigueOnsetLaps = 25;

    /// <summary>ESTIMATE: seconds per lap lost per lap beyond <see cref="DriverFatigueOnsetLaps"/>.</summary>
    public const double DriverFatigueSecondsPerLap = 0.04;

    /// <summary>ESTIMATE: seconds per lap a dry tyre loses in rain, at probability 1.</summary>
    public const double RainOnDryPenaltySeconds = 8.0;

    /// <summary>ESTIMATE: wet tyres are candidates only when the forecast rain probability in the next laps reaches this.</summary>
    public const double WetCandidateProbability = 0.25;

    /// <summary>ESTIMATE: a race is treated as wet (the mandatory compound mix is waived) when the forecast probability for the current lap reaches this.</summary>
    public const double WetRaceProbability = 0.6;

    /// <summary>ESTIMATE: laps ahead the forecast is read over for the wet candidates.</summary>
    public const int ForecastLookaheadLaps = 8;

    /// <summary>ESTIMATE: most stops a plan may contain.</summary>
    public const int MaxPlannedStops = 3;

    /// <summary>ESTIMATE: how many times the planned stop laps are shifted earlier and later when searching.</summary>
    public const int StopShiftVariants = 2;

    /// <summary>ESTIMATE: standard deviation of the strategist's perception noise on a plan's total time at skill 0, seconds; falls linearly to 0 at skill 100.</summary>
    public const double PlanNoiseSdSecondsAtSkill0 = 6.0;

    /// <summary>ESTIMATE: standard deviation of the log of the strategist's tyre-wear misjudgement at skill 0; falls linearly to 0 at skill 100.</summary>
    public const double WearBeliefSdAtSkill0 = 0.35;
}
