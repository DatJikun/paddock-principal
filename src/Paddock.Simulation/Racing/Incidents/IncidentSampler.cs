using Paddock.Domain.Random;

namespace Paddock.Simulation.Racing.Incidents;

/// <summary>A car and the two driver traits the incident model reads.</summary>
/// <param name="CarId">Stable id of the car; selects the car's sub-stream.</param>
/// <param name="Aggression">The driver's aggression, 0 to 100. Raises the chance of an incident.</param>
/// <param name="Composure">The driver's composure, 0 to 100. Lowers the chance of an incident.</param>
public sealed record IncidentCar(string CarId, double Aggression, double Composure);

/// <summary>What the race looks like around one car on one lap.</summary>
/// <param name="Lap">The lap, 1-based.</param>
/// <param name="Wetness">Track wetness in [0, 1] (the T32 weather model's scale).</param>
/// <param name="NearbyCarIds">Ids of the other cars within one second of the car, the car itself excluded. Their number is the contact density; the order does not matter.</param>
public sealed record LapContext(int Lap, double Wetness, IReadOnlyList<string> NearbyCarIds);

/// <summary>What is fixed for the whole race.</summary>
/// <param name="Era">The era's safety profile.</param>
/// <param name="TrackDanger">The circuit's danger factor; 1 is a typical circuit. Within <see cref="IncidentConstants.MinTrackDanger"/> and <see cref="IncidentConstants.MaxTrackDanger"/>.</param>
/// <param name="FatalitiesEnabled">The career's fatality option (PP-006; <c>CareerConfig.FatalityLevel == On</c>). When false, a fatal-class event becomes a career-ending injury.</param>
public sealed record IncidentRaceContext(EraSafetyProfile Era, double TrackDanger, bool FatalitiesEnabled);

/// <summary>
/// Samples the incidents of a race from the <c>Incidents</c> RNG stream, one car and one lap at a time.
/// </summary>
/// <remarks>
/// Method: the probability of an incident for a car on a lap is the product of an era rate, the driver's aggression and
/// composure, the number of cars within one second, track wetness, the first-lap factor and the track danger
/// (<see cref="IncidentConstants"/>, all ESTIMATE). If it happens, the kind, the car's outcome and, for a two-car incident,
/// the other car and its outcome follow from further draws. An outcome is none, minor, retire, injury (light, serious,
/// career-ending) or fatal; the era band scales how often a hard crash hurts the driver and how bad the injury is.
/// Determinism: the draws come from a sub-stream derived from the race stream, the car id and the lap, and exactly
/// <see cref="DrawsPerCarLap"/> numbers are taken every time, whatever the branch. So the draws of one car never depend on
/// the other cars, on the number of cars or on other laps, and the fatality option changes only the mapping of the
/// fatal-class outcome, never a draw.
/// Fatal outcomes can only come out when <see cref="IncidentRaceContext.FatalitiesEnabled"/> is true.
/// Queries are pure and use no randomness except through the stream passed in, which is never advanced (INV-005).
/// </remarks>
public static class IncidentSampler
{
    /// <summary>How many numbers are drawn for every car and lap.</summary>
    public const int DrawsPerCarLap = 9;

    /// <summary>The race-level <c>Incidents</c> stream: derived from the master seed, the season and the round.</summary>
    public static RngStream DeriveRaceStream(ulong masterSeed, int season, int round) =>
        RngStream.Derive(masterSeed, RngStreamName.Incidents, season, round);

    /// <summary>
    /// The probability that the car has an incident on the lap. Pure; uses no randomness.
    /// </summary>
    public static double OccurrenceProbability(IncidentCar car, LapContext lap, IncidentRaceContext race)
    {
        Validate(car, lap, race);
        return Occurrence(car, lap, race);
    }

