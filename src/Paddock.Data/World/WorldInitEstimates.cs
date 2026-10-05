using Paddock.Domain.People;
using Paddock.Domain.World;

namespace Paddock.Data.World;

/// <summary>
/// ESTIMATE values used only while the starting world is built. None is a measured fact; they are
/// placeholders until calibration and live here so they can be edited without hunting call sites.
/// </summary>
public static class WorldInitEstimates
{
    /// <summary>ESTIMATE: length of the first contract of every driver and staff member, in seasons.</summary>
    public const int InitialContractSeasons = 1;

    /// <summary>ESTIMATE: nominal salary of a starting contract. The economy is a later phase, so this means "not modelled".</summary>
    public const long PlaceholderSalary = 0;

    /// <summary>ESTIMATE: nominal budget of a starting organization. Not modelled yet.</summary>
    public const long PlaceholderBudget = 0;

    /// <summary>ESTIMATE: the old flat rating of real staff. Chairs now draw a budget band from <see cref="StaffEstimates"/>.</summary>
    public const int UnratedStaffAttribute = 10;

    /// <summary>ESTIMATE: strength passed to <c>RandomizeKnownPerson</c> for real names with random skills (full spread).</summary>
    public const int RandomizedSkillStrength = GenerationEstimates.StrengthMax;

    /// <summary>ESTIMATE: strength of the stand-in used for a real driver the ratings model has not rated (near-flat).</summary>
    public const int UnratedFallbackStrength = GenerationEstimates.StrengthMin;

    /// <summary>ESTIMATE: quality band for a real or random-skill driver who races in the start year.</summary>
    public const QualityBand RacingKnownQuality = QualityBand.Solid;

    /// <summary>ESTIMATE: quality band for a real or random-skill driver in the talent pool.</summary>
    public const QualityBand PoolKnownQuality = QualityBand.Filler;

    /// <summary>ESTIMATE: the old single band of generated staff. Rank now picks the band in <see cref="StaffEstimates"/>.</summary>
    public const QualityBand GeneratedStaffQuality = QualityBand.Solid;

    /// <summary>ESTIMATE: driver seats per team when no seat count is known for it.</summary>
    public const int DefaultSeatsPerTeam = 2;

    /// <summary>ESTIMATE: generated pool size as a share of the generated grid, in percent.</summary>
    public const int PoolPercentOfGrid = 50;

    /// <summary>ESTIMATE: month used when only the birth year of a person is known.</summary>
    public const int EstimatedBirthMonth = 7;

    /// <summary>ESTIMATE: day of month used when only the birth year of a person is known.</summary>
    public const int EstimatedBirthDay = 1;

    /// <summary>ESTIMATE: age used to place a staff member with an unknown birth date.</summary>
    public const int EstimatedStaffAge = 45;

    /// <summary>Nationality text for a person whose authored nationality is missing.</summary>
    public const string UnknownNationality = "UNKNOWN";

    /// <summary>ESTIMATE: weight of a team's home country among generated nationalities.</summary>
    public const int HomeCountryWeight = 3;

    /// <summary>ESTIMATE: the seat status given to every starting driver, because no seat hierarchy is authored.</summary>
    public const SeatStatus DefaultSeatStatus = SeatStatus.Equal;

    /// <summary>ESTIMATE: share (in thousandths) of each quality band among generated grid drivers.</summary>
    public static readonly (QualityBand Band, int Weight)[] GridQualityWeights =
    [
        (QualityBand.Filler, 300),
        (QualityBand.Solid, 450),
        (QualityBand.Contender, 200),
        (QualityBand.FutureStar, 50),
    ];

    /// <summary>ESTIMATE: share (in thousandths) of each quality band among generated pool drivers.</summary>
    public static readonly (QualityBand Band, int Weight)[] PoolQualityWeights =
    [
        (QualityBand.Filler, 600),
        (QualityBand.Solid, 200),
        (QualityBand.FutureStar, 200),
    ];

    /// <summary>ESTIMATE: nationalities offered to the generator, each with weight 1.</summary>
    public static readonly string[] GeneratedNationalities = ["GBR", "ITA", "DEU", "FRA", "BRA", "USA"];
}
