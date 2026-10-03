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
    /// Fillers of later seasons enter on the same day (the pool rules are in <c>PoolEstimates</c>).
    /// </summary>
    public const int PoolEntryMonth = 1;

    /// <summary>ESTIMATE: day of month of the pool-entry day. See <see cref="PoolEntryMonth"/>.</summary>
    public const int PoolEntryDay = 1;

    /// <summary>Pool-entry day as <c>MM-dd</c>, for the estimate explanation.</summary>
    public static string PoolEntryDateText() =>
        PoolEntryMonth.ToString("00", System.Globalization.CultureInfo.InvariantCulture)
        + "-"
        + PoolEntryDay.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
}
