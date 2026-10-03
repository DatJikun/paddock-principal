namespace Paddock.Domain.People;

/// <summary>
/// Growth, peak, plateau, and decline for one person.
/// <see cref="DeclineMilliPerYear"/> is thousandths of an overall point per year (an estimate).
/// Peak age is always after <see cref="GrowthStartAge"/>.
/// </summary>
public readonly record struct CareerCurve
{
    public CareerCurve(int growthStartAge, int peakAge, int plateauYears, int declineMilliPerYear)
    {
        if (growthStartAge < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(growthStartAge), growthStartAge, "Growth must start at age 1 or later.");
        }

        if (peakAge <= growthStartAge)
        {
            throw new ArgumentOutOfRangeException(nameof(peakAge), peakAge, "Peak age must be after growth start.");
        }

        if (plateauYears < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(plateauYears), plateauYears, "Plateau length cannot be negative.");
        }

        if (declineMilliPerYear <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(declineMilliPerYear), declineMilliPerYear, "Decline rate must be positive.");
        }

        GrowthStartAge = growthStartAge;
        PeakAge = peakAge;
        PlateauYears = plateauYears;
        DeclineMilliPerYear = declineMilliPerYear;
    }

    public int GrowthStartAge { get; }

    public int PeakAge { get; }

    public int PlateauYears { get; }

    public int DeclineMilliPerYear { get; }

    public double DeclinePerYear => DeclineMilliPerYear / 1000.0;
}
