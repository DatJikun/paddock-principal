using Paddock.Domain.Random;
using Paddock.Domain.World;

namespace Paddock.Domain.Cars;

public enum BrakingStyle
{
    Early = 0,
    Normal = 1,
    Late = 2,
}

/// <summary>How the driver wants the car (DESIGN §6.1). Balance and traction are −1..1.</summary>
public readonly record struct DriverHandlingPreferences
{
    public DriverHandlingPreferences(double balance, double traction, BrakingStyle braking)
    {
        if (!CarConcept.InRange(balance))
        {
            throw new ArgumentOutOfRangeException(nameof(balance));
        }

        if (!CarConcept.InRange(traction))
        {
            throw new ArgumentOutOfRangeException(nameof(traction));
        }

        if (!Enum.IsDefined(braking))
        {
            throw new ArgumentOutOfRangeException(nameof(braking));
        }

        Balance = CarEstimates.Quantize(balance);
        Traction = CarEstimates.Quantize(traction);
        Braking = braking;
    }

    public double Balance { get; }

    public double Traction { get; }

    public BrakingStyle Braking { get; }
}

/// <summary>One track the driver already knows. The id is the layout id, not a display name.</summary>
public readonly record struct TrackLaps
{
    public TrackLaps(string trackId, int laps)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
        ArgumentOutOfRangeException.ThrowIfNegative(laps);
        TrackId = trackId;
        Laps = laps;
    }

    public string TrackId { get; }

    public int Laps { get; }
}

/// <summary>Experience counters (DESIGN §6.1). They are not attributes.</summary>
public sealed record DriverExperience
{
    public DriverExperience(int starts, int wetRaces, int seasonsWithTeam, IReadOnlyList<TrackLaps> lapsByTrack)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(starts);
        ArgumentOutOfRangeException.ThrowIfNegative(wetRaces);
        ArgumentOutOfRangeException.ThrowIfNegative(seasonsWithTeam);
        ArgumentNullException.ThrowIfNull(lapsByTrack);
        if (wetRaces > starts)
        {
            throw new ArgumentOutOfRangeException(nameof(wetRaces), "Wet races cannot outnumber starts.");
        }

        var ordered = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in lapsByTrack)
        {
            if (!ordered.TryAdd(row.TrackId, row.Laps))
            {
                throw new ArgumentException("A track is listed twice.", nameof(lapsByTrack));
            }
        }

        Starts = starts;
        WetRaces = wetRaces;
        SeasonsWithTeam = seasonsWithTeam;
        LapsByTrack = ordered.Select(pair => new TrackLaps(pair.Key, pair.Value)).ToArray();
    }

    public int Starts { get; }

    public int WetRaces { get; }

    public int SeasonsWithTeam { get; }

    public IReadOnlyList<TrackLaps> LapsByTrack { get; }
}

/// <summary>Preferences and experience of one person. Stored beside the person, not inside the generator's output.</summary>
public sealed record DriverFitProfile
{
    public DriverFitProfile(PersonId person, DriverHandlingPreferences preferences, DriverExperience experience)
    {
        if (!person.IsAssigned)
        {
            throw new ArgumentException("Person id is unassigned.", nameof(person));
        }

        ArgumentNullException.ThrowIfNull(experience);
        Person = person;
        Preferences = preferences;
        Experience = experience;
    }

    public PersonId Person { get; }

    public DriverHandlingPreferences Preferences { get; }

    public DriverExperience Experience { get; }
}

/// <summary>Pace modifier in seconds (positive is faster) and a confidence change. ESTIMATE.</summary>
public readonly record struct DriverCarFitResult(double PaceModifierSeconds, double ConfidenceDelta);

/// <summary>Rolls a driver's fit from a <c>People</c> child. Does not change the generator's person types.</summary>
public static class DriverFitFactory
{
    public static DriverFitProfile Roll(PersonId person, Xoshiro256StarStar rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var braking = (BrakingStyle)rng.NextInt(0, 3);

        var starts = (int)(rng.NextDouble() * 30d);
        var wet = starts == 0 ? 0 : (int)(rng.NextDouble() * (starts + 1));
        var seasons = (int)(rng.NextDouble() * 6d);
        var preferences = new DriverHandlingPreferences((rng.NextDouble() * 2d) - 1d, (rng.NextDouble() * 2d) - 1d, braking);
        return new DriverFitProfile(person, preferences, new DriverExperience(starts, wet, seasons, []));
    }
}

/// <summary>How well this driver suits this car (DESIGN §5.2). Pure, no RNG (INV-005).</summary>
public static class DriverCarFit
{
    public static DriverCarFitResult Evaluate(DriverFitProfile driver, TeamCar car)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(car);
        var carBalance = car.Concept.Aero * 0.5d;
        var carTraction = -car.Concept.Window * 0.5d;
        var carBraking = car.Concept.Window * 0.5d;
        var driverBraking = driver.Preferences.Braking switch
        {
            BrakingStyle.Early => -1d,
            BrakingStyle.Late => 1d,
            _ => 0d,
        };
        var mismatch = Math.Abs(driver.Preferences.Balance - carBalance)
            + Math.Abs(driver.Preferences.Traction - carTraction)
            + Math.Abs(driverBraking - carBraking);
        return new DriverCarFitResult(
            CarEstimates.Quantize(-CarEstimates.PaceSecondsPerMismatch * mismatch),
            CarEstimates.Quantize(CarEstimates.ConfidenceAtMatch - (CarEstimates.ConfidencePerMismatch * mismatch)));
    }
}
