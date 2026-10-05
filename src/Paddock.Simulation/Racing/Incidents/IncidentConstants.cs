namespace Paddock.Simulation.Racing.Incidents;

/// <summary>
/// Every tunable number of the incident model, in one place.
/// ESTIMATE: every value in this file is a guess made to give plausible behaviour. None is calibrated against real data.
/// The model describes the real deaths and injuries of past eras only as bands (<c>fatality_risk</c> in the authored data);
/// the numbers are shaped to those bands and must not be read as statistics.
/// </summary>
public static class IncidentConstants
{
    // ---- Occurrence: p = base * era * driver * contact * wet * lap * danger, per car and lap ----

    /// <summary>ESTIMATE: probability that a typical driver (aggression 50, composure 50) has an incident on one lap, with no car near, on a dry track of typical danger, in a low-risk era.</summary>
    public const double BaseIncidentRatePerCarLap = 0.0012;

    /// <summary>
    /// ESTIMATE, calibrated in #122 against the Jolpica accident share of starters (seasons 1950-2025, fixture field): older
    /// eras had more incidents (less grip, less safe cars). Raised from 1.6/1.4/1.15/1.0/0.9; the low band is held at 1.6
    /// because more would put a safety car in over 80 percent of the 2012 fixture races (<c>WeekendEraTests</c>), and the
    /// order must stay monotone (<c>IncidentSamplerTests</c>), so the real 1990s and 2000s accident peak is still under-delivered.
    /// </summary>
    public static double EraRateMultiplier(FatalityRiskBand band) => band switch
    {
        FatalityRiskBand.VeryHigh => 2.8,
        FatalityRiskBand.High => 2.7,
        FatalityRiskBand.Moderate => 2.6,
        FatalityRiskBand.Low => 1.6,
        FatalityRiskBand.VeryLow => 0.9,
        _ => throw new ArgumentOutOfRangeException(nameof(band)),
    };

    /// <summary>ESTIMATE: the factor is exp(AggressionExponent * (aggression - 50) / 50); aggression 100 gives about 1.65.</summary>
    public const double AggressionExponent = 0.5;

    /// <summary>ESTIMATE: the factor is exp(-ComposureExponent * (composure - 50) / 50); composure 100 gives about 0.64.</summary>
    public const double ComposureExponent = 0.45;

    /// <summary>ESTIMATE: the contact factor is 1 + weight * cars within one second (capped by <see cref="MaxContactCars"/>).</summary>
    public const double ContactDensityWeight = 0.35;

    /// <summary>ESTIMATE: cars beyond this number do not raise the contact factor further.</summary>
    public const int MaxContactCars = 4;

    /// <summary>ESTIMATE: the wet factor is 1 + WetnessWeight * wetness (wetness in [0, 1]).</summary>
    public const double WetnessWeight = 1.5;

    /// <summary>ESTIMATE: multiplier of the incident rate on the first lap (the first-lap chaos factor).</summary>
    public const double FirstLapFactor = 5.0;

    /// <summary>ESTIMATE: multiplier on the second lap, while the field is still bunched.</summary>
    public const double SecondLapFactor = 1.5;

    /// <summary>ESTIMATE: lower bound of the track danger factor (1 is a typical circuit).</summary>
    public const double MinTrackDanger = 0.1;

    /// <summary>ESTIMATE: upper bound of the track danger factor.</summary>
    public const double MaxTrackDanger = 5.0;

    /// <summary>ESTIMATE: upper bound of the probability of an incident on one lap for one car.</summary>
    public const double MaxLapProbability = 0.5;

    // ---- Kind ----

