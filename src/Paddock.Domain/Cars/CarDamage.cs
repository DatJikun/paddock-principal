using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Cars;

/// <summary>How bad a crash was for the car (#270). A wreck needs a new chassis; the others are repaired.</summary>
public enum DamageKind
{
    Light,
    Heavy,
    Wrecked,
}

/// <summary>Where the crash happened.</summary>
public enum DamageSource
{
    Race,
    Test,
}

/// <summary>One damaged car: until <see cref="ReadyOn"/> it cannot start a race. The repair bill is posted to the ledger on <see cref="DamagedOn"/>.</summary>
public sealed record CarDamage(
    string CarId,
    OrganizationId Organization,
    DamageKind Kind,
    DamageSource Source,
    GameDate DamagedOn,
    GameDate ReadyOn,
    long CostCents);

/// <summary>
/// The older chassis a team keeps from the end of the previous season: its last concept and levels, before the spare penalty.
/// A damaged car that cannot be fixed in time runs on it, slower (<see cref="CarDamageEstimates.SpareLevelShare"/>).
/// </summary>
public sealed record SpareChassis(OrganizationId Organization, int Season, CarConcept Concept, PerformanceLevels Levels);

/// <summary>ESTIMATE: every number of crash damage in one place (#270). None is calibrated.</summary>
public static class CarDamageEstimates
{
    /// <summary>ESTIMATE: no repair or new chassis takes fewer days than this.</summary>
    public const int MinDays = 5;

    /// <summary>ESTIMATE: extra days drawn per crash, 0 to this.</summary>
    public const int MaxExtraDays = 2;

    /// <summary>ESTIMATE: levels of the spare are this much lower than the chassis it was.</summary>
    public const double SpareLevelShare = 0.12;

    /// <summary>ESTIMATE: the spare understanding is this share of the damaged car.</summary>
    public const double SpareUnderstandingShare = 0.5;

    /// <summary>ESTIMATE: chance a car crashes on a private test day in 1950; scaled by <see cref="EraScale"/>.</summary>
    public const double TestCrashChance1950 = 0.08;