    /// <summary>
    /// Samples whether the car has an incident on the lap and, if so, what it is.
    /// </summary>
    /// <param name="raceStream">The race's <c>Incidents</c> stream, see <see cref="DeriveRaceStream"/>. It is not advanced.</param>
    /// <param name="car">The car and its driver's traits.</param>
    /// <param name="lap">The lap and what is around the car.</param>
    /// <param name="race">The era, the track and the fatality option.</param>
    /// <returns>The incident, or null when nothing happens to the car on this lap.</returns>
    public static IncidentResult? SampleLap(RngStream raceStream, IncidentCar car, LapContext lap, IncidentRaceContext race)
    {
        ArgumentNullException.ThrowIfNull(raceStream);
        Validate(car, lap, race);

        var rng = raceStream.DeriveChild("car:" + car.CarId + ":lap:" + lap.Lap);
        var draws = new double[DrawsPerCarLap];
        for (var i = 0; i < draws.Length; i++)
        {
            draws[i] = rng.NextDouble();
        }

        if (draws[0] >= Occurrence(car, lap, race))
        {
            return null;
        }

        var nearby = lap.NearbyCarIds.Count;
        var kind = PickKind(draws[1], nearby, lap);
        var instigator = new IncidentParticipant(
            car.CarId,
            IncidentRole.Instigator,
            ResolveOutcome(kind, race, draws[2], draws[3], draws[4]));

        IncidentParticipant? other = null;
        if (kind is IncidentKind.Contact or IncidentKind.Collision)
        {
            // Never empty: the weights of these kinds are zero with nobody near. Sorted so the pick does not depend on the caller's order.
            var sorted = lap.NearbyCarIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var otherId = sorted[Math.Min((int)(draws[5] * sorted.Length), sorted.Length - 1)];
            other = new IncidentParticipant(
                otherId,
                IncidentRole.Other,
                ResolveOutcome(kind, race, draws[6], draws[7], draws[8]));
        }

        return new IncidentResult(lap.Lap, kind, instigator, other);
    }

    private static double Occurrence(IncidentCar car, LapContext lap, IncidentRaceContext race)
    {
        var driver = Math.Exp(IncidentConstants.AggressionExponent * (car.Aggression - 50) / 50)
            * Math.Exp(-IncidentConstants.ComposureExponent * (car.Composure - 50) / 50);
        var contact = 1 + IncidentConstants.ContactDensityWeight * Math.Min(lap.NearbyCarIds.Count, IncidentConstants.MaxContactCars);
        var wet = 1 + IncidentConstants.WetnessWeight * lap.Wetness;
        var probability = IncidentConstants.BaseIncidentRatePerCarLap
            * IncidentConstants.EraRateMultiplier(race.Era.Risk)
            * driver
            * contact
            * wet
            * LapFactor(lap.Lap)
            * race.TrackDanger;
        return Math.Min(probability, IncidentConstants.MaxLapProbability);
    }

    /// <summary>The first-lap chaos factor and its fade on the second lap. ESTIMATE.</summary>
    public static double LapFactor(int lap) => lap switch
    {
        1 => IncidentConstants.FirstLapFactor,
        2 => IncidentConstants.SecondLapFactor,
        _ => 1.0,
    };

    private static IncidentKind PickKind(double draw, int nearby, LapContext lap)
    {
        var kinds = Enum.GetValues<IncidentKind>();
        Span<double> weights = stackalloc double[5];
        var total = 0.0;
        for (var i = 0; i < kinds.Length; i++)
        {
            var kind = kinds[i];
            var weight = IncidentConstants.KindWeight(kind);
            switch (kind)
            {
                case IncidentKind.Spin or IncidentKind.Barrier:
                    weight *= 1 + lap.Wetness * IncidentConstants.WetKindBoost;
                    break;
                case IncidentKind.Contact or IncidentKind.Collision:
                    weight *= Math.Min(nearby, IncidentConstants.MaxContactCars);
                    if (lap.Lap == 1)
                    {
                        weight *= IncidentConstants.FirstLapContactBias;
                    }

                    break;
            }

            weights[i] = weight;
            total += weight;
        }

        var target = draw * total;
        var cumulative = 0.0;
        for (var i = 0; i < kinds.Length; i++)
        {
            cumulative += weights[i];
            if (weights[i] > 0 && target < cumulative)
            {
                return kinds[i];
            }
        }

        // Only reachable through rounding at the very top of the range: the last kind with a positive weight.
        for (var i = kinds.Length - 1; i >= 0; i--)
        {
            if (weights[i] > 0)
            {
                return kinds[i];
            }
        }

        throw new InvalidOperationException("No incident kind has a positive weight.");
    }

