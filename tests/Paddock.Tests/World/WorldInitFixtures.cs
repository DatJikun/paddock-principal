using Paddock.Data.Authored;
using Paddock.Data.World;
using Paddock.Domain.People;

namespace Paddock.Tests.World;

/// <summary>
/// Synthetic data for the world initializer tests. Every id, name and number is invented for the tests;
/// none is a historical fact. Start year of the fixture world is <see cref="Year"/>.
/// </summary>
internal static class WorldInitFixtures
{
    public const int Year = 1970;

    private const string Source = "https://example.com/fixture";

    public static AuthoredData Data()
    {
        var engines = new EnginesFile(
            "fixture",
            [],
            [
                Engine("alpha", Year, "Aurum", "Aurum V8", "works"),
                Engine("bravo", Year, "Aurum", "Aurum V8", "customer"),
                Engine("charlie", Year, "unknown", "unknown", "unknown"),
                Engine("echo", Year, "Ferro Motors", "Ferro 12", "partner"),
                Engine("alpha", Year - 1, "Aurum", "Aurum V8", "works"),
                Engine("charlie", Year - 1, "Zeta", "Zeta 4", "customer"),
                Engine("charlie", Year + 1, "Zeta", "Zeta 4", "customer"),
                Engine("delta", Year + 2, "Aurum", "Aurum V8", "customer"),
            ]);
        var lineage = new LineageFile(
            "fixture",
            [
                new ConstructorLineage(
                    "bravo-line",
                    [
                        new LineageEntry("bravo_old", 1960, 1969, "renamed", Source),
                        new LineageEntry("bravo", 1970, null, null, Source),
                    ]),
                new ConstructorLineage(
                    "echo-line",
                    [
                        new LineageEntry("echo", 1960, 1965, "sold", Source),
                        new LineageEntry("foxtrot", 1966, 1969, "bought back", Source),
                        new LineageEntry("echo", 1970, null, null, Source),
                    ]),
            ]);
        var founders = new FoundersFile(
            "fixture",
            [
                Founder("alpha-org", 1962, Year + 5, 1958, "GBR", new FounderConstructorEntry("alpha", 1962, Year + 5)),
                Founder(
                    "bravo-org",
                    1960,
                    Year + 5,
                    null,
                    "ITA",
                    new FounderConstructorEntry("bravo_old", 1960, 1969),
                    new FounderConstructorEntry("bravo", 1970, Year + 5)),
            ]);
        var staff = new StaffMember[]
        {
            Person("pat_principal", "Pat Principal", "1920-03-01", "GBR", Stint("alpha", "team_principal", 1965, 1975), Stint("alpha", "owner", 1965, 1975)),
            Person("tess_designer", "Tess Dessin", "1930-06-30", "FRA", Stint("alpha", "technical_director", 1968, 1972), Stint("alpha", "chief_designer", 1970, 1970)),
            Person("andre_de_vries", "André de Vries", null, null, Stint("bravo", "chief_designer", 1969, 1971)),
            Person("ghost_writer", "Ghost Writer", "1940-01-01", "USA", Stint("zulu", "technical_director", 1960, 1980)),
            Person("indy_guy", "Indy Guy", "1941-02-02", "USA", Stint("alpha", "technical_director", 1960, 1980, "indycar")),
            Person("engine_wiz", "Engine Wiz", "1925-05-05", "GBR", Stint("aurum", "engine_designer", 1960, 1980, "engine_supplier")),
            Person("future_guy", "Future Guy", "1950-01-01", "GBR", Stint("alpha", "technical_director", 1975, 1980)),
            Person("past_guy", "Past Guy", "1910-01-01", "GBR", Stint("bravo", "team_principal", 1950, 1969)),
            Person("wilson_junior", "Wilson Fittipaldi Júnior", "1943-12-25", "BRA", Stint("echo", "head_of_aero", 1969, 1971)),
        };
        return new AuthoredData(
            [],
            [],
            [],
            new CircuitsFile("fixture", [], []),
            [],
            [],
            engines,
            lineage,
            founders,
            staff,
            [],
            [],
            []);
    }

