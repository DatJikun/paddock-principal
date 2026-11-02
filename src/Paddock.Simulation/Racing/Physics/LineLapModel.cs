namespace Paddock.Simulation.Racing.Physics;

/// <summary>
/// One car's quasi-steady speed profile along a <see cref="RacingLine"/> and the lap time it gives (PP-074, #353).
/// A pure function: track + car + fuel + tyre state in, lap time and trace out. No RNG, no world state, no clock.
/// </summary>
/// <remarks>
/// <para>Grip is the friction circle <c>a_lat² + a_long² ≤ (μ · (g + k_df · v² / m))²</c> with <c>a_lat = v² · κ</c>.</para>
/// <para>Corner limit: the highest speed at which the circle still has room for the longitudinal force that holds speed
/// against drag, <c>(v²κ)² + (k_drag · v² / m)² = (μ · (g + k_df · v² / m))²</c>, a quadratic in <c>v²</c>. Holding
/// speed is allowed in a corner; otherwise a car on the limit would bleed speed through every long bend.</para>
/// <para>Forward pass (acceleration): drive is power over speed, <c>P / (m · v)</c>, capped by what the circle leaves after
/// the lateral load; drag is subtracted. Backward pass (braking): what the circle leaves, capped by the brake system,
/// plus drag. Each step is explicit in <c>v²</c> over the station's segment: <c>v₁² = v₀² ± 2 · a · ds</c>.</para>
/// <para>Determinism across operating systems (TECH §6.5a): this file uses only <c>+ − × ÷</c>, <see cref="Math.Sqrt"/>,
/// <c>Min</c> and <c>Max</c>. The terminal speed <c>∛(P / k_drag)</c> is found by a fixed number of Newton steps, not
/// <c>Math.Cbrt</c>.</para>
/// </remarks>
public static class LineLapModel
{
    private const int TerminalSpeedNewtonSteps = 64;

    /// <summary>The lap of <paramref name="car"/> (from <see cref="Pace.CarPhysics"/>) with fuel and tyre state on <paramref name="line"/>.</summary>
    public static LineLapTrace Run(RacingLine line, Pace.CarPhysics car, double fuelKg, double tyreGrip = 1d) =>
        Run(line, LineCar.From(car, fuelKg, tyreGrip));

    /// <summary>The lap of <paramref name="car"/> on <paramref name="line"/>.</summary>
    public static LineLapTrace Run(RacingLine line, LineCar car)
    {
        ArgumentNullException.ThrowIfNull(line);
        car.Validate();

        var n = line.Count;
        var curvature = line.CurvaturePerM;
        var segments = line.SegmentLengthsM;
        var terminal = TerminalSpeed(car);

        var limits = new double[n];
        var start = 0;
        for (var i = 0; i < n; i++)
        {
            limits[i] = Math.Min(CornerLimit(curvature[i], car), terminal);
            if (limits[i] < limits[start])
            {
                start = i;
            }
        }

        var speeds = (double[])limits.Clone();
        var converged = false;
        for (var sweep = 0; sweep < LinePhysicsConstants.MaxSpeedSweeps; sweep++)
        {
            var changed = false;

            // Forward from the slowest station: nothing may go faster than accelerating from the station before allows.
            for (var k = 0; k < n; k++)
            {
                var i = (start + k) % n;
                var next = (i + 1) % n;
                var reach = Accelerate(speeds[i], curvature[i], segments[i], car);
                if (reach < speeds[next])
                {
                    speeds[next] = reach;
                    changed = true;
                }
            }

            // Backward: nothing may go faster than braking in time for the station after allows.
            for (var k = 0; k < n; k++)
            {
                var i = ((start - k) % n + n) % n;
                var prev = (i - 1 + n) % n;
                var reach = Brake(speeds[i], curvature[i], segments[prev], car);
                if (reach < speeds[prev])
                {
                    speeds[prev] = reach;
                    changed = true;
                }
            }

            if (!changed)
            {
                converged = true;
                break;
            }
        }

        if (!converged)
        {
            throw new InvalidOperationException(
                $"The speed profile did not close in {LinePhysicsConstants.MaxSpeedSweeps} sweeps.");
        }

        var lapSeconds = 0d;
        var throttleM = 0d;
        var top = speeds[0];
        var bottom = speeds[0];
        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n;
            var v0 = speeds[i];
            var v1 = speeds[next];
            lapSeconds += 2d * segments[i] / (v0 + v1);
            top = Math.Max(top, v0);
            bottom = Math.Min(bottom, v0);
            if (v1 >= Accelerate(v0, curvature[i], segments[i], car))
            {
                throttleM += segments[i];
            }
        }

