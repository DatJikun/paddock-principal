using Paddock.Domain.Random;

namespace Paddock.Simulation.Racing.Weather;

/// <summary>
/// What the weather actually is at one minute of the race. TRUTH, not knowledge (INV-003): the race loop may use it
/// to compute pace; actors see only the current observation plus a <see cref="WeatherForecast"/>.
/// </summary>
/// <param name="Minute">Minutes since the start of the race (the grid is one minute).</param>
/// <param name="AirTempC">Air temperature, C.</param>
/// <param name="TrackTempC">Track surface temperature, C.</param>
/// <param name="RainIntensity">Rain falling right now, 0 (none) to 1 (torrential).</param>
/// <param name="TrackWetness">Wetness of the racing line, 0 (dry) to 1 (soaked); see <see cref="TrackWetnessModel"/>.</param>
public sealed record WeatherSample(int Minute, double AirTempC, double TrackTempC, double RainIntensity, double TrackWetness)
{
    public WetnessBand WetnessBand => WetnessBands.From(TrackWetness);
}

/// <summary>
/// The full, true weather of one race, sampled once per minute from minute 0 to <see cref="DurationMinutes"/>.
/// Immutable. Produced by <see cref="RaceWeather"/>. Forecasts are made separately by <see cref="WeatherForecaster"/>
/// and never read future samples exactly.
/// </summary>
public sealed class TruthWeather
{
    private readonly WeatherSample[] _samples;

    internal TruthWeather(RngStream stream, ClimateProfile climate, bool showery, int onsetMinute, WeatherSample[] samples)
    {
        Stream = stream;
        Climate = climate;
        IsShowery = showery;
        OnsetMinute = showery ? onsetMinute : null;
        _samples = samples;
    }

    /// <summary>Snapshot of the per-race Weather stream; forecast noise is hashed from it without advancing anything (INV-005).</summary>
    internal RngStream Stream { get; }

    public ClimateProfile Climate { get; }

    /// <summary>True when the race-level draw decided this race gets rain at some point.</summary>
    public bool IsShowery { get; }

    /// <summary>Minute the first rain spell starts, or null on a dry race.</summary>
    public int? OnsetMinute { get; }

    public int DurationMinutes => _samples.Length - 1;

    public IReadOnlyList<WeatherSample> Samples => _samples;

    /// <summary>True when any sample reaches <see cref="WeatherConstants.MeasurableRainIntensity"/>.</summary>
    public bool HasRain => _samples.Any(s => s.RainIntensity >= WeatherConstants.MeasurableRainIntensity);

    /// <summary>Sample at a minute; minutes outside the race are clamped to its first and last sample.</summary>
    public WeatherSample At(double minute)
    {
        var index = (int)Math.Floor(Math.Clamp(minute, 0, DurationMinutes));
        return _samples[index];
    }

    /// <summary>Sample at the start of a lap (1-based) for a constant lap duration in minutes.</summary>
    public WeatherSample AtLap(int lap, double lapMinutes)
    {
        if (lap < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(lap), "Laps are 1-based.");
        }

        if (!(lapMinutes > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(lapMinutes), "Must be positive.");
        }

        return At((lap - 1) * lapMinutes);
    }
}
