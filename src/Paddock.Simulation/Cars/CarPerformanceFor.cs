using Paddock.Domain.Cars;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Simulation.Racing.Pace;

namespace Paddock.Simulation.Cars;

/// <summary>
/// The truth vector the race engine reads. Simulation code only: a manager or an AI never receives this.
/// Downforce above the era cap is cut here, so extra downforce gives no lap-time gain.
/// Understanding (PP-066) takes a few points off every component but power: see CarEstimates.UnderstandingMaxLoss.
/// </summary>
public static class CarPerformanceFor
{
    public static CarPerformance Resolve(TeamCar car, EraPerformanceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(car);
        ArgumentNullException.ThrowIfNull(limits);
        // A car the team does not understand yet loses a little everywhere but in raw power (PP-066). Full understanding loses nothing.
        var loss = CarEstimates.UnderstandingMaxLoss * (1d - (Math.Clamp(car.Understanding, 0d, 100d) / 100d));
        return new CarPerformance(
            car.Levels.Power,
            Math.Max(0d, Math.Min(car.Levels.Downforce, limits.DownforceCap) - loss),
            Math.Max(0d, car.Levels.MechanicalGrip - loss),
            Math.Max(0d, car.Levels.Braking - loss),
            Math.Max(0d, car.Levels.Reliability - loss));
    }

    /// <summary>
    /// The vector with the engine of the car's supply deal (T43). <see cref="EngineContribution.None"/> gives exactly the vector of
    /// <see cref="Resolve(TeamCar, EraPerformanceLimits)"/>. Power and reliability stay inside 0..100.
    /// </summary>
    public static CarPerformance Resolve(TeamCar car, EraPerformanceLimits limits, EngineContribution engine)
    {
        var baseline = Resolve(car, limits);
        return new CarPerformance(
            Math.Clamp(baseline.Power + engine.PowerOffset, 0d, 100d),
            baseline.Downforce,
            baseline.MechanicalGrip,
            baseline.Braking,
            Math.Clamp(baseline.Reliability + engine.ReliabilityOffset, 0d, 100d));
    }

    public static CarPerformance Resolve(CarsSection section, string carId, GameDate date)
    {
        ArgumentNullException.ThrowIfNull(section);
        var car = section.Find(carId) ?? throw new ArgumentException("No car has id '" + carId + "'.", nameof(carId));
        return Resolve(car, EraPerformanceLimits.EstimateFor(date.Year));
    }
}
