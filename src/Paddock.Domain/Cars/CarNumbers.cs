namespace Paddock.Domain.Cars;

/// <summary>
/// Uncalibrated numbers of the car model (T41). Every value is an ESTIMATE until the race engine is calibrated.
/// The axis-to-vector table is a proposal for review, not a settled decision.
/// </summary>
public static class CarEstimates
{
    public const int CarsPerTeam = 2;

    /// <summary>ESTIMATE: mean concept ceiling when the philosophy is pure evolution, before staff quality.</summary>
    public const double EvolutionCeilingMean = 58;

    /// <summary>ESTIMATE: mean concept ceiling when the philosophy is pure revolution, before staff quality.</summary>
    public const double RevolutionCeilingMean = 80;

    /// <summary>ESTIMATE: spread of the evolution ceiling.</summary>
    public const double EvolutionCeilingSd = 3.5;

    /// <summary>ESTIMATE: spread of the revolution ceiling. Wider than evolution.</summary>
    public const double RevolutionCeilingSd = 10;

    /// <summary>ESTIMATE: fraction of the ceiling a pure-evolution car already has on the day it is approved.</summary>
    public const double EvolutionStartFraction = 0.82;

    /// <summary>ESTIMATE: fraction of the ceiling a pure-revolution car already has on the day it is approved.</summary>
    public const double RevolutionStartFraction = 0.48;

    /// <summary>ESTIMATE: staff at the bottom of the scale still deliver this share of the ceiling mean.</summary>
    public const double ExecutionFloor = 0.75;

    /// <summary>ESTIMATE: understanding of a car the team is already racing at the start of the world.</summary>
    public const double InitialUnderstanding = 100;

    /// <summary>ESTIMATE: understanding on the day a new concept is approved. In-season gains are T42.</summary>
    public const double NewConceptUnderstanding = 25;

    /// <summary>
    /// ESTIMATE: rating points a car with no understanding at all loses on downforce, grip, braking and reliability, when the race engine
    /// reads it (PP-066). It falls in proportion as understanding rises, so a new concept costs something until it is understood.
    /// </summary>
    public const double UnderstandingMaxLoss = 6;

    /// <summary>ESTIMATE: car strength when no historical effect and no tier draw applies.</summary>
    public const double TierFallback = 50;

    /// <summary>ESTIMATE: headroom above initial car strength for in-season development.</summary>
    public const double InitialHeadroom = 15;

    /// <summary>ESTIMATE: narrowest half-width of an engineer's band, even for a perfect technical director.</summary>
    public const double MinHalfWidth = 3;

    /// <summary>ESTIMATE: widest half-width, for the weakest technical director and aero head.</summary>
    public const double MaxHalfWidth = 16;

    /// <summary>ESTIMATE: attribute used when the team has no one in that chair (1–20 scale).</summary>
    public const int DefaultStaffAttribute = 10;

    /// <summary>ESTIMATE: seconds lost per unit of preference mismatch. Positive affinity means faster, so the modifier is negative.</summary>
    public const double PaceSecondsPerMismatch = 0.12;

    /// <summary>ESTIMATE: confidence change when the car matches the driver exactly.</summary>
    public const double ConfidenceAtMatch = 4;

    /// <summary>ESTIMATE: confidence lost per unit of preference mismatch.</summary>
    public const double ConfidencePerMismatch = 6;

    /// <summary>Same Irwin–Hall width the people generator uses, so a draw is stable across operating systems.</summary>
    public const int NormalSampleCount = 12;

    public static double Quantize(double value) =>
        Math.Round(value, 3, MidpointRounding.AwayFromZero);

    public static int Milli(double value) =>
        (int)Math.Round(value * 1000d, MidpointRounding.AwayFromZero);

    public static double FromMilli(int milli) => milli / 1000d;

    public static double ClampRating(double value) => Quantize(Math.Clamp(value, 0d, 100d));

    public static double ClampAxis(double value) => Quantize(Math.Clamp(value, -1d, 1d));
}
