namespace Paddock.Simulation.Racing.Weekend;

/// <summary>
/// Every tunable number of the race weekend orchestrator, in one place. EVERY value here is an ESTIMATE: nothing has been
/// calibrated against real data (calibration is a separate task). The components (weather, lap time, tyres, reliability,
/// incidents, pits) keep their own constants files; this file holds only what the orchestrator itself decides: how input
/// ratings turn into pace inputs, how cars hold each other up, how neutralised laps look, how often a team consults its pit
/// wall. Changing a value changes races, so it is a gameplay-balance change and not a refactor.
/// </summary>
public static class WeekendConstants
{
    // ---- Attribute to pace-input mapping (replaced by T22 ratings) ---------------------------------------

    /// <summary>
    /// ESTIMATE: a visible attribute (1..20) becomes a 0..100 pace input by multiplying by this factor (linear x5, so
    /// 20 gives 100 and 10 gives 50, the reference). T22 ratings replace <see cref="RaceInputMapping"/>.
    /// </summary>
    public const double AttributeToRatingScale = 5d;

    // ---- Passing (IPassModel default) --------------------------------------------------------------------

    /// <summary>
    /// ESTIMATE: a car within this many seconds behind another runs in its dirty air, and needs a pace advantage of
    /// more than this many seconds per lap (adjusted by Overtaking versus Defending) to pass it.
    /// </summary>
    public const double OvertakeMarginSeconds = 0.8d;

    /// <summary>
    /// ESTIMATE: how strongly Overtaking versus Defending moves the margin. The margin is multiplied by
    /// <c>1 + OvertakeSkillWeight * (defending - overtaking) / 100</c> (ratings 0..100), so 0.5 gives 0.5x..1.5x.
    /// </summary>
    public const double OvertakeSkillWeight = 0.5d;

    /// <summary>ESTIMATE: seconds a car that cannot pass stays behind the car in front at the end of the lap.</summary>
    public const double FollowGapSeconds = 0.3d;

    /// <summary>ESTIMATE: gap between neighbouring grid slots, seconds (the car on pole starts at 0).</summary>
    public const double GridSlotGapSeconds = 0.25d;

    // ---- Qualifying input --------------------------------------------------------------------------------

    /// <summary>ESTIMATE: fuel on board for a qualifying lap, kg (low, so the grid reflects pace).</summary>
    public const double QualifyingFuelKg = 15d;

    // ---- Wet running -------------------------------------------------------------------------------------

    /// <summary>ESTIMATE: on a dry-weather compound the fit mismatch is wetness divided by this, capped at 1.</summary>
    public const double DryTyreWetnessLimit = 0.45d;

    /// <summary>ESTIMATE: wetness at which a wet-weather compound runs without any dry-track penalty (and has no mismatch).</summary>
    public const double WetTyreFullWetness = 0.6d;

    // ---- Neutralised laps --------------------------------------------------------------------------------

    /// <summary>ESTIMATE: a safety-car lap takes this many times the reference (track base) lap.</summary>
    public const double SafetyCarLapFactor = 1.6d;

    /// <summary>ESTIMATE: under a safety car the gap between neighbouring cars closes to at most this many seconds.</summary>
    public const double BunchedGapSeconds = 0.9d;

    /// <summary>ESTIMATE: a pit stop under a safety car costs this share of its normal time (the field is slow too).</summary>
    public const double SafetyCarPitLossFactor = 0.5d;

    /// <summary>ESTIMATE: tyre wear and fuel burn per lap under a safety car, as a share of normal.</summary>
    public const double SafetyCarWearBurnFactor = 0.4d;

    /// <summary>ESTIMATE: a virtual-safety-car lap takes this many times the reference lap, for every car (gaps are kept).</summary>
    public const double VirtualSafetyCarLapFactor = 1.4d;

    /// <summary>ESTIMATE: a pit stop under a virtual safety car costs this share of its normal time.</summary>
    public const double VirtualSafetyCarPitLossFactor = 0.6d;

    /// <summary>ESTIMATE: tyre wear and fuel burn per lap under a virtual safety car, as a share of normal.</summary>
    public const double VirtualSafetyCarWearBurnFactor = 0.6d;