    public static FixtureProvider Provider() => new(
        [
            // Deliberately not in id order.
            Driver("d_ghost", "Ghost", "Team", new DateOnly(1941, 1, 1), null, "Italian", 1968, 1966, Seat(Year, "zulu", 1, "race")),
            Driver("d_racer1", "Racer", "One", new DateOnly(1940, 5, 5), null, "British", 1962, 1960, Seat(Year, "alpha", 1, "race")),
            Driver("d_racer2", "Racer", "Two", null, 1945, "Italian", 1966, 1964, Seat(Year, "bravo", 1, "race")),
            Driver("d_racer3", "Racer", "Three", new DateOnly(1946, 3, 3), null, null, 1967, 1965, Seat(Year, "alpha", 1, "race")),
            Driver("d_multi", "Multi", "Seat", new DateOnly(1944, 4, 4), null, "French", 1965, 1963, Seat(Year, "bravo", 5, "race"), Seat(Year, "alpha", 1, "race")),
            Driver("d_sub", "Sub", "Stitute", new DateOnly(1947, 7, 7), null, "British", 1969, 1967, Seat(Year, "charlie", 2, "substitute")),
            Driver("d_pool", "Pool", "Prospect", new DateOnly(1952, 8, 8), null, "British", Year + 2, Year - 1),
            Driver("d_later", "Later", "Prospect", new DateOnly(1955, 9, 9), null, "British", Year + 5, Year + 3),
            Driver("d_past", "Past", "Racer", new DateOnly(1930, 1, 1), null, "British", 1960, 1958, Seat(Year - 5, "alpha", 1, "race")),
            Driver("d_nobirth", "No", "Birth", null, null, "British", 1968, 1966, Seat(Year, "alpha", 1, "race")),
        ],
        new Dictionary<string, DriverRating>(StringComparer.Ordinal)
        {
            ["d_racer1"] = Rating(14, 17),
            ["d_racer3"] = Rating(9, 12),
            ["d_multi"] = Rating(11, 11),
            ["d_pool"] = Rating(6, 16),
        });

    public static DriverRating Rating(int current, int potential) => new(Attributes(current), Attributes(potential));

    public static DriverAttributes Attributes(int value) =>
        new(value, value, value, value, value, value, value, value, value, value, value);

    private static EngineEntry Engine(string constructor, int year, string supplier, string name, string type) =>
        new(constructor, year, supplier, name, type, "high", Source);

    private static FounderOrganization Founder(
        string id,
        int from,
        int to,
        int? founded,
        string country,
        params FounderConstructorEntry[] entries) =>
        new(id, to - from + 1, from, to, entries, founded, [], country, null, "high", Source, "fixture");

    private static StaffMember Person(string id, string name, string? born, string? nationality, params StaffCareerStint[] career) =>
        new(id, name, born, nationality, career, [], "high");

    private static StaffCareerStint Stint(string org, string role, int from, int to, string? series = null) =>
        new(org, role, from, to, Source, series);

    private static DriverSeat Seat(int season, string constructor, int firstRound, string role) =>
        new(season, constructor, firstRound, role);

    private static RealDriverRecord Driver(
        string id,
        string given,
        string family,
        DateOnly? birth,
        int? bornYear,
        string? nationality,
        int firstSeason,
        int poolEntry,
        params DriverSeat[] seats) =>
        new(id, given, family, birth, bornYear, nationality, firstSeason, poolEntry, seats);
}

internal sealed class FixtureProvider : IPeopleProvider
{
    private readonly Dictionary<string, DriverRating> _ratings;

    public FixtureProvider(IReadOnlyList<RealDriverRecord> drivers, Dictionary<string, DriverRating> ratings)
    {
        Drivers = drivers;
        _ratings = ratings;
    }

    public IReadOnlyList<RealDriverRecord> Drivers { get; }

    public int RatingCalls { get; private set; }

    public FixtureProvider Reversed() => new(Drivers.Reverse().ToArray(), _ratings);

    public DriverRating? RatingFor(string driverId, int season)
    {
        RatingCalls++;
        return _ratings.TryGetValue(driverId, out var rating) ? rating : null;
    }
}
