namespace Paddock.Simulation.Racing.Reliability;

/// <summary>
/// Every tunable number of the reliability model, in one place.
/// ESTIMATE: every value in this file is a guess made to give plausible behaviour. None is calibrated;
/// a later task is meant to calibrate the era targets against the T5 Jolpica report.
/// </summary>
public static class ReliabilityConstants
{
    // ---- Hazard formula: h = baseHazard(era, component) * (1 + stress) * (1 - rating * RatingEffect) * ageFactor ----

    /// <summary>ESTIMATE: a reliability rating of 1 removes this share of the hazard (rating 0 removes none).</summary>
    public const double RatingEffect = 0.8;

    /// <summary>ESTIMATE: hazard grows linearly with mileage; at one full <see cref="ComponentLifeLaps"/> it has doubled.</summary>
    public const double AgeSlope = 1.0;

    /// <summary>ESTIMATE: stress weight of pushing the pace (0 = cruising, 1 = flat out).</summary>
    public const double PaceStressWeight = 0.5;

    /// <summary>ESTIMATE: stress weight of weather heat (0 = cool, 1 = extreme).</summary>
    public const double HeatStressWeight = 0.25;

    /// <summary>ESTIMATE: stress weight of a driver without mechanical sympathy (the weight applies to 1 - sympathy).</summary>
    public const double SympathyStressWeight = 0.25;

    // ---- Reference ("typical") car, the car the era finish-rate targets are solved for ----

    /// <summary>ESTIMATE: reliability rating of a typical car.</summary>
    public const double ReferenceReliabilityRating = 0.5;

    /// <summary>ESTIMATE: a typical driver's mechanical sympathy.</summary>
    public const double ReferenceSympathy = 0.5;

    /// <summary>ESTIMATE: a typical car runs at medium pace.</summary>
    public const double ReferencePaceStress = 0.5;

    /// <summary>ESTIMATE: a typical race is run in medium heat.</summary>
    public const double ReferenceHeat = 0.5;

    /// <summary>ESTIMATE: a typical car starts the race with parts that have no mileage on them.</summary>
    public const double ReferenceMileageLaps = 0;

    // ---- Per-component split ----

    /// <summary>ESTIMATE: the share of the total base hazard that belongs to each component. The shares sum to 1.</summary>
    public static double ComponentShare(MechanicalComponent component) => component switch
    {
        MechanicalComponent.Engine => 0.35,
        MechanicalComponent.Gearbox => 0.20,
        MechanicalComponent.Suspension => 0.13,
        MechanicalComponent.Brakes => 0.10,
        MechanicalComponent.ElectricsHydraulics => 0.12,
        MechanicalComponent.Cooling => 0.10,
        _ => throw new ArgumentOutOfRangeException(nameof(component)),
    };

    /// <summary>ESTIMATE: laps after which a part's hazard has doubled (see <see cref="AgeSlope"/>).</summary>
    public static double ComponentLifeLaps(MechanicalComponent component) => component switch
    {
        MechanicalComponent.Engine => 600,
        MechanicalComponent.Gearbox => 500,
        MechanicalComponent.Suspension => 900,
        MechanicalComponent.Brakes => 400,
        MechanicalComponent.ElectricsHydraulics => 700,
        MechanicalComponent.Cooling => 700,
        _ => throw new ArgumentOutOfRangeException(nameof(component)),
    };

    /// <summary>
    /// ESTIMATE: what a failure of the component does to the car. <c>Retire</c> is the share of failures that end the race.
    /// Of the rest, <c>PowerLossShareOfRest</c> lose power for the remainder of the race and the others stop for a repair.
    /// </summary>
    public static (double Retire, double PowerLossShareOfRest) EffectSplit(MechanicalComponent component) => component switch
    {
        MechanicalComponent.Engine => (0.80, 1.00),
        MechanicalComponent.Gearbox => (0.70, 0.30),
        MechanicalComponent.Suspension => (0.60, 0.20),
        MechanicalComponent.Brakes => (0.40, 0.20),
        MechanicalComponent.ElectricsHydraulics => (0.50, 0.40),
        MechanicalComponent.Cooling => (0.60, 0.60),
        _ => throw new ArgumentOutOfRangeException(nameof(component)),
    };

    /// <summary>ESTIMATE: lower bound of the power lost after a lose-power failure, percent of full power.</summary>
    public const double PowerLossMinPercent = 5;

    /// <summary>ESTIMATE: upper bound of the power lost after a lose-power failure, percent of full power.</summary>
    public const double PowerLossMaxPercent = 25;

    /// <summary>ESTIMATE: lower bound of the time a repair stop costs, seconds.</summary>
    public const double RepairMinSeconds = 40;

    /// <summary>ESTIMATE: upper bound of the time a repair stop costs, seconds.</summary>
    public const double RepairMaxSeconds = 180;

    // ---- Warnings ----

    /// <summary>ESTIMATE: laps of warning a failure gives before it happens, unless the caller configures another value.</summary>
    public const int DefaultWarningLeadLaps = 3;

    /// <summary>
    /// ESTIMATE (PP-057): share of mechanical failures that arrive with no warning and no degraded pace.
    /// The draw is a child of the Failures stream keyed by the car and the failure lap, so it does not move any other draw.
    /// </summary>
    public const double SuddenFailureShare = 0.33;

    /// <summary>ESTIMATE: share of lap time a car with a developing failure loses while the warning is active.</summary>
    public const double DegradedPaceLossFraction = 0.015;

    // ---- Era profile ----

    /// <summary>
    /// ESTIMATE, calibrated in #122 (<c>SimRunner calibrate-race</c>): the per-lap hazard target of the reference car, by season.
    /// Between anchors the profile is interpolated with a smoothstep; outside the first and last anchor it stays flat.
    /// These are NOT the real retirement rates. The hazard is per lap and solved for a typical race length, but the fixture
    /// races are shorter in laps and the cars better than the reference, so the delivered mechanical retirement rate was
    /// about 0.6 times the anchor. The anchors were raised until the delivered rate matched the Jolpica mechanical share of
    /// starters per era band (1950s 43 percent, 1960s 37, 1970s 33, 1980-93 36, 1994-2009 22, 2010 onwards 8), seasons 1950-2025.
    /// A per-distance hazard would remove the factor; that is a model change and not done here.
    /// </summary>
    public static readonly IReadOnlyList<(int Season, double Value)> MechanicalRetirementAnchors =
    [
        (1950, 0.68),
        (1960, 0.62),
        (1970, 0.55),
        (1980, 0.52),
        (1990, 0.44),
        (2000, 0.34),
        (2010, 0.10),
        (2020, 0.085),
    ];

    /// <summary>
    /// ESTIMATE: typical race distance in laps, by season. Races are longer in laps on short circuits, so this is
    /// only a rough average. Same interpolation as <see cref="MechanicalRetirementAnchors"/>.
    /// </summary>
    public static readonly IReadOnlyList<(int Season, double Value)> TypicalRaceLapsAnchors =
    [
        (1950, 75),
        (1970, 70),
        (1990, 62),
        (2010, 58),
    ];
}
