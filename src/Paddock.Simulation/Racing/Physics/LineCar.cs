using System.Globalization;
using Paddock.Simulation.Racing.Pace;

namespace Paddock.Simulation.Racing.Physics;

/// <summary>
/// One car as the v2 speed profile sees it (PP-074, TECH §6.5a): forces, not ratings.
/// Grip is the friction circle <c>a_lat² + a_long² ≤ (μ · (g + k_df · v² / m))²</c>; drive is power over speed,
/// limited by the same circle; drag is <c>k_drag · v²</c>; braking is the circle, capped by the brake system.
/// </summary>
/// <param name="MassKg">Car, driver and fuel, kg.</param>
/// <param name="PowerW">Power at the wheels, W.</param>
/// <param name="DragNPerMs2">Drag coefficient <c>k_drag</c>: drag force is <c>k_drag · v²</c>, N per (m/s)².</param>
/// <param name="DownforceNPerMs2">Downforce coefficient <c>k_df</c>: downforce is <c>k_df · v²</c>, N per (m/s)².</param>
/// <param name="TyreMu">Tyre friction coefficient <c>μ</c> (tyre state already applied).</param>
/// <param name="BrakeForceN">Most braking force the brake system gives, N, whatever the grip.</param>
public readonly record struct LineCar(
    double MassKg,
    double PowerW,
    double DragNPerMs2,
    double DownforceNPerMs2,
    double TyreMu,
    double BrakeForceN)
{
    /// <summary>
    /// The v2 car for <paramref name="car"/> (from <see cref="CarPhysics.From"/>) carrying <paramref name="fuelKg"/>
    /// of fuel on tyres at <paramref name="tyreGrip"/> of their reference grip (1 = the era tyre, below 1 = worn).
    /// </summary>
    /// <remarks>
    /// <see cref="CarPhysics"/> describes a car at the reference mass (<see cref="LinePhysicsConstants.CarMassKg2010"/>,
    /// no fuel), relative to the 2010 era car:
    /// <list type="bullet">
    /// <item><c>μ = MechanicalGrip / g</c>; <c>k_df = DownforcePerM · m_ref / μ</c>, so at the reference mass the lateral
    /// limit is exactly <c>a0 + c · v²</c> as in <see cref="SpeedTraceModel"/>.</item>
    /// <item>power scales with <c>Acceleration / era Acceleration</c>; <c>k_drag = P / TopSpeed³</c>, so the car tops out
    /// at <see cref="CarPhysics.TopSpeedMs"/> on a long straight.</item>
    /// <item>brake force is <c>Braking · m_ref</c>: the same brakes stop a heavier car more slowly.</item>
    /// </list>
    /// Fuel adds mass only: more inertia, the same aero and tyre forces, so a full car is slower everywhere.
    /// Only the 2010 era has mass and power constants; another season's <see cref="CarPhysics"/> is still read
    /// relative to 2010.
    /// </remarks>
    public static LineCar From(CarPhysics car, double fuelKg, double tyreGrip = 1d)
    {
        Positive(car.MechanicalGripMs2, nameof(car.MechanicalGripMs2));
        NotNegative(car.DownforcePerM, nameof(car.DownforcePerM));
        Positive(car.TopSpeedMs, nameof(car.TopSpeedMs));
        Positive(car.AccelerationMs2, nameof(car.AccelerationMs2));
        Positive(car.BrakingMs2, nameof(car.BrakingMs2));
        NotNegative(fuelKg, nameof(fuelKg));
        Positive(tyreGrip, nameof(tyreGrip));

        var era = EraCarPhysics.For(LinePhysicsConstants.ReferenceSeason);
        const double referenceMass = LinePhysicsConstants.CarMassKg2010;
        var referenceMu = car.MechanicalGripMs2 / LinePhysicsConstants.GravityMs2;
        var power = LinePhysicsConstants.EnginePowerW2010 * (car.AccelerationMs2 / era.AccelerationMs2);
        var vmax = car.TopSpeedMs;

        return new LineCar(
            referenceMass + fuelKg,
            power,
            power / (vmax * vmax * vmax),
            car.DownforcePerM * referenceMass / referenceMu,
            referenceMu * tyreGrip,
            car.BrakingMs2 * referenceMass);
    }

    internal void Validate()
    {
        Positive(MassKg, nameof(MassKg));
        Positive(PowerW, nameof(PowerW));
        NotNegative(DragNPerMs2, nameof(DragNPerMs2));
        NotNegative(DownforceNPerMs2, nameof(DownforceNPerMs2));
        Positive(TyreMu, nameof(TyreMu));
        Positive(BrakeForceN, nameof(BrakeForceN));
    }

    private static void Positive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                name,
                value,
                $"Expected a finite value > 0, got {value.ToString("R", CultureInfo.InvariantCulture)}.");
        }
    }

    private static void NotNegative(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0d)
        {
            throw new ArgumentOutOfRangeException(
                name,
                value,
                $"Expected a finite value >= 0, got {value.ToString("R", CultureInfo.InvariantCulture)}.");
        }
    }
}
