using System.Globalization;
using Paddock.Domain.Random;
using Paddock.Simulation.Racing.Tyres;

namespace Paddock.Simulation.Racing.Pits;

/// <summary>The work done in one stop.</summary>
/// <param name="ChangeTyres">A new set goes on.</param>
/// <param name="RefuelKg">Fuel put in, kg; 0 for none.</param>
/// <param name="ChangeDriver">The driver of a shared car is changed.</param>
public readonly record struct PitStopRequest(bool ChangeTyres, double RefuelKg = 0, bool ChangeDriver = false);

/// <summary>The pit crew of a team.</summary>
/// <param name="Quality">0 (worst) to 100 (best); 50 is an average crew.</param>
/// <param name="CrewSize">People working on the car; null for the standard crew of the era.</param>
public readonly record struct PitCrew(double Quality, int? CrewSize = null);

/// <summary>What went wrong in a stop, if anything.</summary>
public enum PitStopFault
{
    None,

    /// <summary>A slow stop: a sticky wheel nut, a fumbled jack, a few seconds lost.</summary>
    SlowStop,

    /// <summary>An error: a stuck wheel, a stall, a long delay.</summary>
    Error,
}

/// <summary>The time of one pit stop, in parts. <c>Total = LaneLoss + Service + CrewVariance + Fault</c>.</summary>
/// <param name="LaneLossSeconds">Time lost in the pit lane (see <see cref="PitRules.PitLaneTimeLossSeconds"/>).</param>
/// <param name="ServiceSeconds">Nominal time the car stands still: the slowest of the jobs, scaled by the crew, at least the minimum stop time.</param>
/// <param name="CrewVarianceSeconds">Random difference from the nominal service time; zero-mean.</param>
/// <param name="FaultSeconds">Time added by a slow stop or an error.</param>
/// <param name="Fault">The kind of fault, or none.</param>
public sealed record PitStopTime(
    double LaneLossSeconds,
    double ServiceSeconds,
    double CrewVarianceSeconds,
    double FaultSeconds,
    PitStopFault Fault)
{
    /// <summary>Everything the stop costs against staying on the track.</summary>
    public double TotalSeconds => LaneLossSeconds + StationarySeconds;

    /// <summary>Time the car stands still, faults included.</summary>
    public double StationarySeconds => ServiceSeconds + CrewVarianceSeconds + FaultSeconds;
}

/// <summary>
/// The pit-stop time model: <c>StopTime = laneLoss + service(tyres, fuel, driver change) + crewVariance</c>, plus an
/// occasional slow stop or error. All numbers ESTIMATE (see <see cref="PitConstants"/>); the stationary tyre-change
/// and refuelling times are T30's (<see cref="FuelModel.TyreChangeSeconds"/>, <see cref="FuelModel.RefuelRateKgPerSecond"/>).
/// </summary>
/// <remarks>
/// The jobs run at the same time, so the service time is the slowest of them (as in <see cref="FuelModel.StationarySeconds"/>),
/// scaled by the crew: <c>(1 + CrewSpeedSpread * (50 - quality) / 50) * crewSizeFactor</c>. Variance is a normal draw cut at a few
/// standard deviations, scaled down by crew quality. A fault happens with a probability that falls with crew quality.
/// Randomness: <see cref="Sample"/> uses a generator derived from the <c>PitStops</c> race stream, the car and the stop number, and
/// always takes five draws, so no stop depends on another car, another stop or on whether a fault happened (INV-004).
/// </remarks>
public static class PitStopModel
{
    private const int DrawsPerStop = 5;

    /// <summary>The race-level <c>PitStops</c> stream: derived from the master seed, the season and the round.</summary>
    public static RngStream DeriveRaceStream(ulong masterSeed, int season, int round) =>
        RngStream.Derive(masterSeed, RngStreamName.PitStops, season, round);

