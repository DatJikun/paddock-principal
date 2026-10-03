namespace Paddock.Simulation.Racing.Weather;

/// <summary>Coarse climate class a circuit is filed under until R9 data replaces the table.</summary>
public enum ClimateClass
{
    Maritime,
    Continental,
    Mediterranean,
    Desert,
    Tropical,
}

/// <summary>Rain-probability band of a circuit in its race month.</summary>
public enum RainBand
{
    Low,
    Medium,
    High,
}

/// <summary>
/// Climate of one circuit in one race month: how likely a race is to see rain, the typical air-temperature range
/// and how changeable the weather is. The numbers behind the built-in profiles are ESTIMATES
/// (<see cref="WeatherConstants"/>).
/// </summary>
public sealed class ClimateProfile
{
    public ClimateProfile(
        ClimateClass climateClass,
        RainBand rainBand,
        double raceRainProbability,
        double airTempMinC,
        double airTempMaxC,
        double variability)
    {
        if (double.IsNaN(raceRainProbability) || raceRainProbability is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(raceRainProbability), "Must be in [0, 1].");
        }

        if (double.IsNaN(airTempMinC) || double.IsNaN(airTempMaxC) || airTempMinC > airTempMaxC)
        {
            throw new ArgumentException("Air temperature range must be ordered.", nameof(airTempMaxC));
        }

        if (double.IsNaN(variability) || variability is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(variability), "Must be in [0, 1].");
        }

        ClimateClass = climateClass;
        RainBand = rainBand;
        RaceRainProbability = raceRainProbability;
        AirTempMinC = airTempMinC;
        AirTempMaxC = airTempMaxC;
        Variability = variability;
    }

    public ClimateClass ClimateClass { get; }

    public RainBand RainBand { get; }

    /// <summary>Probability that a race sees measurable rain at some point.</summary>
    public double RaceRainProbability { get; }

    public double AirTempMinC { get; }

    public double AirTempMaxC { get; }

    /// <summary>0 = steady weather (long spells, small wander), 1 = changeable (short spells, bigger wander).</summary>
    public double Variability { get; }

    public static double ProbabilityFor(RainBand band) => band switch
    {
        RainBand.Low => WeatherConstants.RaceRainProbabilityLow,
        RainBand.Medium => WeatherConstants.RaceRainProbabilityMedium,
        RainBand.High => WeatherConstants.RaceRainProbabilityHigh,
        _ => throw new ArgumentOutOfRangeException(nameof(band)),
    };

    /// <summary>Profile whose rain probability comes from the band.</summary>
    public static ClimateProfile FromBand(
        ClimateClass climateClass,
        RainBand band,
        double airTempMinC,
        double airTempMaxC,
        double variability) =>
        new(climateClass, band, ProbabilityFor(band), airTempMinC, airTempMaxC, variability);
}