    private static IncidentOutcome ResolveOutcome(
        IncidentKind kind,
        IncidentRaceContext race,
        double severityDraw,
        double injuryDraw,
        double gradeDraw)
    {
        var hard = IncidentConstants.HardCrashShare(kind);
        if (severityDraw >= hard)
        {
            return severityDraw < hard + IncidentConstants.MinorShare(kind) ? IncidentOutcome.Minor : IncidentOutcome.None;
        }

        var band = race.Era.Risk;
        var injuryShare = Math.Min(1.0, IncidentConstants.InjuryShareOfHardCrash(band) * IncidentConstants.InjuryKindFactor(kind));
        if (injuryDraw >= injuryShare)
        {
            return IncidentOutcome.Retire;
        }

        var split = IncidentConstants.InjurySplit(band);
        if (gradeDraw < split.Light)
        {
            return IncidentOutcome.Injured(InjuryGrade.Light);
        }

        if (gradeDraw < split.Light + split.Serious)
        {
            return IncidentOutcome.Injured(InjuryGrade.Serious);
        }

        if (gradeDraw < split.Light + split.Serious + split.CareerEnding)
        {
            return IncidentOutcome.Injured(InjuryGrade.CareerEnding);
        }

        // The fatal class (PP-006): a death only when the option is on, otherwise the end of a career.
        return race.FatalitiesEnabled ? IncidentOutcome.Fatal : IncidentOutcome.Injured(InjuryGrade.CareerEnding);
    }

    private static void Validate(IncidentCar car, LapContext lap, IncidentRaceContext race)
    {
        ArgumentNullException.ThrowIfNull(car);
        ArgumentNullException.ThrowIfNull(lap);
        ArgumentNullException.ThrowIfNull(race);
        ArgumentNullException.ThrowIfNull(race.Era);
        ArgumentException.ThrowIfNullOrWhiteSpace(car.CarId);
        ArgumentNullException.ThrowIfNull(lap.NearbyCarIds);

        if (!InRange(car.Aggression, 0, 100) || !InRange(car.Composure, 0, 100))
        {
            throw new ArgumentOutOfRangeException(nameof(car), "Aggression and composure must be in [0, 100].");
        }

        if (lap.Lap < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(lap), "Laps are numbered from 1.");
        }

        if (!InRange(lap.Wetness, 0, 1))
        {
            throw new ArgumentOutOfRangeException(nameof(lap), "Wetness must be in [0, 1].");
        }

        if (!InRange(race.TrackDanger, IncidentConstants.MinTrackDanger, IncidentConstants.MaxTrackDanger))
        {
            throw new ArgumentOutOfRangeException(nameof(race), "Track danger is outside the allowed range.");
        }

        if (!Enum.IsDefined(race.Era.Risk))
        {
            throw new ArgumentOutOfRangeException(nameof(race), "Unknown fatality-risk band.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in lap.NearbyCarIds)
        {
            if (string.IsNullOrWhiteSpace(id) || string.Equals(id, car.CarId, StringComparison.Ordinal) || !seen.Add(id))
            {
                throw new ArgumentException("Nearby cars must be distinct, non-empty ids and not the car itself.", nameof(lap));
            }
        }
    }

    private static bool InRange(double value, double min, double max) => value >= min && value <= max;
}
