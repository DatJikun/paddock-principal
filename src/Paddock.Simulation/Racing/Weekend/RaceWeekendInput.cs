using System.Collections.Immutable;
using Paddock.Domain.Career;
using Paddock.Domain.People;
using Paddock.Domain.World;
using Paddock.Simulation.Racing.Incidents;
using Paddock.Simulation.Racing.Pace;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Reliability;
using Paddock.Simulation.Racing.Tyres;
using Paddock.Simulation.Racing.Weather;

namespace Paddock.Simulation.Racing.Weekend;

/// <summary>
/// One driver of a car, with the ratings the race loop reads, all on 0..100 (the pace inputs, see
/// <see cref="RaceInputMapping"/>). A plain record: no world state, no Application type.
/// </summary>
/// <param name="DriverId">Stable id of the person.</param>
/// <param name="Pace">The lap-time model's driver ratings (T29). <c>AffinitySeconds</c> is the hidden track affinity; 0 until a source exists.</param>
/// <param name="Aggression">Raises the chance of an incident (T33).</param>
/// <param name="Overtaking">Attack skill, used by <see cref="IPassModel"/>.</param>
/// <param name="Defending">Defence skill, used by <see cref="IPassModel"/>.</param>
/// <param name="Smoothness">Gentleness to the car: tyre wear and the failure model's mechanical sympathy.</param>
public sealed record DriverEntry(
    string DriverId,
    DriverPace Pace,
    double Aggression,
    double Overtaking,
    double Defending,
    double Smoothness);

/// <summary>One car of the field, with everything the weekend needs to know about it. All plain values.</summary>
/// <param name="CarId">Stable id of the car. It selects the car's sub-streams in every RNG stream (INV-004).</param>
/// <param name="ConstructorId">Stable id of the constructor that entered the car.</param>
/// <param name="Drivers">One driver, or two for a shared car. The first is the primary driver: the one on the grid, on the tape and in the classification.</param>
/// <param name="Car">The car's performance vector (T29).</param>
/// <param name="Components">The parts that can fail, with their reliability rating (0..1) and mileage (T31).</param>
/// <param name="Crew">The team's pit crew (T34).</param>
/// <param name="StrategistSkill">0..100 (T34).</param>
/// <param name="ForecastQuality">0..1: how good the team's weather forecast is (T32).</param>
/// <param name="TyreSupplier">The tyre supplier's profile (T30).</param>
/// <param name="PartnerTuned">The tyres are tuned for this team.</param>
/// <param name="Engine">The kind of engine (fuel burn and caps, T30).</param>
/// <param name="InPreQualifying">The car has to go through pre-qualifying where the rules have one (T28).</param>
public sealed record RaceEntry(
    string CarId,
    string ConstructorId,
    ImmutableArray<DriverEntry> Drivers,
    CarPerformance Car,
    ImmutableArray<ComponentState> Components,
    PitCrew Crew,
    int StrategistSkill,
    double ForecastQuality,
    TyreSupplierProfile TyreSupplier,
    bool PartnerTuned,
    FuelEngineType Engine,
    bool InPreQualifying = false)
{
    /// <summary>The primary driver.</summary>
    public DriverEntry Primary => Drivers[0];
}

/// <summary>
/// Everything one race weekend needs, as plain records (no <c>WorldState</c>, no Application type), so a pure function
/// can turn it into a race (T35). The world integration that builds it from the world is T47.
/// </summary>
public sealed record RaceWeekendInput
{
    /// <summary>The career master seed. Every RNG stream is derived from it by (name, season, round) (TECH §4).</summary>
    public required ulong MasterSeed { get; init; }

    public required int Season { get; init; }

    public required int Round { get; init; }

    /// <summary>True for the last round of the season (matters for the double-points finale).</summary>
    public bool IsFinalRound { get; init; }

    public required TrackLayout Track { get; init; }

    /// <summary>The regulations of the season; the weekend reads pit, fuel, qualifying and points rules from it.</summary>
    public required RuleSet Rules { get; init; }

