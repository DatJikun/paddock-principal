using System.Globalization;
using Paddock.Data.Historical;
using Paddock.Domain.People;

namespace Paddock.Data.World;

/// <summary>
/// Current attributes and hidden ceiling of a real driver for one season, as the ratings model (not part of
/// the world initializer) would supply them.
/// </summary>
public sealed record DriverRating(DriverAttributes Current, DriverAttributes Potential);

/// <summary>
/// One season-and-constructor seat of a real driver (a T12 schedule stint, reduced to what the world needs).
/// <see cref="Starts"/> is the number of races the driver started for the constructor that season.
/// </summary>
public sealed record DriverSeat(int Season, string ConstructorId, int FirstRound, string Role, int Starts = 0);

/// <summary>
/// A real driver as the schedule knows them. <see cref="PoolEntryYear"/> is the year the driver enters the
/// talent pool (DESIGN section 2.1) and <see cref="FirstSeason"/> the debut season.
/// </summary>
public sealed record RealDriverRecord(
    string DriverId,
    string GivenName,
    string FamilyName,
    DateOnly? BirthDate,
    int? BornYear,
    string? Nationality,
    int? FirstSeason,
    int? PoolEntryYear,
    IReadOnlyList<DriverSeat> Seats);

/// <summary>
/// Source of real drivers for <see cref="WorldInitializer"/>. Implemented from the T12 people schedule by
/// <see cref="ScheduleBackedPeopleProvider"/>; tests use small fixtures. Both members are queries:
/// they must not change state or consume RNG (INV-005).
/// </summary>
public interface IPeopleProvider
{
    /// <summary>Every real driver the provider knows, in any order. The initializer sorts by id.</summary>
    IReadOnlyList<RealDriverRecord> Drivers { get; }

    /// <summary>The rating of a driver for a season, or null when there is none.</summary>
    DriverRating? RatingFor(string driverId, int season);
}

/// <summary>A provider with no drivers. Use it when the local Jolpica cache has not been built.</summary>
public sealed class EmptyPeopleProvider : IPeopleProvider
{
    public static EmptyPeopleProvider Instance { get; } = new();

    public IReadOnlyList<RealDriverRecord> Drivers => [];

    public DriverRating? RatingFor(string driverId, int season) => null;
}

/// <summary>
/// Joins the T12 <see cref="PeopleScheduleReport"/> with the normalized driver table (names, birth date,
/// nationality). Ratings come from an optional callback because the ratings model output is a separate task.
/// A scheduled driver with no row in the driver table is left out, because the world needs a name.
/// Jolpica gives some drivers the same id as a constructor (Bruce McLaren is <c>mclaren</c>, John Surtees <c>surtees</c>).
/// One string is one id in the world (INV-009), so such a driver is renamed to <c>driver:{id}</c> here, once, for every
/// consumer of the provider. A constructor id is any id in a seat of the schedule or in <c>organizationIds</c>
/// (the normalized constructor table). <see cref="RatingFor"/> takes the renamed id.
/// </summary>
public sealed class ScheduleBackedPeopleProvider : IPeopleProvider
{
    public const string RenamedDriverPrefix = "driver:";

    private readonly Func<string, int, DriverRating?>? _ratings;
    private readonly Dictionary<string, string> _sourceIds = new(StringComparer.Ordinal);

    public ScheduleBackedPeopleProvider(
        PeopleScheduleReport schedule,
        IReadOnlyList<HistoricalDriver> drivers,
        Func<string, int, DriverRating?>? ratings = null,
        IEnumerable<string>? organizationIds = null)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(drivers);
        _ratings = ratings;

        var constructors = new HashSet<string>(organizationIds ?? [], StringComparer.Ordinal);
        constructors.UnionWith(schedule.Drivers.SelectMany(driver => driver.Stints).Select(stint => stint.ConstructorId));

        var table = new Dictionary<string, HistoricalDriver>(StringComparer.Ordinal);
        foreach (var driver in drivers)
        {
            table[driver.DriverId] = driver;
        }

        var records = new List<RealDriverRecord>();
        foreach (var scheduled in schedule.Drivers)
        {
            if (!table.TryGetValue(scheduled.DriverId, out var row))
            {
                continue;
            }

            var seats = scheduled.Stints
                .Select(stint => new DriverSeat(stint.Season, stint.ConstructorId, stint.FirstRound, stint.Role, stint.Starts))
                .ToArray();
            var id = scheduled.DriverId;
            if (constructors.Contains(id))
            {
                id = RenamedDriverPrefix + id;
                _sourceIds[id] = scheduled.DriverId;
            }

            records.Add(new RealDriverRecord(
                id,
                row.GivenName,
                row.FamilyName,
                ParseDate(row.DateOfBirth),
                scheduled.Born ?? PeopleScheduleRules.BornYear(row.DateOfBirth),
                row.Nationality ?? scheduled.Nationality,
                scheduled.FirstSeason,
                scheduled.PoolEntryYear,
                seats));
        }

        Drivers = records;
    }

    public IReadOnlyList<RealDriverRecord> Drivers { get; }

    public DriverRating? RatingFor(string driverId, int season) =>
        _ratings?.Invoke(_sourceIds.GetValueOrDefault(driverId, driverId), season);

    private static DateOnly? ParseDate(string? text)
    {
        if (text is not null
            && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        return null;
    }
}