    /// <summary>
    /// The nominal seconds the car stands still: the slowest job times the crew factor, never below the minimum stop time.
    /// Throws when the request is not allowed by the rules.
    /// </summary>
    /// <exception cref="InvalidOperationException">The request asks for a tyre change, fuel or a driver change the rules do not allow.</exception>
    public static double ServiceSeconds(PitRules rules, PitStopRequest request, PitCrew crew)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Validate(rules, request, crew);

        var jobs = 0.0;
        if (request.ChangeTyres)
        {
            jobs = Math.Max(jobs, FuelModel.TyreChangeSeconds(rules.Season));
        }

        if (request.RefuelKg > 0)
        {
            jobs = Math.Max(jobs, request.RefuelKg / FuelModel.RefuelRateKgPerSecond(rules.Season));
        }

        if (request.ChangeDriver)
        {
            jobs = Math.Max(jobs, PitConstants.DriverChangeSeconds);
        }

        return Math.Max(rules.MinimumStopSeconds, jobs * CrewSpeedFactor(rules, crew));
    }

    /// <summary>How much slower (above 1) or faster (below 1) than the standard crew a crew works.</summary>
    public static double CrewSpeedFactor(PitRules rules, PitCrew crew)
    {
        ArgumentNullException.ThrowIfNull(rules);
        CheckCrew(crew);
        var quality = 1 + (PitConstants.CrewSpeedSpread * (50 - crew.Quality) / 50);
        var size = crew.CrewSize is { } n
            ? Math.Clamp(
                Math.Pow((double)rules.StandardCrewSize / n, PitConstants.CrewSizeExponent),
                PitConstants.CrewSizeFactorMin,
                PitConstants.CrewSizeFactorMax)
            : 1.0;
        return quality * size;
    }

    /// <summary>Probability that a stop has a slow stop or an error, falling linearly with crew quality.</summary>
    public static double FaultProbability(double crewQuality)
    {
        CheckQuality(crewQuality);
        return Lerp(PitConstants.FaultProbabilityAtQuality0, PitConstants.FaultProbabilityAtQuality100, crewQuality / 100);
    }

    /// <summary>Mean seconds a fault adds, given there is one.</summary>
    public static double MeanFaultSeconds =>
        (PitConstants.ErrorShareOfFaults * (PitConstants.ErrorMinSeconds + PitConstants.ErrorMaxSeconds) / 2)
        + ((1 - PitConstants.ErrorShareOfFaults) * (PitConstants.SlowStopMinSeconds + PitConstants.SlowStopMaxSeconds) / 2);

    /// <summary>Standard deviation of the crew variance of a stop with the given nominal service time, seconds.</summary>
    public static double VarianceSd(double serviceSeconds, double crewQuality)
    {
        CheckQuality(crewQuality);
        var scale = Lerp(PitConstants.VarianceScaleAtQuality0, PitConstants.VarianceScaleAtQuality100, crewQuality / 100);
        return (PitConstants.VarianceBaseSdSeconds + (PitConstants.VarianceSdFraction * serviceSeconds)) * scale;
    }

    /// <summary>
    /// The expected total cost of a stop (lane loss plus service plus the mean fault time), without drawing anything.
    /// This is what a strategist may know in advance; it is exact in expectation of <see cref="Sample"/> except for the
    /// cut of the variance, which is symmetric.
    /// </summary>
    public static double ExpectedStopSeconds(PitRules rules, double laneLossSeconds, PitStopRequest request, PitCrew crew)
    {
        CheckLane(laneLossSeconds);
        return laneLossSeconds + ServiceSeconds(rules, request, crew) + (FaultProbability(crew.Quality) * MeanFaultSeconds);
    }

    /// <summary>
    /// Draws one stop. The draws come from a generator derived from <paramref name="raceStream"/> (not advanced), the car and
    /// the stop number, so a car's stops do not depend on any other car or on how many stops it made before.
    /// </summary>
    /// <param name="raceStream">The race's <c>PitStops</c> stream, see <see cref="DeriveRaceStream"/>.</param>
    /// <param name="carId">Stable id of the car.</param>
    /// <param name="stopNumber">1 for the car's first stop, 2 for its second, and so on.</param>
    /// <param name="rules">The pit rules of the season.</param>
    /// <param name="laneLossSeconds">Lane loss of the track, see <see cref="PitRules.PitLaneTimeLossSeconds"/>.</param>
    public static PitStopTime Sample(
        RngStream raceStream,
        string carId,
        int stopNumber,
        PitRules rules,
        double laneLossSeconds,
        PitStopRequest request,
        PitCrew crew)
    {
        ArgumentNullException.ThrowIfNull(raceStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(carId);
        if (stopNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(stopNumber), "Stops are numbered from 1.");
        }

        CheckLane(laneLossSeconds);
        var service = ServiceSeconds(rules, request, crew);

        var rng = raceStream.DeriveChild(string.Create(CultureInfo.InvariantCulture, $"car:{carId}:stop:{stopNumber}"));
        Span<double> u = stackalloc double[DrawsPerStop];
        for (var i = 0; i < u.Length; i++)
        {
            u[i] = rng.NextDouble();
        }

        var z = Math.Clamp(Gaussian.FromUniforms(u[0], u[1]), -PitConstants.VarianceCutSigmas, PitConstants.VarianceCutSigmas);
        var variance = z * VarianceSd(service, crew.Quality);
        var floor = Math.Max(rules.MinimumStopSeconds, PitConstants.MinServiceShare * service);
        variance = Math.Max(variance, floor - service);

        var fault = PitStopFault.None;
        var faultSeconds = 0.0;
        if (u[2] < FaultProbability(crew.Quality))
        {
            if (u[3] < PitConstants.ErrorShareOfFaults)
            {
                fault = PitStopFault.Error;
                faultSeconds = Lerp(PitConstants.ErrorMinSeconds, PitConstants.ErrorMaxSeconds, u[4]);
            }
            else
            {
                fault = PitStopFault.SlowStop;
                faultSeconds = Lerp(PitConstants.SlowStopMinSeconds, PitConstants.SlowStopMaxSeconds, u[4]);
            }
        }

        return new PitStopTime(laneLossSeconds, service, variance, faultSeconds, fault);
    }

    private static void Validate(PitRules rules, PitStopRequest request, PitCrew crew)
    {
        CheckCrew(crew);
        if (!(double.IsFinite(request.RefuelKg) && request.RefuelKg >= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Fuel cannot be negative.");
        }

        if (request.ChangeTyres && !rules.TyreChangeAllowed)
        {
            throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture, $"Tyre changes are not allowed in {rules.Season}."));
        }

        if (request.RefuelKg > 0 && !rules.RefuellingAllowed)
        {
            throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture, $"Refuelling is banned in {rules.Season}."));
        }

        if (request.ChangeDriver && !rules.DriverChangeAllowed)
        {
            throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture, $"Driver changes are not allowed in {rules.Season}."));
        }
    }

    private static void CheckCrew(PitCrew crew)
    {
        CheckQuality(crew.Quality);
        if (crew.CrewSize is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(crew), "A crew has at least one member.");
        }
    }

    private static void CheckQuality(double quality)
    {
        if (!(double.IsFinite(quality) && quality is >= 0 and <= 100))
        {
            throw new ArgumentOutOfRangeException(nameof(quality), "Crew quality must be in [0, 100].");
        }
    }

    private static void CheckLane(double laneLossSeconds)
    {
        if (!(double.IsFinite(laneLossSeconds) && laneLossSeconds >= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(laneLossSeconds), "The lane loss cannot be negative.");
        }
    }

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);
}

/// <summary>Normal draws from uniforms.</summary>
internal static class Gaussian
{
    /// <summary>Box-Muller from two uniforms in [0, 1); the first is shifted off zero so the log is finite.</summary>
    public static double FromUniforms(double u1, double u2) =>
        Math.Sqrt(-2.0 * Math.Log(1.0 - u1)) * Math.Cos(2.0 * Math.PI * u2);
}