    /// <summary>The safety facts of the season: fatality-risk band, neutralisation rules, red flags (T33). See <see cref="EraSafetyProfile.FromAuthored"/>.</summary>
    public required EraSafetyProfile Safety { get; init; }

    /// <summary>The career's fatality option (PP-006) as a plain value; <see cref="FatalityLevel.Off"/> turns a death into a career-ending injury.</summary>
    public FatalityLevel Fatality { get; init; } = FatalityLevel.Off;

    /// <summary>Where the climate of the circuit comes from (T32).</summary>
    public required IClimateSource Climate { get; init; }

    /// <summary>The race month, 1..12 (climate and temperature).</summary>
    public required int Month { get; init; }

    /// <summary>Laps the race is scheduled for. See <see cref="RaceDistance.LapsFor"/>.</summary>
    public required int TotalLaps { get; init; }

    public required ImmutableArray<RaceEntry> Entries { get; init; }

    /// <summary>The circuit's danger factor for incidents; 1 is typical.</summary>
    public double TrackDanger { get; init; } = WeekendConstants.DefaultTrackDanger;

    /// <summary>The tyre compounds of every era; the synthetic ESTIMATE catalog when null.</summary>
    public TyreCompoundCatalog? Compounds { get; init; }

    /// <summary>The pass rule; <see cref="MarginPassModel"/> when null.</summary>
    public IPassModel? PassModel { get; init; }

    /// <summary>Search limits of the default strategist; <see cref="WeekendConstants.DefaultStrategistMaxStops"/> and its pair when null.</summary>
    public StrategistOptions? StrategistOptions { get; init; }

    /// <summary>How often a team consults its strategist, in laps; see <see cref="WeekendConstants.StrategistCadenceLaps"/>.</summary>
    public int StrategistCadenceLaps { get; init; } = WeekendConstants.StrategistCadenceLaps;

    /// <summary>
    /// Orders from the pit walls during the race (#286), in the order they were given. Empty for a race nobody steers: the
    /// strategists alone decide, as before.
    /// </summary>
    public ImmutableArray<PitWallOrder> Orders { get; init; } = [];
}

/// <summary>
/// The single mapping from the visible 1..20 driver attributes to the 0..100 pace inputs of the race loop. ESTIMATE:
/// linear x<see cref="WeekendConstants.AttributeToRatingScale"/>. T22 ratings replace this function and nothing else.
/// </summary>
public static class RaceInputMapping
{
    /// <summary>
    /// Pace from Cornering, Consistency and Composure as they are, Overtaking, Defending and Smoothness as they are.
    /// There is no aggression attribute yet, so the caller gives it (1..20). Braking, Adaptability, Wet weather,
    /// Fitness and Feedback are not read yet.
    /// </summary>
    public static DriverEntry DriverFrom(string driverId, DriverAttributes attributes, int aggression1To20, double affinitySeconds = 0d, double paceMultiplier = 1.0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverId);
        attributes.EnsureValid();
        return new DriverEntry(
            driverId,
            new DriverPace(
                Scale(attributes.Cornering) * paceMultiplier,
                Scale(attributes.Consistency),
                Scale(attributes.Composure),
                affinitySeconds),
            Scale(aggression1To20),
            Scale(attributes.Overtaking),
            Scale(attributes.Defending),
            Scale(attributes.Smoothness));
    }

    /// <summary>A 1..20 value as a 0..100 rating.</summary>
    public static double Scale(int attribute1To20) => attribute1To20 * WeekendConstants.AttributeToRatingScale;

    /// <summary>The reliability components of a car with one rating for all parts (rating 0..100 as in <see cref="CarPerformance.Reliability"/>).</summary>
    public static ImmutableArray<ComponentState> UniformComponents(double reliability0To100, double mileageLaps = 0d) =>
        [.. Enum.GetValues<MechanicalComponent>().Select(c => new ComponentState(c, reliability0To100 / 100d, mileageLaps))];
}