        return new LineLapTrace(speeds, lapSeconds, top, bottom, throttleM / line.LengthM);
    }

    /// <summary>
    /// Largest <c>v</c> with <c>(v²κ)² + (k_drag·v²/m)² ≤ (μ·(g + k_df·v²/m))²</c>; positive infinity when aero grip
    /// grows faster than the load (the caller caps it at the terminal speed).
    /// </summary>
    private static double CornerLimit(double signedCurvature, LineCar car)
    {
        var kappa = Math.Abs(signedCurvature);
        var drag = car.DragNPerMs2 / car.MassKg;
        var aero = car.TyreMu * car.DownforceNPerMs2 / car.MassKg;
        var mechanical = car.TyreMu * LinePhysicsConstants.GravityMs2;

        // A·u² + B·u + C = 0 in u = v².
        var a = (kappa * kappa) + (drag * drag) - (aero * aero);
        if (!(a > 0d))
        {
            return double.PositiveInfinity;
        }

        var b = -2d * mechanical * aero;
        var c = -(mechanical * mechanical);
        var u = (-b + Math.Sqrt((b * b) - (4d * a * c))) / (2d * a);
        return Math.Sqrt(u);
    }

    /// <summary>Grip left for the longitudinal direction after the lateral load at <paramref name="speed"/>.</summary>
    private static double LongitudinalGrip(double speed, double signedCurvature, LineCar car)
    {
        var v2 = speed * speed;
        var total = car.TyreMu * (LinePhysicsConstants.GravityMs2 + (car.DownforceNPerMs2 * v2 / car.MassKg));
        var lateral = v2 * Math.Abs(signedCurvature);
        var room = (total * total) - (lateral * lateral);
        return room > 0d ? Math.Sqrt(room) : 0d;
    }

    private static double Accelerate(double speed, double signedCurvature, double distance, LineCar car)
    {
        var traction = LongitudinalGrip(speed, signedCurvature, car);
        var drive = speed > 0d ? Math.Min(car.PowerW / (car.MassKg * speed), traction) : traction;
        var acceleration = drive - (car.DragNPerMs2 * speed * speed / car.MassKg);
        var v2 = (speed * speed) + (2d * acceleration * distance);
        return v2 > 0d ? Math.Sqrt(v2) : 0d;
    }

    private static double Brake(double speed, double signedCurvature, double distance, LineCar car)
    {
        var grip = Math.Min(LongitudinalGrip(speed, signedCurvature, car), car.BrakeForceN / car.MassKg);
        var deceleration = grip + (car.DragNPerMs2 * speed * speed / car.MassKg);
        return Math.Sqrt((speed * speed) + (2d * deceleration * distance));
    }

    /// <summary>
    /// Speed where power balances drag on a straight: <c>v³ = P / k_drag</c>, by Newton's method from above, with a
    /// fixed step count (no <c>Math.Cbrt</c>, which is not guaranteed bit-identical across C libraries).
    /// </summary>
    private static double TerminalSpeed(LineCar car)
    {
        if (!(car.DragNPerMs2 > 0d))
        {
            return double.MaxValue;
        }

        var cube = car.PowerW / car.DragNPerMs2;

        // Start above the root (v ≥ 1 and v ≥ cube both bound it), so Newton descends monotonically.
        var v = Math.Max(1d, cube);
        for (var step = 0; step < TerminalSpeedNewtonSteps; step++)
        {
            var next = v - (((v * v * v) - cube) / (3d * v * v));
            if (!(next < v))
            {
                break;
            }

            v = next;
        }

        return v;
    }
}