    /// <summary>ESTIMATE: base days in 1950 for a light repair, a heavy repair and a new chassis.</summary>
    public static int BaseDays(DamageKind kind) => kind switch
    {
        DamageKind.Light => 5,
        DamageKind.Heavy => 14,
        DamageKind.Wrecked => 35,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>ESTIMATE: share of the typical season budget a repair or a new chassis costs.</summary>
    public static double CostShare(DamageKind kind) => kind switch
    {
        DamageKind.Light => 0.015,
        DamageKind.Heavy => 0.04,
        DamageKind.Wrecked => 0.10,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>ESTIMATE: 1.0 in 1950, falling to 0.3 by 2026 (stronger cars and better workshops).</summary>
    public static double EraScale(int year) => Math.Max(0.3d, 1d - (0.0092d * Math.Max(0, year - 1950)));

    public static int Days(DamageKind kind, int year, int extra)
    {
        var scaled = (int)Math.Round(BaseDays(kind) * EraScale(year), MidpointRounding.AwayFromZero);
        return Math.Max(MinDays, scaled) + Math.Clamp(extra, 0, MaxExtraDays);
    }

    public static long CostCents(DamageKind kind, long typicalCents) =>
        typicalCents <= 0 ? 0L : Math.Max(1L, (long)Math.Round(typicalCents * CostShare(kind), MidpointRounding.AwayFromZero));

    /// <summary>
    /// Picks a severity from a uniform draw in [0, 1). ESTIMATE: wrecks are likelier in old eras; a crash that hurt the driver
    /// is never light.
    /// </summary>
    public static DamageKind Kind(double draw, int year, bool driverHurt)
    {
        var wrecked = 0.3d * EraScale(year) * (driverHurt ? 1.5d : 1d);
        var heavy = driverHurt ? 0.5d : 0.35d;
        if (draw < wrecked)
        {
            return DamageKind.Wrecked;
        }

        if (draw < wrecked + heavy)
        {
            return DamageKind.Heavy;
        }

        return driverHurt ? DamageKind.Heavy : DamageKind.Light;
    }

    public static double TestCrashChance(int year) => TestCrashChance1950 * EraScale(year);
}

/// <summary>
/// Damaged cars and spare chassis, section <see cref="SectionName"/>. Immutable. A save without any has no section.
/// Canonical text (schema 1), after the section header: a count of damages and one line each (car, organization, kind,
/// source, damaged on, ready on, cost in cents), then a count of spares and one line each (organization, season, six
/// concept axes and five levels as milli-units).
/// </summary>
public sealed class CarDamageSection : IWorldSection
{
    public const string SectionName = "damage";

    private readonly SortedDictionary<string, CarDamage> _damages;
    private readonly SortedDictionary<string, SpareChassis> _spares;

    private CarDamageSection(SortedDictionary<string, CarDamage> damages, SortedDictionary<string, SpareChassis> spares)
    {
        _damages = damages;
        _spares = spares;
    }

    public static CarDamageSection Empty { get; } = new(
        new SortedDictionary<string, CarDamage>(StringComparer.Ordinal),
        new SortedDictionary<string, SpareChassis>(StringComparer.Ordinal));

    public string Name => SectionName;

    public int SchemaVersion => 1;

    public bool IsEmpty => _damages.Count == 0 && _spares.Count == 0;

    public IReadOnlyList<CarDamage> Damages => _damages.Values.ToArray();

    public IReadOnlyList<SpareChassis> Spares => _spares.Values.ToArray();

    public CarDamage? DamageOf(string carId) => _damages.TryGetValue(carId, out var damage) ? damage : null;

    /// <summary>The damage that still keeps the car out on <paramref name="day"/>, or null.</summary>
    public CarDamage? OpenOn(string carId, GameDate day) =>
        DamageOf(carId) is { } damage && damage.ReadyOn > day ? damage : null;

    public SpareChassis? SpareOf(OrganizationId organization) =>
        organization.IsAssigned && _spares.TryGetValue(organization.Value, out var spare) ? spare : null;

    public CarDamageSection WithDamage(CarDamage damage)
    {
        ArgumentNullException.ThrowIfNull(damage);
        Validate(damage);
        var copy = new SortedDictionary<string, CarDamage>(_damages, StringComparer.Ordinal) { [damage.CarId] = damage };
        return new CarDamageSection(copy, _spares);
    }

    public CarDamageSection WithoutDamage(string carId)
    {
        if (!_damages.ContainsKey(carId))
        {
            return this;
        }

        var copy = new SortedDictionary<string, CarDamage>(_damages, StringComparer.Ordinal);
        copy.Remove(carId);
        return new CarDamageSection(copy, _spares);
    }

    public CarDamageSection WithSpare(SpareChassis spare)
    {
        ArgumentNullException.ThrowIfNull(spare);
        if (!spare.Organization.IsAssigned)
        {
            throw new InvalidOperationException("A spare needs an organization.");
        }

        var copy = new SortedDictionary<string, SpareChassis>(_spares, StringComparer.Ordinal) { [spare.Organization.Value] = spare };
        return new CarDamageSection(_damages, copy);
    }

    public CarDamageSection WithoutSpare(OrganizationId organization)
    {
        if (!organization.IsAssigned || !_spares.ContainsKey(organization.Value))
        {
            return this;
        }

        var copy = new SortedDictionary<string, SpareChassis>(_spares, StringComparer.Ordinal);
        copy.Remove(organization.Value);
        return new CarDamageSection(_damages, copy);
    }

    public static CarDamageSection Restore(IEnumerable<CarDamage> damages, IEnumerable<SpareChassis> spares)
    {
        ArgumentNullException.ThrowIfNull(damages);
        ArgumentNullException.ThrowIfNull(spares);
        var section = Empty;
        foreach (var damage in damages)
        {
            ArgumentNullException.ThrowIfNull(damage);
            if (section.DamageOf(damage.CarId) is not null)
            {
                throw new InvalidOperationException($"Car '{damage.CarId}' is damaged twice.");
            }

            section = section.WithDamage(damage);
        }

        foreach (var spare in spares)
        {
            ArgumentNullException.ThrowIfNull(spare);
            if (section.SpareOf(spare.Organization) is not null)
            {
                throw new InvalidOperationException($"Organization '{spare.Organization}' has two spares.");
            }

            section = section.WithSpare(spare);
        }

        return section;
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Count("damages", _damages.Count);
        foreach (var damage in _damages.Values)
        {
            writer.Begin("damage");
            writer.Field(damage.CarId);
            writer.Space();
            writer.Field(damage.Organization.Value);
            writer.Raw(
                " " + damage.Kind + " " + damage.Source + " " + damage.DamagedOn + " " + damage.ReadyOn
                + " " + damage.CostCents.ToString(CultureInfo.InvariantCulture));
            writer.End();
        }

        writer.Count("spares", _spares.Count);
        foreach (var spare in _spares.Values)
        {
            writer.Begin("spare");
            writer.Field(spare.Organization.Value);
            writer.Raw(
                " " + spare.Season.ToString(CultureInfo.InvariantCulture)
                + " " + M(spare.Concept.Aero) + " " + M(spare.Concept.Philosophy) + " " + M(spare.Concept.Window)
                + " " + M(spare.Concept.Cooling) + " " + M(spare.Concept.TyreKindness) + " " + M(spare.Concept.Integration)
                + " " + M(spare.Levels.Power) + " " + M(spare.Levels.Downforce) + " " + M(spare.Levels.MechanicalGrip)
                + " " + M(spare.Levels.Braking) + " " + M(spare.Levels.Reliability));
            writer.End();
        }
    }

    private static string M(double value) => CarEstimates.Milli(value).ToString(CultureInfo.InvariantCulture);

    private static void Validate(CarDamage damage)
    {
        if (!CarIds.TryParse(damage.CarId, out _)
            || !damage.Organization.IsAssigned
            || !Enum.IsDefined(damage.Kind)
            || !Enum.IsDefined(damage.Source)
            || damage.ReadyOn <= damage.DamagedOn
            || damage.CostCents < 0)
        {
            throw new InvalidOperationException($"The damage of '{damage.CarId}' is out of range.");
        }
    }
}
