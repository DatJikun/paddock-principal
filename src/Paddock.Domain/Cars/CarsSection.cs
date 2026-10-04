using System.Globalization;
using Paddock.Domain.World;

namespace Paddock.Domain.Cars;

/// <summary>
/// The cars of the world and the driver-fit records, section <see cref="SectionName"/>.
/// Immutable. Car numbers only move forward (INV-009). Truth stays here; a manager reads a query.
/// <para>
/// Canonical text (schema 1), after the section header. Integers are invariant. Axes are milli-units of −1..1.
/// Ratings, the ceiling, understanding and wear are milli-units of the stored number.
/// <code>
/// next-car &lt;n&gt;
/// next-draw &lt;n&gt;
/// cars &lt;count&gt;
/// car &lt;len&gt;:&lt;id&gt;
/// organization &lt;len&gt;:&lt;id&gt;
/// season &lt;year&gt;
/// axes &lt;aero&gt; &lt;philosophy&gt; &lt;window&gt; &lt;cooling&gt; &lt;tyre&gt; &lt;integration&gt;
/// levels &lt;power&gt; &lt;downforce&gt; &lt;grip&gt; &lt;braking&gt; &lt;reliability&gt;
/// ceiling &lt;milli&gt;
/// understanding &lt;milli&gt;
/// wear &lt;milli&gt;
/// cost &lt;int&gt;
/// engine &lt;len&gt;:&lt;key or -&gt;
/// driver &lt;len&gt;:&lt;id or -&gt;
/// fits &lt;count&gt;
/// fit &lt;len&gt;:&lt;personId&gt;
/// prefs &lt;balance&gt; &lt;traction&gt; &lt;Early|Normal|Late&gt;
/// experience &lt;starts&gt; &lt;wet&gt; &lt;seasons&gt;
/// laps &lt;count&gt;
/// lap &lt;len&gt;:&lt;trackId&gt; &lt;laps&gt;
/// </code>
/// </para>
/// </summary>
public sealed class CarsSection : IWorldSection
{
    public const string SectionName = "cars";

    private readonly SortedDictionary<string, TeamCar> _cars;
    private readonly SortedDictionary<string, DriverFitProfile> _fits;

    private CarsSection(
        long nextCar,
        long nextCeilingDraw,
        SortedDictionary<string, TeamCar> cars,
        SortedDictionary<string, DriverFitProfile> fits)
    {
        NextCar = nextCar;
        NextCeilingDraw = nextCeilingDraw;
        _cars = cars;
        _fits = fits;
    }

    public static CarsSection Empty { get; } = new(1, 1, new SortedDictionary<string, TeamCar>(StringComparer.Ordinal), new SortedDictionary<string, DriverFitProfile>(StringComparer.Ordinal));

    public string Name => SectionName;

    public int SchemaVersion => 1;

    public long NextCar { get; }

    /// <summary>The number the next concept-ceiling draw uses. Approval advances it; the draw itself is a child of Development.</summary>
    public long NextCeilingDraw { get; }

    public bool IsEmpty => NextCar == 1 && NextCeilingDraw == 1 && _cars.Count == 0 && _fits.Count == 0;

    public IReadOnlyList<TeamCar> Cars => _cars.Values.ToArray();

    public IReadOnlyList<DriverFitProfile> Profiles => _fits.Values.ToArray();

    public TeamCar? Find(string id) => _cars.TryGetValue(id, out var car) ? car : null;

    public IReadOnlyList<TeamCar> Of(OrganizationId organization) =>
        _cars.Values.Where(car => car.Organization == organization).ToArray();

    public DriverFitProfile? FitOf(PersonId person) =>
        person.IsAssigned && _fits.TryGetValue(person.Value, out var fit) ? fit : null;

    public static CarsSection Restore(
        long nextCar,
        long nextCeilingDraw,
        IEnumerable<TeamCar> cars,
        IEnumerable<DriverFitProfile> profiles)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nextCar, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(nextCeilingDraw, 1);
        ArgumentNullException.ThrowIfNull(cars);
        ArgumentNullException.ThrowIfNull(profiles);
        var carMap = new SortedDictionary<string, TeamCar>(StringComparer.Ordinal);
        foreach (var car in cars)
        {
            ArgumentNullException.ThrowIfNull(car);
            var number = CarIds.Require(car.Id);
            if (number >= nextCar)
            {
                throw new InvalidOperationException($"Car '{car.Id}' is not below the counter {nextCar}.");
            }

            if (!carMap.TryAdd(car.Id, car))
            {
                throw new InvalidOperationException($"Car '{car.Id}' appears twice.");
            }
        }

