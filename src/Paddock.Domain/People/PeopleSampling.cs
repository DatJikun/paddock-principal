using System.Globalization;
using Paddock.Domain.Random;

namespace Paddock.Domain.People;

internal static class PeopleSampling
{
    public static void EnsureSeason(int season)
    {
        if (season < GenerationEstimates.MinSeason || season > GenerationEstimates.MaxSeason)
        {
            throw new ArgumentOutOfRangeException(nameof(season), season, "Season is outside the supported range.");
        }
    }

    public static void EnsurePeopleStream(RngStream people)
    {
        ArgumentNullException.ThrowIfNull(people);
        if (!string.Equals(people.Name, RngStreamName.People, StringComparison.Ordinal))
        {
            throw new ArgumentException("People generation uses only the People RNG stream.", nameof(people));
        }
    }

    public static Xoshiro256StarStar Child(RngStream people, string kind, string id)
    {
        EnsurePeopleStream(people);
        string version = GenerationEstimates.DrawSequenceVersion.ToString(CultureInfo.InvariantCulture);
        return people.DeriveChild(kind + ":v" + version + ":" + id);
    }

    public static string PickNationality(Xoshiro256StarStar rng, IReadOnlyList<NationalityWeight> weights)
    {
        int total = 0;
        foreach (NationalityWeight weight in weights)
        {
            total += weight.Weight;
        }

        int roll = rng.NextInt(0, total);
        int cursor = 0;
        foreach (NationalityWeight weight in weights)
        {
            cursor += weight.Weight;
            if (roll < cursor)
            {
                return weight.Code;
            }
        }

        throw new InvalidOperationException("Nationality weights did not cover the roll.");
    }

    public static bool PickFemale(Xoshiro256StarStar rng, int season)
    {
        return rng.NextDouble() < GenerationEstimates.FemaleShare(season);
    }

    public static int PickAge(Xoshiro256StarStar rng, int minInclusive, int maxExclusive)
    {
        return rng.NextInt(minInclusive, maxExclusive);
    }

    public static DateOnly PickBirthDate(Xoshiro256StarStar rng, int season, int age)
    {
        int year = season - age;
        int month = rng.NextInt(1, 13);
        int day = rng.NextInt(1, GenerationEstimates.BirthDayMaxExclusive);
        return new DateOnly(year, month, day);
    }

