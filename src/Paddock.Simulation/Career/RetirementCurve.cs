namespace Paddock.Simulation.Career;

/// <summary>
/// Placeholder retirement roll (ESTIMATE). The ages live in <see cref="Paddock.Domain.Career.CareerDayEstimates"/>.
/// Below the first age there is no roll. At or above the certain age the person retires with no roll.
/// Between them the chance at age <c>a</c> is <c>(a - from + 1) / (certain - from + 1)</c>,
/// compared with one draw from the caller. The draw is the <c>LifeEvents</c> stream for that season.
/// </summary>
public static class RetirementCurve
{
    public static bool Retires(int age, bool driver, Func<double> nextUnit)
    {
        ArgumentNullException.ThrowIfNull(nextUnit);
        var from = driver
            ? Paddock.Domain.Career.CareerDayEstimates.DriverRetirementFromAge
            : Paddock.Domain.Career.CareerDayEstimates.StaffRetirementFromAge;
        var certain = driver
            ? Paddock.Domain.Career.CareerDayEstimates.DriverRetirementCertainAge
            : Paddock.Domain.Career.CareerDayEstimates.StaffRetirementCertainAge;
        if (certain <= from)
        {
            throw new InvalidOperationException("The certain retirement age must be after the first checked age.");
        }

        if (age < from)
        {
            return false;
        }

        if (age >= certain)
        {
            return true;
        }

        var steps = certain - from + 1;
        var numerator = age - from + 1;
        return nextUnit() < numerator / (double)steps;
    }
}