    /// <summary>
    /// ESTIMATE: relative weight of the kind of incident. For contact and collision it is multiplied by the number of cars
    /// nearby (capped), so it is zero with nobody near and a two-car incident never happens alone.
    /// </summary>
    public static double KindWeight(IncidentKind kind) => kind switch
    {
        IncidentKind.Spin => 0.30,
        IncidentKind.Contact => 0.20,
        IncidentKind.Collision => 0.15,
        IncidentKind.Barrier => 0.25,
        IncidentKind.Debris => 0.10,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>ESTIMATE: in the wet, the weight of spins and barrier crashes is multiplied by 1 + wetness * this.</summary>
    public const double WetKindBoost = 1.0;

    /// <summary>ESTIMATE: on the first lap, contact and collision weights are multiplied by this.</summary>
    public const double FirstLapContactBias = 2.0;

    // ---- Severity of one car's outcome ----

    /// <summary>ESTIMATE: probability that the car is out of the race (or worse), given the kind.</summary>
    public static double HardCrashShare(IncidentKind kind) => kind switch
    {
        IncidentKind.Spin => 0.15,
        IncidentKind.Contact => 0.20,
        IncidentKind.Collision => 0.55,
        IncidentKind.Barrier => 0.70,
        IncidentKind.Debris => 0.15,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>ESTIMATE: probability of a minor outcome (damage, lost time), given the kind. With <see cref="HardCrashShare"/> it is at most 1; the rest has no consequence.</summary>
    public static double MinorShare(IncidentKind kind) => kind switch
    {
        IncidentKind.Spin => 0.35,
        IncidentKind.Contact => 0.45,
        IncidentKind.Collision => 0.30,
        IncidentKind.Barrier => 0.20,
        IncidentKind.Debris => 0.45,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>ESTIMATE: share of out-of-the-race crashes in which the driver is hurt, by era band. The main era scale of severity.</summary>
    public static double InjuryShareOfHardCrash(FatalityRiskBand band) => band switch
    {
        FatalityRiskBand.VeryHigh => 0.40,
        FatalityRiskBand.High => 0.30,
        FatalityRiskBand.Moderate => 0.15,
        FatalityRiskBand.Low => 0.06,
        FatalityRiskBand.VeryLow => 0.03,
        _ => throw new ArgumentOutOfRangeException(nameof(band)),
    };

    /// <summary>ESTIMATE: how much the kind scales the injury share (a barrier hit is harder than a spin).</summary>
    public static double InjuryKindFactor(IncidentKind kind) => kind switch
    {
        IncidentKind.Spin => 0.3,
        IncidentKind.Contact => 0.5,
        IncidentKind.Collision => 1.0,
        IncidentKind.Barrier => 1.2,
        IncidentKind.Debris => 0.3,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>
    /// ESTIMATE: split of the injuries into light, serious, career-ending and fatal-class by era band. Each row sums to 1.
    /// The fatal-class share is mapped to a career-ending injury when fatalities are disabled (PP-006).
    /// </summary>
    public static (double Light, double Serious, double CareerEnding, double FatalClass) InjurySplit(FatalityRiskBand band) => band switch
    {
        FatalityRiskBand.VeryHigh => (0.40, 0.30, 0.15, 0.15),
        FatalityRiskBand.High => (0.45, 0.30, 0.15, 0.10),
        FatalityRiskBand.Moderate => (0.55, 0.28, 0.12, 0.05),
        FatalityRiskBand.Low => (0.65, 0.25, 0.08, 0.02),
        FatalityRiskBand.VeryLow => (0.72, 0.22, 0.05, 0.01),
        _ => throw new ArgumentOutOfRangeException(nameof(band)),
    };

    // ---- Neutralisation ----

    /// <summary>ESTIMATE: the first season with marshals and yellow flags, and with a red flag available. The authored data has no dimension for either.</summary>
    public const int MarshalsAndFlagsFromSeason = 1960;

    /// <summary>ESTIMATE: laps behind a virtual safety car after a single-car retirement or a light injury.</summary>
    public const int VscLaps = 2;

    /// <summary>ESTIMATE: laps behind the safety car after a single-car retirement or a light injury.</summary>
    public const int SafetyCarRetirementLaps = 3;

    /// <summary>ESTIMATE: laps behind the safety car after a retirement in a two-car incident, or after debris.</summary>
    public const int SafetyCarMultiCarLaps = 4;

    /// <summary>ESTIMATE: laps behind the safety car after a serious injury when no red flag is available.</summary>
    public const int SafetyCarSeriousLaps = 6;

    /// <summary>ESTIMATE: length of a red-flag suspension, in laps of racing, after a serious injury.</summary>
    public const int RedFlagSeriousLaps = 6;

    /// <summary>ESTIMATE: length of a red-flag suspension, in laps of racing, after a career-ending injury or a death.</summary>
    public const int RedFlagSevereLaps = 10;

    /// <summary>ESTIMATE: a red-flagged race resumes when the incident is within this fraction of the distance; after that it is not restarted.</summary>
    public const double RedFlagResumeUntilFraction = 0.75;

    // ---- Recovery & Stand-in (PP-061, #219) ----

    /// <summary>ESTIMATE: days after a light injury during which a racing driver has reduced pace.</summary>
    public const int LightPaceDays = 21;

    /// <summary>ESTIMATE: pace share reduction for a driver racing within <see cref="LightPaceDays"/> of an injury.</summary>
    public const double LightInjuryPacePenalty = 0.03;

    /// <summary>ESTIMATE: minimum number of championship races missed from a serious injury.</summary>
    public const int MinSeriousRaces = 2;

    /// <summary>ESTIMATE: maximum number of championship races missed from a serious injury.</summary>
    public const int MaxSeriousRaces = 6;

    /// <summary>ESTIMATE: probability that a lightly injured driver misses zero championship races (otherwise one).</summary>
    public const double LightInjuryZeroRacesProbability = 0.5;
}
