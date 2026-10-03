namespace Paddock.Simulation.Racing.Qualifying;

/// <summary>
/// One car in qualifying.
/// </summary>
/// <param name="DriverId">Stable ID of the driver. Must be unique within one qualifying run.</param>
/// <param name="ConstructorId">Stable ID of the constructor that entered the car.</param>
/// <param name="PaceSeconds">
/// Pace in seconds per lap relative to the track base (see <see cref="QualifyingContext.BaseLapSeconds"/>):
/// lower is faster, negative is quicker than the base. The lap-time model (T29) supplies it. This is the noise-free
/// "ideal" lap before track evolution, wetness and lap-to-lap loss.
/// </param>
/// <param name="Consistency">0..1, composure over a lap. 1 loses very little to mistakes, 0 loses a lot.</param>
/// <param name="CarNotRunning">
/// Reliability flag: the car cannot take part (it never leaves the garage). It sets no time, is not part of any
/// session and cannot qualify. Other cars' random draws do not change because of this flag.
/// </param>
/// <param name="InPreQualifying">The car has to go through pre-qualifying when the rules have one. Ignored otherwise.</param>
/// <param name="StartsFromPitLane">
/// The car qualified for the grid on pace but has to start from the pit lane (for example after a car change).
/// It still takes up one of the places admitted to the race.
/// </param>
public sealed record QualifyingEntrant(
    string DriverId,
    string ConstructorId,
    double PaceSeconds,
    double Consistency,
    bool CarNotRunning = false,
    bool InPreQualifying = false,
    bool StartsFromPitLane = false);

/// <summary>Facts about the weekend that are not part of any car.</summary>
/// <param name="Seed">Career master seed. The <c>LapNoise</c> stream is derived from it, the season and the round.</param>
/// <param name="Season">Season year.</param>
/// <param name="Round">Round number within the season (part of the RNG stream key).</param>
/// <param name="BaseLapSeconds">The track base lap time that every entrant's pace is relative to. Must be positive.</param>
/// <param name="WetnessLapMultiplier">
/// Optional wetness effect: the base-plus-pace lap is multiplied by this (1 = dry, the default). The weather model
/// (T32) decides the value; qualifying only applies it. Must be at least 1.
/// </param>
/// <param name="DeclaredWet">
/// The session was declared wet. Only the "unless wet" cutoff rule looks at it (a wet session is not subject to the
/// 107 percent cutoff).
/// </param>
public sealed record QualifyingContext(
    ulong Seed,
    int Season,
    int Round,
    double BaseLapSeconds,
    double WetnessLapMultiplier = 1.0,
    bool DeclaredWet = false);
