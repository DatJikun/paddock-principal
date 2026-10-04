using Paddock.Domain.Cars;
using Paddock.Domain.Time;
using Paddock.Simulation.Racing.Pace;

namespace Paddock.Simulation.Cars;

/// <summary>
/// The truth vector the race engine reads. Simulation code only: a manager or an AI never receives this.
/// Downforce above the era cap is cut here, so extra downforce gives no lap-time gain.
/// Understanding is stored on the car and applied by T42; it does not scale the vector yet.
/// </summary>
public static class CarPerformanceFor
{
    public static CarPerformance Resolve(TeamCar car, EraPerformanceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(car);
        ArgumentNullException.ThrowIfNull(limits);
        return new CarPerformance(
            car.Levels.Power,
            Math.Min(car.Levels.Downforce, limits.DownforceCap),
            car.Levels.MechanicalGrip,
            car.Levels.Braking,
            car.Levels.Reliability);
    }

    public static CarPerformance Resolve(CarsSection section, string carId, GameDate date)
    {
        ArgumentNullException.ThrowIfNull(section);
        var car = section.Find(carId) ?? throw new ArgumentException("No car has id '" + carId + "'.", nameof(carId));
        return Resolve(car, EraPerformanceLimits.EstimateFor(date.Year));
    }
}