        var fits = new SortedDictionary<string, DriverFitProfile>(StringComparer.Ordinal);
        foreach (var profile in profiles)
        {
            ArgumentNullException.ThrowIfNull(profile);
            if (!fits.TryAdd(profile.Person.Value, profile))
            {
                throw new InvalidOperationException($"Person '{profile.Person}' has two fit records.");
            }
        }

        return new CarsSection(nextCar, nextCeilingDraw, carMap, fits);
    }

    public CarsSection Add(TeamCar car)
    {
        ArgumentNullException.ThrowIfNull(car);
        if (car.Id != CarIds.Format(NextCar))
        {
            throw new InvalidOperationException($"The next car id is {CarIds.Format(NextCar)}.");
        }

        var cars = new SortedDictionary<string, TeamCar>(_cars, StringComparer.Ordinal) { [car.Id] = car };
        return new CarsSection(NextCar + 1, NextCeilingDraw, cars, _fits);
    }

    public CarsSection Replace(TeamCar car)
    {
        ArgumentNullException.ThrowIfNull(car);
        if (!_cars.ContainsKey(car.Id))
        {
            throw new InvalidOperationException($"Car '{car.Id}' is not in the section.");
        }

        var cars = new SortedDictionary<string, TeamCar>(_cars, StringComparer.Ordinal) { [car.Id] = car };
        return new CarsSection(NextCar, NextCeilingDraw, cars, _fits);
    }

    public CarsSection AddProfile(DriverFitProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (_fits.ContainsKey(profile.Person.Value))
        {
            throw new InvalidOperationException($"Person '{profile.Person}' already has a fit record.");
        }

        var fits = new SortedDictionary<string, DriverFitProfile>(_fits, StringComparer.Ordinal)
        {
            [profile.Person.Value] = profile,
        };
        return new CarsSection(NextCar, NextCeilingDraw, _cars, fits);
    }

    public CarsSection AdvanceCeilingDraw() => new(NextCar, NextCeilingDraw + 1, _cars, _fits);

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Number("next-car", NextCar);
        writer.Number("next-draw", NextCeilingDraw);
        writer.Count("cars", _cars.Count);
        foreach (var car in _cars.Values)
        {
            writer.TextLine("car", car.Id);
            writer.TextLine("organization", car.Organization.Value);
            writer.Number("season", car.Season);
            writer.Line(
                "axes "
                + Milli(car.Concept.Aero) + " "
                + Milli(car.Concept.Philosophy) + " "
                + Milli(car.Concept.Window) + " "
                + Milli(car.Concept.Cooling) + " "
                + Milli(car.Concept.TyreKindness) + " "
                + Milli(car.Concept.Integration));
            writer.Line(
                "levels "
                + Milli(car.Levels.Power) + " "
                + Milli(car.Levels.Downforce) + " "
                + Milli(car.Levels.MechanicalGrip) + " "
                + Milli(car.Levels.Braking) + " "
                + Milli(car.Levels.Reliability));
            writer.Line("ceiling " + Milli(car.ConceptCeiling));
            writer.Line("understanding " + Milli(car.Understanding));
            writer.Line("wear " + Milli(car.TyreWearMultiplier));
            writer.Number("cost", car.SupplierChangeCost);
            writer.TextLine("engine", car.EngineKey ?? "-");
            writer.TextLine("driver", car.Driver is PersonId driver ? driver.Value : "-");
        }

        writer.Count("fits", _fits.Count);
        foreach (var fit in _fits.Values)
        {
            writer.TextLine("fit", fit.Person.Value);
            writer.Line(
                "prefs "
                + Milli(fit.Preferences.Balance) + " "
                + Milli(fit.Preferences.Traction) + " "
                + fit.Preferences.Braking);
            writer.Line(
                "experience "
                + fit.Experience.Starts.ToString(CultureInfo.InvariantCulture) + " "
                + fit.Experience.WetRaces.ToString(CultureInfo.InvariantCulture) + " "
                + fit.Experience.SeasonsWithTeam.ToString(CultureInfo.InvariantCulture));
            writer.Count("laps", fit.Experience.LapsByTrack.Count);
            foreach (var lap in fit.Experience.LapsByTrack)
            {
                writer.TextNumber("lap", lap.TrackId, lap.Laps);
            }
        }
    }

    private static string Milli(double value) =>
        CarEstimates.Milli(value).ToString(CultureInfo.InvariantCulture);
}
