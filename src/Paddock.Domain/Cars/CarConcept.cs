namespace Paddock.Domain.Cars;

/// <summary>
/// One season's design, as six axes (DESIGN §5.2). Each value is −1 at the first pole and +1 at the second.
/// Aero: drag lean ↔ downforce. Philosophy: evolution ↔ revolution. Window: wide ↔ narrow.
/// Cooling: on the edge ↔ margin. Tyre kindness: aggressive ↔ kind. Integration: tailored ↔ universal.
/// </summary>
public readonly record struct CarConcept
{
    public CarConcept(double aero, double philosophy, double window, double cooling, double tyreKindness, double integration)
    {
        Aero = Axis(aero, nameof(aero));
        Philosophy = Axis(philosophy, nameof(philosophy));
        Window = Axis(window, nameof(window));
        Cooling = Axis(cooling, nameof(cooling));
        TyreKindness = Axis(tyreKindness, nameof(tyreKindness));
        Integration = Axis(integration, nameof(integration));
    }

    public static CarConcept Neutral { get; } = new(0, 0, 0, 0, 0, 0);

    public double Aero { get; init; }

    public double Philosophy { get; init; }

    public double Window { get; init; }

    public double Cooling { get; init; }

    public double TyreKindness { get; init; }

    public double Integration { get; init; }

    public static bool InRange(double value) => value is >= -1d and <= 1d && !double.IsNaN(value);

    private static double Axis(double value, string name)
    {
        if (!InRange(value))
        {
            throw new ArgumentOutOfRangeException(name, value, "A concept axis is from -1 to 1.");
        }

        return CarEstimates.Quantize(value);
    }
}

/// <summary>The performance vector of DESIGN §5.4, each component on 0..100. This is simulation truth.</summary>
public readonly record struct PerformanceLevels(double Power, double Downforce, double MechanicalGrip, double Braking, double Reliability)
{
    public static PerformanceLevels Of(double power, double downforce, double grip, double braking, double reliability) =>
        new(
            CarEstimates.ClampRating(power),
            CarEstimates.ClampRating(downforce),
            CarEstimates.ClampRating(grip),
            CarEstimates.ClampRating(braking),
            CarEstimates.ClampRating(reliability));

    public PerformanceLevels Scale(double fraction)
    {
        if (fraction < 0d || double.IsNaN(fraction))
        {
            throw new ArgumentOutOfRangeException(nameof(fraction));
        }

        return Of(Power * fraction, Downforce * fraction, MechanicalGrip * fraction, Braking * fraction, Reliability * fraction);
    }
}
