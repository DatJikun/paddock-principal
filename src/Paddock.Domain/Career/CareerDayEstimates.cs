using Paddock.Domain.People;

namespace Paddock.Domain.Career;

/// <summary>
/// ESTIMATE rules for the day-by-day career run. None of these is a measured fact.
/// Edit them here; the run reads this type and does not repeat the numbers at the call site.
/// The player-facing explanation is <c>career.run.estimates</c> in both string files.
/// </summary>
public static class CareerDayEstimates
{
    /// <summary>ESTIMATE: a driver is not considered for retirement before this birthday age.</summary>
    public const int DriverRetirementFromAge = 38;

    /// <summary>ESTIMATE: a driver retires on the birthday that reaches this age, with no roll.</summary>
    public const int DriverRetirementCertainAge = 50;

    /// <summary>ESTIMATE: a staff member is not considered for retirement before this birthday age.</summary>
    public const int StaffRetirementFromAge = 65;

    /// <summary>ESTIMATE: a staff member retires on the birthday that reaches this age, with no roll.</summary>
    public const int StaffRetirementCertainAge = 80;

    /// <summary>
    /// ESTIMATE: month of the pool-entry day. The people schedule stores a year, not a day,
    /// so every scheduled driver enters on this month and <see cref="PoolEntryDay"/>.
    /// Generated drivers of later seasons enter on the same day.
    /// </summary>
    public const int PoolEntryMonth = 1;

    /// <summary>ESTIMATE: day of month of the pool-entry day. See <see cref="PoolEntryMonth"/>.</summary>
    public const int PoolEntryDay = 1;

    /// <summary>
    /// ESTIMATE: fictional drivers added to the talent pool on the pool-entry day of each season
    /// after the opening season. The opening season's pool is built by the world initializer.
    /// </summary>
    public const int GeneratedIntakePerSeason = 2;

    /// <summary>ESTIMATE: quality mix of drivers who enter the pool during the run, weights in thousandths.</summary>
    public static readonly QualityBandWeight[] PoolQualityWeights =
    [
        new(QualityBand.Filler, 600),
        new(QualityBand.Solid, 200),
        new(QualityBand.FutureStar, 200),
    ];

    /// <summary>ESTIMATE: nationalities offered to generated pool intake, each with weight 1.</summary>
    public static readonly NationalityWeight[] IntakeNationalities =
    [
        new("GBR", 1),
        new("ITA", 1),
        new("DEU", 1),
        new("FRA", 1),
        new("BRA", 1),
        new("USA", 1),
    ];

    /// <summary>Pool-entry day as <c>MM-dd</c>, for the estimate explanation.</summary>
    public static string PoolEntryDateText() =>
        PoolEntryMonth.ToString("00", System.Globalization.CultureInfo.InvariantCulture)
        + "-"
        + PoolEntryDay.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>One quality band and its weight in a mix. The weight is a positive whole number.</summary>
public readonly record struct QualityBandWeight
{
    public QualityBandWeight(QualityBand band, int weight)
    {
        if (!Enum.IsDefined(band))
        {
            throw new ArgumentOutOfRangeException(nameof(band), band, "Unknown quality band.");
        }

        if (weight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "Quality weight must be positive.");
        }

        Band = band;
        Weight = weight;
    }

    public QualityBand Band { get; }

    public int Weight { get; }
}
