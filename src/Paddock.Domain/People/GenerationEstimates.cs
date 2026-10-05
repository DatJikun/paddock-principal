namespace Paddock.Domain.People;

/// <summary>
/// Uncalibrated balance estimates for fictional people. Not measured facts.
/// Generators read every distribution, weight, age band, and threshold from here
/// so the numbers can be edited without hunting through call sites.
/// </summary>
public static class GenerationEstimates
{
    public const int DrawSequenceVersion = 1;

    public const int AttributeMin = 1;
    public const int AttributeMax = 20;
    public const int OverallMin = 1;
    public const int OverallMax = 100;
    public const int WeightTotal = 1000;

    public const int MinSeason = 1950;
    public const int MaxSeason = 9999;
    public const int MinBirthYear = 1800;
    public const int MaxBirthYear = 9999;

    public const int StarPotential = 85;

    public const int StrengthMin = 0;
    public const int StrengthMax = 100;

    /// <summary>Share of the band's standard deviation kept when strength is 0.</summary>
    public const double StrengthFloorSdFactor = 0.15;

    public const int NormalSampleCount = 12;
    public const int MaxNameAttempts = 8;
    public const int BirthDayMaxExclusive = 29;

    public const int EarlyEraLastYear = 1960;
    public const int ModernEraFirstYear = 1994;

    public const double EarlySmoothnessMeanBonus = 1.0;
    public const double EarlyFitnessMeanBonus = 1.5;
    public const double ClassicBrakingMeanBonus = 0.5;
    public const double ModernFeedbackMeanBonus = 1.0;
    public const double ModernFitnessMeanPenalty = -0.5;

    public const double FillerMean = 8.0;
    public const double FillerSd = 2.0;
    public const double SolidMean = 11.0;
    public const double SolidSd = 1.8;
    public const double ContenderMean = 14.5;
    public const double ContenderSd = 1.5;
    public const double FutureStarMean = 12.0;
    public const double FutureStarSd = 1.8;

    public const int FillerHeadroomMin = 0;
    public const int FillerHeadroomMax = 3;
    public const int SolidHeadroomMin = 0;
    public const int SolidHeadroomMax = 5;
    public const int ContenderHeadroomMin = 1;
    public const int ContenderHeadroomMax = 5;
    public const int FutureStarHeadroomMin = 4;
    public const int FutureStarHeadroomMax = 8;

    public const int FillerAgeMin = 23;
    public const int FillerAgeMaxExclusive = 36;
    public const int SolidAgeMin = 21;
    public const int SolidAgeMaxExclusive = 33;
    public const int ContenderAgeMin = 20;
    public const int ContenderAgeMaxExclusive = 31;
    public const int FutureStarAgeMin = 17;
    public const int FutureStarAgeMaxExclusive = 23;
    public const int StaffAgeMin = 28;
    public const int StaffAgeMaxExclusive = 61;

    public const int DriverGrowthStartMin = 16;
    public const int DriverGrowthStartMaxExclusive = 22;
    public const int DriverYearsToPeakMin = 4;
    public const int DriverYearsToPeakMaxExclusive = 13;
    public const int DriverPlateauMin = 0;
    public const int DriverPlateauMaxExclusive = 9;
    public const int DriverDeclineMilliMin = 20;
    public const int DriverDeclineMilliMaxExclusive = 81;

    public const int StaffGrowthStartMin = 22;
    public const int StaffGrowthStartMaxExclusive = 31;
    public const int StaffYearsToPeakMin = 8;
    public const int StaffYearsToPeakMaxExclusive = 21;
    public const int StaffPlateauMin = 2;
    public const int StaffPlateauMaxExclusive = 13;
    public const int StaffDeclineMilliMin = 10;
    public const int StaffDeclineMilliMaxExclusive = 51;

    public const double PersonalityMean = 10.0;
    public const double PersonalitySd = 3.0;

