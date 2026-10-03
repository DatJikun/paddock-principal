namespace Paddock.Simulation.Racing.Points;

/// <summary>
/// One car's raw result. A list of these, in the order the cars finished (laps completed, then time), is what
/// <see cref="RaceClassifier"/> turns into a classification.
/// </summary>
/// <param name="DriverId">The driver the car is entered for (the "primary" driver, normally the starter).</param>
/// <param name="ConstructorId">The constructor that entered the car.</param>
/// <param name="Status">How the race ended for the car.</param>
/// <param name="LapsCompleted">Laps the car completed (not negative).</param>
/// <param name="SharedDrivePartnerIds">
/// The other drivers who drove this same car. Partners do not appear as entries of their own.
/// Empty for a car driven by one person.
/// </param>
/// <param name="SetFastestLap">True when this car set the fastest race lap. Several cars may carry the flag when the lap time is tied.</param>
public sealed record RaceResultInput(
    string DriverId,
    string ConstructorId,
    FinishStatus Status,
    int LapsCompleted,
    IReadOnlyList<string> SharedDrivePartnerIds,
    bool SetFastestLap)
{
    public RaceResultInput(string driverId, string constructorId, FinishStatus status, int lapsCompleted)
        : this(driverId, constructorId, status, lapsCompleted, [], false)
    {
    }
}

/// <summary>Facts about the race that are not part of any car's result.</summary>
/// <param name="ScheduledLaps">Laps the race was scheduled for. Needed for the half-distance fastest-lap condition.</param>
/// <param name="IsFinalRound">True for the last round of the season. Only matters when the double-points finale is in force.</param>
public readonly record struct RaceContext(int ScheduledLaps, bool IsFinalRound = false);
