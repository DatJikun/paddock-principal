using System.Globalization;

namespace Paddock.Simulation.Racing.Weather;

/// <summary>Where the weather generator gets the climate of a circuit in a race month. R9 data can implement this later.</summary>
public interface IClimateSource
{
    ClimateProfile For(string circuitId, int month);
}

/// <summary>Table row: climate class of a circuit, whether it lies in the southern hemisphere (seasons shifted by six months) and, when the R9 data says otherwise, the circuit's own rain band.</summary>
public sealed record CircuitClimateAssignment(ClimateClass ClimateClass, bool SouthernHemisphere = false, RainBand? RainBandOverride = null);

/// <summary>
/// Built-in climate source: a circuit-to-class table (every circuit of R9, <c>data/authored/weather/circuit_climate.json</c>)
/// plus per-class numbers. STILL ESTIMATES: the rain band of each row is the R9 band (filed from published precipitation
/// tables), but the class and hemisphere are coarse author judgements, and the class temperature numbers were checked
/// against the R9 race air temperatures only (see <c>calibrate-race</c>), not fitted per circuit. Circuits not in the table fall back to
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
            ["adelaide"] = new(ClimateClass.Mediterranean, SouthernHemisphere: true, RainBandOverride: RainBand.Low),
            ["ain-diab"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Low),
            ["aintree"] = new(ClimateClass.Maritime, RainBandOverride: RainBand.Medium),
            ["albert_park"] = new(ClimateClass.Maritime, SouthernHemisphere: true, RainBandOverride: RainBand.Low),
            ["americas"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Medium),
            ["anderstorp"] = new(ClimateClass.Continental, RainBandOverride: RainBand.Medium),
            ["avus"] = new(ClimateClass.Continental, RainBandOverride: RainBand.Medium),
            ["bahrain"] = new(ClimateClass.Desert, RainBandOverride: RainBand.Low),
            ["baku"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Low),
            ["boavista"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Low),
            ["brands_hatch"] = new(ClimateClass.Maritime, RainBandOverride: RainBand.Medium),
            ["bremgarten"] = new(ClimateClass.Continental, RainBandOverride: RainBand.High),
            ["buddh"] = new(ClimateClass.Desert, RainBandOverride: RainBand.Low),
            ["catalunya"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Medium),
            ["charade"] = new(ClimateClass.Continental, RainBandOverride: RainBand.Medium),
            ["dallas"] = new(ClimateClass.Desert, RainBandOverride: RainBand.Low),
            ["detroit"] = new(ClimateClass.Continental, RainBandOverride: RainBand.Medium),
            ["dijon"] = new(ClimateClass.Continental, RainBandOverride: RainBand.Medium),
            ["donington"] = new(ClimateClass.Maritime, RainBandOverride: RainBand.Low),
            ["essarts"] = new(ClimateClass.Maritime, RainBandOverride: RainBand.Medium),
            ["estoril"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Low),
            ["fuji"] = new(ClimateClass.Continental, RainBandOverride: RainBand.High),
            ["galvez"] = new(ClimateClass.Mediterranean, SouthernHemisphere: true, RainBandOverride: RainBand.High),
            ["george"] = new(ClimateClass.Mediterranean, SouthernHemisphere: true, RainBandOverride: RainBand.Medium),
            ["hockenheimring"] = new(ClimateClass.Continental, RainBandOverride: RainBand.Medium),
            ["hungaroring"] = new(ClimateClass.Continental, RainBandOverride: RainBand.Medium),
            ["imola"] = new(ClimateClass.Continental, RainBandOverride: RainBand.Medium),
            ["indianapolis"] = new(ClimateClass.Continental, RainBandOverride: RainBand.High),
            ["interlagos"] = new(ClimateClass.Tropical, SouthernHemisphere: true, RainBandOverride: RainBand.High),
            ["istanbul"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Medium),
            ["jacarepagua"] = new(ClimateClass.Tropical, SouthernHemisphere: true, RainBandOverride: RainBand.High),
            ["jarama"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Low),
            ["jeddah"] = new(ClimateClass.Desert, RainBandOverride: RainBand.Low),
            ["jerez"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Medium),
            ["kyalami"] = new(ClimateClass.Mediterranean, SouthernHemisphere: true, RainBandOverride: RainBand.Medium),
            ["las_vegas"] = new(ClimateClass.Desert, RainBandOverride: RainBand.Low),
            ["lemans"] = new(ClimateClass.Maritime, RainBandOverride: RainBand.Low),
            ["long_beach"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Low),
            ["losail"] = new(ClimateClass.Desert, RainBandOverride: RainBand.Low),
            ["madring"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Low),
            ["magny_cours"] = new(ClimateClass.Continental, RainBandOverride: RainBand.Medium),
            ["marina_bay"] = new(ClimateClass.Tropical, RainBandOverride: RainBand.High),
            ["miami"] = new(ClimateClass.Tropical, RainBandOverride: RainBand.High),
            ["monaco"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Low),
            ["monsanto"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Low),
            ["montjuic"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Low),
            ["monza"] = new(ClimateClass.Continental, RainBandOverride: RainBand.Medium),
            ["mosport"] = new(ClimateClass.Continental, RainBandOverride: RainBand.Medium),
            ["mugello"] = new(ClimateClass.Continental, RainBandOverride: RainBand.Medium),
            ["nivelles"] = new(ClimateClass.Maritime, RainBandOverride: RainBand.Medium),
            ["nurburgring"] = new(ClimateClass.Maritime, RainBandOverride: RainBand.Medium),
            ["okayama"] = new(ClimateClass.Continental, RainBandOverride: RainBand.Medium),
            ["pedralbes"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Medium),
            ["pescara"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Low),
            ["phoenix"] = new(ClimateClass.Desert, RainBandOverride: RainBand.Low),
            ["portimao"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Low),
            ["red_bull_ring"] = new(ClimateClass.Continental, RainBandOverride: RainBand.High),
            ["reims"] = new(ClimateClass.Continental, RainBandOverride: RainBand.Medium),
            ["ricard"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Low),
            ["riverside"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Low),
            ["rodriguez"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Medium),
            ["sebring"] = new(ClimateClass.Tropical, RainBandOverride: RainBand.Low),
            ["sepang"] = new(ClimateClass.Tropical, RainBandOverride: RainBand.High),
            ["shanghai"] = new(ClimateClass.Continental, RainBandOverride: RainBand.Medium),
            ["silverstone"] = new(ClimateClass.Maritime, RainBandOverride: RainBand.Medium),
            ["sochi"] = new(ClimateClass.Continental, RainBandOverride: RainBand.High),
            ["spa"] = new(ClimateClass.Maritime, RainBandOverride: RainBand.Medium),
            ["suzuka"] = new(ClimateClass.Continental, RainBandOverride: RainBand.High),
            ["tremblant"] = new(ClimateClass.Continental, RainBandOverride: RainBand.High),
            ["valencia"] = new(ClimateClass.Mediterranean, RainBandOverride: RainBand.Low),
            ["vegas"] = new(ClimateClass.Desert, RainBandOverride: RainBand.Low),
            ["villeneuve"] = new(ClimateClass.Continental, RainBandOverride: RainBand.Medium),
            ["watkins_glen"] = new(ClimateClass.Continental, RainBandOverride: RainBand.Medium),
            ["yas_marina"] = new(ClimateClass.Desert, RainBandOverride: RainBand.Low),
            ["yeongam"] = new(ClimateClass.Continental, RainBandOverride: RainBand.Medium),
            ["zandvoort"] = new(ClimateClass.Maritime, RainBandOverride: RainBand.Medium),
            ["zeltweg"] = new(ClimateClass.Continental, RainBandOverride: RainBand.High),
            ["zolder"] = new(ClimateClass.Maritime, RainBandOverride: RainBand.Medium),
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

        var (mean, amplitude, halfRange, variability, classBand) = Numbers(assignment.ClimateClass);
        var band = assignment.RainBandOverride ?? classBand;
        var peak = assignment.SouthernHemisphere
            ? ((WeatherConstants.NorthernPeakMonth + 5) % 12) + 1
            : WeatherConstants.NorthernPeakMonth;
        var monthMean = mean + WeatherConstants.RaceAfternoonWarmingC + (amplitude * Math.Cos(2 * Math.PI * (month - peak) / 12.0));
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