    public const double FemaleShareBefore1970 = 0.01;
    public const double FemaleShareBefore2000 = 0.02;
    public const double FemaleShareBefore2020 = 0.05;
    public const double FemaleShareFrom2020 = 0.08;
    public const int FemaleShareYear1970 = 1970;
    public const int FemaleShareYear2000 = 2000;
    public const int FemaleShareYear2020 = 2020;

    public const int AeroAvailableFrom = 1968;
    public const int CommercialAvailableFrom = 1968;
    public const int RaceEngineerAvailableFrom = 1970;
    public const int StrategistAvailableFrom = 1994;

    public const QualityBand ReviewCommandQuality = QualityBand.Filler;
    public const int ReviewNationalityWeight = 1;

    public const double FillerSampleMeanLower = 7.2;
    public const double FillerSampleMeanUpper = 8.8;
    public const double FillerStarShareUpper = 0.02;
    public const double FutureStarSampleMeanLower = 10.8;
    public const double FutureStarSampleMeanUpper = 13.2;
    public const double FutureStarShareLower = 0.05;
    public const double FutureStarShareUpper = 0.99;

    public static readonly string[] DriverAttributeKeys =
    [
        "cornering",
        "braking",
        "smoothness",
        "overtaking",
        "defending",
        "consistency",
        "composure",
        "adaptability",
        "wet_weather",
        "fitness",
        "feedback",
    ];

    // ESTIMATE (PP-057): before 1960 smoothness is 12% of the overall (120 of 1000) and cornering takes the 20 points.
    private static readonly AttributeWeight[] EarlyWeights =
    [
        new("cornering", 180),
        new("braking", 90),
        new("smoothness", 120),
        new("overtaking", 60),
        new("defending", 60),
        new("consistency", 100),
        new("composure", 90),
        new("adaptability", 60),
        new("wet_weather", 60),
        new("fitness", 120),
        new("feedback", 60),
    ];

    private static readonly AttributeWeight[] ClassicWeights =
    [
        new("cornering", 180),
        new("braking", 110),
        new("smoothness", 90),
        new("overtaking", 70),
        new("defending", 70),
        new("consistency", 120),
        new("composure", 100),
        new("adaptability", 70),
        new("wet_weather", 60),
        new("fitness", 70),
        new("feedback", 60),
    ];

    private static readonly AttributeWeight[] ModernWeights =
    [
        new("cornering", 170),
        new("braking", 100),
        new("smoothness", 60),
        new("overtaking", 80),
        new("defending", 70),
        new("consistency", 130),
        new("composure", 110),
        new("adaptability", 80),
        new("wet_weather", 50),
        new("fitness", 50),
        new("feedback", 100),
    ];

    public static (double Mean, double Sd) AttributeDistribution(QualityBand band)
    {
        EnsureBand(band);
        return band switch
        {
            QualityBand.Filler => (FillerMean, FillerSd),
            QualityBand.Solid => (SolidMean, SolidSd),
            QualityBand.Contender => (ContenderMean, ContenderSd),
            QualityBand.FutureStar => (FutureStarMean, FutureStarSd),
            _ => throw new ArgumentOutOfRangeException(nameof(band), band, "Unknown quality band."),
        };
    }

    public static (int MinExtra, int MaxExtraInclusive) Headroom(QualityBand band)
    {
        EnsureBand(band);
        return band switch
        {
            QualityBand.Filler => (FillerHeadroomMin, FillerHeadroomMax),
            QualityBand.Solid => (SolidHeadroomMin, SolidHeadroomMax),
            QualityBand.Contender => (ContenderHeadroomMin, ContenderHeadroomMax),
            QualityBand.FutureStar => (FutureStarHeadroomMin, FutureStarHeadroomMax),
            _ => throw new ArgumentOutOfRangeException(nameof(band), band, "Unknown quality band."),
        };
    }

