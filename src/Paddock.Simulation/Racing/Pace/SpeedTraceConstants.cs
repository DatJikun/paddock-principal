namespace Paddock.Simulation.Racing.Pace;

/// <summary>
/// Tunables for <see cref="SpeedTraceModel"/> and the era/team car mapping. EVERY value is an ESTIMATE:
/// nothing has been calibrated against timing data. Changing a value changes lap times.
/// </summary>
public static class SpeedTraceConstants
{
    // ---- Rating sensitivities -----------------------------------------------------------------------------
    // Scale is 1 at rating 50 (the era average) and linear in (rating - 50) / 50. A sensitivity of 0.40 means
    // rating 100 is 1.40× the era value and rating 0 is 0.60×. Kept below 1 so a zero rating stays positive.

    /// <summary>
    /// ESTIMATE: how far power swings around the era average. Low-speed acceleration scales with this factor;
    /// top speed scales with its cube root (drag power is proportional to speed cubed).
    /// </summary>
    public const double PowerSensitivity = 0.40d;

    /// <summary>
    /// ESTIMATE: mechanical-grip swing. Smaller than power: within an era the tyre is largely common, and the
    /// chassis only moves lateral grip so far.
    /// </summary>
    public const double MechanicalGripSensitivity = 0.25d;

    /// <summary>
    /// ESTIMATE: braking swing inside an era. Carbon versus steel is already in the era table; this is only the
    /// car-to-car gap on top of that.
    /// </summary>
    public const double BrakingSensitivity = 0.25d;

    /// <summary>
    /// ESTIMATE: aero is the big performance differentiator, so downforce coefficient <c>c</c> swings wider than grip.
    /// The rating is capped by <see cref="EraPerformanceLimits.DownforceCap"/> before this scale is applied.
    /// </summary>
    public const double DownforceSensitivity = 0.50d;

    // ---- Era baselines, one anchor per decade -------------------------------------------------------------
    // Interpolated with the same smoothstep as the lap-time model (see EraCurve). Flat outside 1950 and 2020.
    // Each anchor is the average car of that decade, not the fastest car.

    /// <summary>
    /// ESTIMATE: lateral grip without aero, m/s². Narrow treaded tyres in 1950 (~1 g); slicks by 1980; grooved
    /// tyres cut mechanical grip around 1998–2008; slicks and wider tyres bring it back afterwards.
    /// </summary>
    public static readonly (int Season, double Value)[] MechanicalGripMs2 =
    [
        (1950, 10.0d), // ESTIMATE: ~1.0 g, narrow crossplies, no aero helping the contact patch.
        (1960, 12.0d), // ESTIMATE: wider tyres, still a treaded compound.
        (1970, 14.0d), // ESTIMATE: slick-width tyres arriving with the wing era.
        (1980, 16.5d), // ESTIMATE: full slicks at the ground-effect peak.
        (1990, 18.0d), // ESTIMATE: mature slicks before the grooved-tyre rules.
        (2000, 16.0d), // ESTIMATE: grooved tyres (from 1998) take mechanical grip back.
        (2010, 17.0d), // ESTIMATE: slicks return in 2009.
        (2020, 18.5d), // ESTIMATE: wider 2017 tyres, still a control tyre.
    ];

    /// <summary>
    /// ESTIMATE: aero coefficient <c>c</c> (1/m), where extra lateral acceleration is <c>c·v²</c>. Zero until wings;
    /// the 1970 anchor carries the 1968 wing step; 1980 is the 1977–82 ground-effect peak; 1990 is the
    /// flat-bottom, rules-limited recovery; 2020 is the high-downforce hybrid era.
    /// </summary>
    public static readonly (int Season, double Value)[] DownforcePerM =
    [
        (1950, 0d), // ESTIMATE: no wings, no tunnels — lateral grip does not grow with speed.
        (1960, 0d), // ESTIMATE: still before the 1968 wing; this anchor stays at zero.
        (1970, 0.0018d), // ESTIMATE: early wings. At 70 m/s this is about 0.9 g of extra lateral grip.
        (1980, 0.0042d), // ESTIMATE: ground effect (1977–82). Fast corners become aero-limited.
        (1990, 0.0026d), // ESTIMATE: flat bottoms from 1983 cut the peak; wings recover under the rules.
        (2000, 0.0033d), // ESTIMATE: rules-limited aero on a lighter, stiffer car.
        (2010, 0.0030d), // ESTIMATE: the 2009 aero cut, then a partial recovery.
        (2020, 0.0039d), // ESTIMATE: 2017 width rules restore high downforce in the hybrid era.
    ];