    /// <summary>ESTIMATE: after a red-flag restart the cars stand this many seconds apart, in the order they were in.</summary>
    public const double RestartGapSeconds = 0.5d;

    /// <summary>
    /// ESTIMATE: a red-flag suspension of N laps (T33's <c>DurationLaps</c>) stops the clock of the race for N reference
    /// laps times this factor. It is added to every car equally, so it changes no order. Tyres are not changed (not modelled).
    /// </summary>
    public const double RedFlagStoppageLapFactor = 1d;

    // ---- Incidents ---------------------------------------------------------------------------------------

    /// <summary>ESTIMATE: seconds a car loses for a minor outcome of an incident (damage, a spin and rejoin).</summary>
    public const double MinorIncidentLossSeconds = 12d;

    /// <summary>ESTIMATE: cars within this many seconds of a car (at the start of a lap) count as near it for contact.</summary>
    public const double NearbySeconds = 1d;

    /// <summary>ESTIMATE: the incident danger of a circuit when the input does not say (1 is typical).</summary>
    public const double DefaultTrackDanger = 1d;

    // ---- Reliability inputs ------------------------------------------------------------------------------

    /// <summary>ESTIMATE: how hard the car is pushed in the failure model (0..1), the same for all cars.</summary>
    public const double FailurePaceStress = 0.5d;

    /// <summary>ESTIMATE: mean air temperature at which the heat stress of the failure model is 0.</summary>
    public const double HeatStressMinAirC = 15d;

    /// <summary>ESTIMATE: mean air temperature at which the heat stress of the failure model is 1.</summary>
    public const double HeatStressMaxAirC = 40d;

    // ---- Tyres, fuel -------------------------------------------------------------------------------------

    /// <summary>ESTIMATE: lowest track abrasiveness of a race (the hidden truth is drawn between this and the maximum).</summary>
    public const double AbrasivenessMin = 0.35d;

    /// <summary>ESTIMATE: highest track abrasiveness of a race.</summary>
    public const double AbrasivenessMax = 0.65d;

    /// <summary>ESTIMATE: fuel stops planned at the start where refuelling is allowed and the rig is fast (from 1994). Elsewhere the tank holds the whole race.</summary>
    public const int PlannedFuelStopsWithFastRig = 2;

    // ---- Strategist --------------------------------------------------------------------------------------

    /// <summary>
    /// ESTIMATE: a team consults its strategist on every Nth lap (lap 1, 1 + N, and so on), and on every lap under a
    /// neutralisation (cheap stops). Between consultations the pace mode stays. A cost limit, not a model of the pit wall.
    /// </summary>
    public const int StrategistCadenceLaps = 4;

    /// <summary>
    /// ESTIMATE: the default strategist's search limits when the input gives none. T34's own defaults (3 stops, 2 shifts) take
    /// seconds per race with five compounds; these two keep a race to about a second. A cost limit, not a model of the pit wall.
    /// </summary>
    public const int DefaultStrategistMaxStops = 2;

    /// <summary>ESTIMATE: see <see cref="DefaultStrategistMaxStops"/>.</summary>
    public const int DefaultStrategistStopShiftVariants = 1;

    /// <summary>ESTIMATE: how far ahead the weather forecast the pit wall has reaches, in minutes.</summary>
    public const int ForecastHorizonMinutes = 60;

    /// <summary>ESTIMATE: the weather is generated for this many times the base race time (slow races stay inside it).</summary>
    public const double WeatherDurationFactor = 1.35d;

    /// <summary>ESTIMATE: minutes added to the generated weather duration.</summary>
    public const int WeatherDurationExtraMinutes = 10;

    // ---- Race distance -----------------------------------------------------------------------------------

    /// <summary>ESTIMATE: scheduled distance in km where the data says the rule is unknown (the 1950s).</summary>
    public const double UnknownDistanceKm = 300d;

    /// <summary>DATA (race_distance_rule): the minimum distance of the 300 km rule, km.</summary>
    public const double MinimumDistanceKm = 300d;

    /// <summary>DATA (race_distance_rule): the 305 km distance, km.</summary>
    public const double StandardDistanceKm = 305d;

    /// <summary>DATA (race_distance_rule, least_laps_over_305km_monaco_260): the Monaco distance, km.</summary>
    public const double MonacoDistanceKm = 260d;
}