    public static PersonName PickName(
        Xoshiro256StarStar rng,
        INameSource names,
        INameBlocklist blocklist,
        string nationality,
        int birthYear,
        bool female)
    {
        for (var attempt = 0; attempt < GenerationEstimates.MaxNameAttempts; attempt++)
        {
            PersonName candidate = names.Pick(rng, nationality, birthYear, female);
            if (!blocklist.Blocks(candidate.Given, candidate.Family))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("The name source produced only blocked names.");
    }

    public static DriverAttributes PickAttributes(
        Xoshiro256StarStar rng,
        QualityBand quality,
        int eraYear,
        double sdFactor)
    {
        (double mean, double sd) = GenerationEstimates.AttributeDistribution(quality);
        sd *= sdFactor;
        return new DriverAttributes(
            Sample(rng, "cornering", mean, sd, eraYear),
            Sample(rng, "braking", mean, sd, eraYear),
            Sample(rng, "smoothness", mean, sd, eraYear),
            Sample(rng, "overtaking", mean, sd, eraYear),
            Sample(rng, "defending", mean, sd, eraYear),
            Sample(rng, "consistency", mean, sd, eraYear),
            Sample(rng, "composure", mean, sd, eraYear),
            Sample(rng, "adaptability", mean, sd, eraYear),
            Sample(rng, "wet_weather", mean, sd, eraYear),
            Sample(rng, "fitness", mean, sd, eraYear),
            Sample(rng, "feedback", mean, sd, eraYear));
    }

    public static DriverAttributes PickPotential(
        Xoshiro256StarStar rng,
        DriverAttributes current,
        QualityBand quality,
        int strength)
    {
        (int minExtra, int maxExtra) = GenerationEstimates.ScaledHeadroom(quality, strength);
        int span = maxExtra - minExtra + 1;
        return new DriverAttributes(
            Raise(rng, current.Cornering, minExtra, span),
            Raise(rng, current.Braking, minExtra, span),
            Raise(rng, current.Smoothness, minExtra, span),
            Raise(rng, current.Overtaking, minExtra, span),
            Raise(rng, current.Defending, minExtra, span),
            Raise(rng, current.Consistency, minExtra, span),
            Raise(rng, current.Composure, minExtra, span),
            Raise(rng, current.Adaptability, minExtra, span),
            Raise(rng, current.WetWeather, minExtra, span),
            Raise(rng, current.Fitness, minExtra, span),
            Raise(rng, current.Feedback, minExtra, span));
    }

    public static CareerCurve PickDriverCurve(Xoshiro256StarStar rng)
    {
        return PickCurve(
            rng,
            GenerationEstimates.DriverGrowthStartMin,
            GenerationEstimates.DriverGrowthStartMaxExclusive,
            GenerationEstimates.DriverYearsToPeakMin,
            GenerationEstimates.DriverYearsToPeakMaxExclusive,
            GenerationEstimates.DriverPlateauMin,
            GenerationEstimates.DriverPlateauMaxExclusive,
            GenerationEstimates.DriverDeclineMilliMin,
            GenerationEstimates.DriverDeclineMilliMaxExclusive);
    }

    public static CareerCurve PickStaffCurve(Xoshiro256StarStar rng)
    {
        return PickCurve(
            rng,
            GenerationEstimates.StaffGrowthStartMin,
            GenerationEstimates.StaffGrowthStartMaxExclusive,
            GenerationEstimates.StaffYearsToPeakMin,
            GenerationEstimates.StaffYearsToPeakMaxExclusive,
            GenerationEstimates.StaffPlateauMin,
            GenerationEstimates.StaffPlateauMaxExclusive,
            GenerationEstimates.StaffDeclineMilliMin,
            GenerationEstimates.StaffDeclineMilliMaxExclusive);
    }

    public static PersonalityTraits PickPersonality(Xoshiro256StarStar rng)
    {
        int primary = rng.NextInt(0, 8);
        return new PersonalityTraits(
            (PrimaryPersonality)primary,
            NormalInt(rng, GenerationEstimates.PersonalityMean, GenerationEstimates.PersonalitySd),
            NormalInt(rng, GenerationEstimates.PersonalityMean, GenerationEstimates.PersonalitySd),
            NormalInt(rng, GenerationEstimates.PersonalityMean, GenerationEstimates.PersonalitySd),
            NormalInt(rng, GenerationEstimates.PersonalityMean, GenerationEstimates.PersonalitySd),
            NormalInt(rng, GenerationEstimates.PersonalityMean, GenerationEstimates.PersonalitySd));
    }

    public static int PickStaffAttribute(Xoshiro256StarStar rng, QualityBand quality)
    {
        (double mean, double sd) = GenerationEstimates.AttributeDistribution(quality);
        return NormalInt(rng, mean, sd);
    }

    public static int PickHiddenTrait(Xoshiro256StarStar rng)
    {
        return NormalInt(rng, GenerationEstimates.PersonalityMean, GenerationEstimates.PersonalitySd);
    }

    private static CareerCurve PickCurve(
        Xoshiro256StarStar rng,
        int growthMin,
        int growthMaxExclusive,
        int yearsMin,
        int yearsMaxExclusive,
        int plateauMin,
        int plateauMaxExclusive,
        int declineMin,
        int declineMaxExclusive)
    {
        int growth = rng.NextInt(growthMin, growthMaxExclusive);
        int peak = growth + rng.NextInt(yearsMin, yearsMaxExclusive);
        int plateau = rng.NextInt(plateauMin, plateauMaxExclusive);
        int decline = rng.NextInt(declineMin, declineMaxExclusive);
        return new CareerCurve(growth, peak, plateau, decline);
    }

    private static int Sample(Xoshiro256StarStar rng, string key, double mean, double sd, int eraYear)
    {
        return NormalInt(rng, mean + GenerationEstimates.EraMeanAdjustment(key, eraYear), sd);
    }

    private static int Raise(Xoshiro256StarStar rng, int current, int minExtra, int span)
    {
        int roll = rng.NextInt(0, span);
        int extra = minExtra + roll;
        int raised = current + extra;
        if (raised > GenerationEstimates.AttributeMax)
        {
            return GenerationEstimates.AttributeMax;
        }

        return raised;
    }

    private static int NormalInt(Xoshiro256StarStar rng, double mean, double sd)
    {
        double value = mean + StandardNormal(rng) * sd;
        if (value < GenerationEstimates.AttributeMin)
        {
            return GenerationEstimates.AttributeMin;
        }

        if (value > GenerationEstimates.AttributeMax)
        {
            return GenerationEstimates.AttributeMax;
        }

        return (int)Math.Floor(value + 0.5);
    }

    /// <summary>
    /// Irwin–Hall of <see cref="GenerationEstimates.NormalSampleCount"/> uniforms, shifted to mean 0.
    /// Integer and double arithmetic only, so the draw stays the same on both CI operating systems.
    /// </summary>
    private static double StandardNormal(Xoshiro256StarStar rng)
    {
        double sum = 0;
        for (var i = 0; i < GenerationEstimates.NormalSampleCount; i++)
        {
            sum += rng.NextDouble();
        }

        return sum - (GenerationEstimates.NormalSampleCount / 2.0);
    }
}