    public static (int MinInclusive, int MaxExclusive) AgeRange(QualityBand band)
    {
        EnsureBand(band);
        return band switch
        {
            QualityBand.Filler => (FillerAgeMin, FillerAgeMaxExclusive),
            QualityBand.Solid => (SolidAgeMin, SolidAgeMaxExclusive),
            QualityBand.Contender => (ContenderAgeMin, ContenderAgeMaxExclusive),
            QualityBand.FutureStar => (FutureStarAgeMin, FutureStarAgeMaxExclusive),
            _ => throw new ArgumentOutOfRangeException(nameof(band), band, "Unknown quality band."),
        };
    }

    public static double FemaleShare(int season)
    {
        if (season < FemaleShareYear1970)
        {
            return FemaleShareBefore1970;
        }

        if (season < FemaleShareYear2000)
        {
            return FemaleShareBefore2000;
        }

        if (season < FemaleShareYear2020)
        {
            return FemaleShareBefore2020;
        }

        return FemaleShareFrom2020;
    }

    public static double StrengthSdFactor(int strength)
    {
        if (strength < StrengthMin || strength > StrengthMax)
        {
            throw new ArgumentOutOfRangeException(nameof(strength), strength, "Strength must be from 0 to 100.");
        }

        return StrengthFloorSdFactor
            + (1.0 - StrengthFloorSdFactor) * (strength / (double)StrengthMax);
    }

    public static double EraMeanAdjustment(string attributeKey, int eraYear)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeKey);
        if (eraYear <= EarlyEraLastYear)
        {
            if (attributeKey == "smoothness")
            {
                return EarlySmoothnessMeanBonus;
            }

            if (attributeKey == "fitness")
            {
                return EarlyFitnessMeanBonus;
            }
        }
        else if (eraYear < ModernEraFirstYear)
        {
            if (attributeKey == "braking")
            {
                return ClassicBrakingMeanBonus;
            }
        }
        else
        {
            if (attributeKey == "feedback")
            {
                return ModernFeedbackMeanBonus;
            }

            if (attributeKey == "fitness")
            {
                return ModernFitnessMeanPenalty;
            }
        }

        return 0;
    }

    public static AttributeWeight[] OverallWeights(int eraYear)
    {
        AttributeWeight[] source = eraYear <= EarlyEraLastYear
            ? EarlyWeights
            : eraYear < ModernEraFirstYear ? ClassicWeights : ModernWeights;
        return (AttributeWeight[])source.Clone();
    }

    public static int Overall(DriverAttributes attributes, int eraYear)
    {
        attributes.EnsureValid();
        AttributeWeight[] weights = OverallWeights(eraYear);
        long weighted = 0;
        long total = 0;
        foreach (AttributeWeight weight in weights)
        {
            weighted += (long)attributes.Get(weight.Key) * weight.Weight;
            total += weight.Weight;
        }

        if (total <= 0)
        {
            throw new InvalidOperationException("Era weights must sum to a positive total.");
        }

        long denominator = 20L * total;
        int overall = (int)((weighted * 100L + denominator / 2L) / denominator);
        if (overall < OverallMin)
        {
            return OverallMin;
        }

        if (overall > OverallMax)
        {
            return OverallMax;
        }

        return overall;
    }

    public static (int MinExtra, int MaxExtraInclusive) ScaledHeadroom(QualityBand band, int strength)
    {
        (int minExtra, int maxExtra) = Headroom(band);
        if (strength < StrengthMin || strength > StrengthMax)
        {
            throw new ArgumentOutOfRangeException(nameof(strength), strength, "Strength must be from 0 to 100.");
        }

        if (strength == StrengthMax)
        {
            return (minExtra, maxExtra);
        }

        int scaledMin = minExtra * strength / StrengthMax;
        int scaledMax = maxExtra * strength / StrengthMax;
        if (scaledMax < scaledMin)
        {
            scaledMax = scaledMin;
        }

        return (scaledMin, scaledMax);
    }

    private static void EnsureBand(QualityBand band)
    {
        if (!Enum.IsDefined(band))
        {
            throw new ArgumentOutOfRangeException(nameof(band), band, "Unknown quality band.");
        }
    }
}

public readonly record struct AttributeWeight(string Key, int Weight);