    /// <summary>
    /// ESTIMATE: top speed of the average car, m/s. Wings and ground effect add drag, so speed does not rise
    /// every decade; the V10 years are the peak, the early hybrid years give some of it back.
    /// </summary>
    public static readonly (int Season, double Value)[] TopSpeedMs =
    [
        (1950, 70d), // ESTIMATE: ~250 km/h on a long straight.
        (1960, 77d), // ESTIMATE: ~277 km/h, more power, little extra drag.
        (1970, 83d), // ESTIMATE: ~299 km/h; early wings are not yet the dominant drag.
        (1980, 81d), // ESTIMATE: ~292 km/h; ground-effect drag offsets the power gain.
        (1990, 91d), // ESTIMATE: ~328 km/h, turbo then high-revving NA power, flatter floors.
        (2000, 97d), // ESTIMATE: ~349 km/h, the V10 peak (the fastest years sit next to this anchor).
        (2010, 89d), // ESTIMATE: ~320 km/h, V8s and the 2009 aero package.
        (2020, 93d), // ESTIMATE: ~335 km/h, hybrid power recovered but not back to the V10 peak.
    ];

    /// <summary>
    /// ESTIMATE: longitudinal acceleration at low speed, m/s², before drag. The trace uses
    /// <c>a(v) = a0 · (1 − (v / vmax)²)</c>, so this number is the launch end of that curve.
    /// </summary>
    public static readonly (int Season, double Value)[] AccelerationMs2 =
    [
        (1950, 3.4d), // ESTIMATE: ~0–100 km/h in about 8 s for a front-engined car.
        (1960, 4.4d), // ESTIMATE: lighter rear-engined cars, still modest power.
        (1970, 6.0d), // ESTIMATE: DFV-era power-to-weight.
        (1980, 8.0d), // ESTIMATE: early turbos; traction, not power, caps the launch.
        (1990, 10.0d), // ESTIMATE: high-revving NA engines, better tyres off the corner.
        (2000, 12.0d), // ESTIMATE: V10 peak, ~0–100 km/h in well under 3 s.
        (2010, 11.0d), // ESTIMATE: V8s give a little of that up.
        (2020, 12.5d), // ESTIMATE: hybrid torque fills the low-speed gap.
    ];

    /// <summary>
    /// ESTIMATE: constant braking deceleration, m/s². Aero is not applied on top (the era's brakes and tyres are
    /// already in the anchor). Drums, then discs, then carbon.
    /// </summary>
    public static readonly (int Season, double Value)[] BrakingMs2 =
    [
        (1950, 9d), // ESTIMATE: ~0.9 g, drum brakes, narrow tyres.
        (1960, 12d), // ESTIMATE: discs become normal, still ~1.2 g.
        (1970, 18d), // ESTIMATE: wide tyres and wings add braking authority.
        (1980, 28d), // ESTIMATE: ground effect and early carbon brakes.
        (1990, 38d), // ESTIMATE: carbon brakes are the norm.
        (2000, 46d), // ESTIMATE: ~4.7 g, stiffer cars, better cooling.
        (2010, 50d), // ESTIMATE: ~5 g on slicks.
        (2020, 55d), // ESTIMATE: ~5.6 g, the modern carbon peak for an average car.
    ];
}
