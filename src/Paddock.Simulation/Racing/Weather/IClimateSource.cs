using System.Globalization;

namespace Paddock.Simulation.Racing.Weather;

/// <summary>Where the weather generator gets the climate of a circuit in a race month. R9 data can implement this later.</summary>
public interface IClimateSource
{
    ClimateProfile For(string circuitId, int month);
}

/// <summary>Table row: climate class of a circuit and whether it lies in the southern hemisphere (seasons shifted by six months).</summary>
public sealed record CircuitClimateAssignment(ClimateClass ClimateClass, bool SouthernHemisphere = false);

/// <summary>
/// Built-in climate source: a small circuit-to-class table plus per-class numbers. EVERYTHING HERE IS AN ESTIMATE
/// AND A PLACEHOLDER: the class assignments are coarse author guesses for a handful of circuits, not researched
/// facts (the R9 data in <c>data/authored/weather</c> will replace this). Circuits not in the table fall back to
/// <see cref="FallbackClass"/>. The month only drives the temperature (a cosine around the class mean, peaking in
/// July, or January in the south); the rain band is per class.
/// </summary>
public sealed class DefaultClimateSource : IClimateSource
{
    /// <summary>ESTIMATE: class used for a circuit missing from the table.</summary>
    public const ClimateClass FallbackClass = ClimateClass.Continental;

    /// <summary>ESTIMATE / placeholder assignments for a few circuits, enough to exercise every class.</summary>
    public static readonly IReadOnlyDictionary<string, CircuitClimateAssignment> BuiltInAssignments =
        new Dictionary<string, CircuitClimateAssignment>(StringComparer.Ordinal)
        {
            ["silverstone"] = new(ClimateClass.Maritime),
            ["spa"] = new(ClimateClass.Maritime),
            ["zandvoort"] = new(ClimateClass.Maritime),
            ["hungaroring"] = new(ClimateClass.Continental),
            ["red_bull_ring"] = new(ClimateClass.Continental),
            ["monza"] = new(ClimateClass.Continental),
            ["monaco"] = new(ClimateClass.Mediterranean),
            ["catalunya"] = new(ClimateClass.Mediterranean),
            ["bahrain"] = new(ClimateClass.Desert),
            ["yas_marina"] = new(ClimateClass.Desert),
            ["sepang"] = new(ClimateClass.Tropical),
            ["marina_bay"] = new(ClimateClass.Tropical),
            ["interlagos"] = new(ClimateClass.Tropical, SouthernHemisphere: true),
            ["albert_park"] = new(ClimateClass.Maritime, SouthernHemisphere: true),
        };

    private readonly IReadOnlyDictionary<string, CircuitClimateAssignment> _assignments;

    public DefaultClimateSource()
        : this(BuiltInAssignments)
    {
    }

    public DefaultClimateSource(IReadOnlyDictionary<string, CircuitClimateAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        _assignments = assignments;
    }

    public ClimateProfile For(string circuitId, int month)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(circuitId);
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(month),
                string.Create(CultureInfo.InvariantCulture, $"Month must be 1..12, was {month}."));
        }

        var assignment = _assignments.TryGetValue(circuitId, out var found)
            ? found
            : new CircuitClimateAssignment(FallbackClass);

        var (mean, amplitude, halfRange, variability, band) = Numbers(assignment.ClimateClass);
        var peak = assignment.SouthernHemisphere
            ? ((WeatherConstants.NorthernPeakMonth + 5) % 12) + 1
            : WeatherConstants.NorthernPeakMonth;
        var monthMean = mean + (amplitude * Math.Cos(2 * Math.PI * (month - peak) / 12.0));
        return ClimateProfile.FromBand(
            assignment.ClimateClass,
            band,
            monthMean - halfRange,
            monthMean + halfRange,
            variability);
    }

    private static (double Mean, double Amplitude, double HalfRange, double Variability, RainBand Band) Numbers(ClimateClass climateClass) =>
        climateClass switch
        {
            ClimateClass.Maritime => (WeatherConstants.MaritimeMeanC, WeatherConstants.MaritimeAmplitudeC, WeatherConstants.MaritimeHalfRangeC, WeatherConstants.MaritimeVariability, RainBand.High),
            ClimateClass.Continental => (WeatherConstants.ContinentalMeanC, WeatherConstants.ContinentalAmplitudeC, WeatherConstants.ContinentalHalfRangeC, WeatherConstants.ContinentalVariability, RainBand.Medium),
            ClimateClass.Mediterranean => (WeatherConstants.MediterraneanMeanC, WeatherConstants.MediterraneanAmplitudeC, WeatherConstants.MediterraneanHalfRangeC, WeatherConstants.MediterraneanVariability, RainBand.Low),
            ClimateClass.Desert => (WeatherConstants.DesertMeanC, WeatherConstants.DesertAmplitudeC, WeatherConstants.DesertHalfRangeC, WeatherConstants.DesertVariability, RainBand.Low),
            ClimateClass.Tropical => (WeatherConstants.TropicalMeanC, WeatherConstants.TropicalAmplitudeC, WeatherConstants.TropicalHalfRangeC, WeatherConstants.TropicalVariability, RainBand.High),
            _ => throw new ArgumentOutOfRangeException(nameof(climateClass)),
        };
}
