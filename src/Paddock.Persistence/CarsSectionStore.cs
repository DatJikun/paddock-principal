using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.Cars;
using Paddock.Domain.World;

namespace Paddock.Persistence;

/// <summary>Saves the <c>cars</c> section into the tables made by <see cref="V011_CarsSection"/>.</summary>
public sealed class CarsSectionStore : ISectionStore
{
    public string SectionName => CarsSection.SectionName;

    public int SchemaVersion => CarsSection.Empty.SchemaVersion;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var cars = section switch
        {
            null => null,
            CarsSection typed => typed,
            _ => throw new ArgumentException($"The cars store cannot save a {section.GetType().Name}.", nameof(section)),
        };

        foreach (var table in new[] { "driver_track_laps", "driver_fits", "cars", "cars_counter" })
        {
            Run(connection, transaction, "DELETE FROM " + table, []);
        }

        if (cars is null)
        {
            return;
        }

        Run(
            connection,
            transaction,
            "INSERT INTO cars_counter (id, next_car, next_draw) VALUES (1, $next, $draw)",
            [("$next", cars.NextCar), ("$draw", cars.NextCeilingDraw)]);
        foreach (var car in cars.Cars)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO cars (number, organization_id, season, aero, philosophy, window_axis, cooling, tyre, integration, "
                + "power, downforce, grip, braking, reliability, ceiling, understanding, wear, supplier_cost, engine_key, driver_id) "
                + "VALUES ($number, $org, $season, $aero, $philosophy, $window, $cooling, $tyre, $integration, "
                + "$power, $downforce, $grip, $braking, $reliability, $ceiling, $understanding, $wear, $cost, $engine, $driver)",
                [
                    ("$number", CarIds.Require(car.Id)),
                    ("$org", car.Organization.Value),
                    ("$season", car.Season),
                    ("$aero", CarEstimates.Milli(car.Concept.Aero)),
                    ("$philosophy", CarEstimates.Milli(car.Concept.Philosophy)),
                    ("$window", CarEstimates.Milli(car.Concept.Window)),
                    ("$cooling", CarEstimates.Milli(car.Concept.Cooling)),
                    ("$tyre", CarEstimates.Milli(car.Concept.TyreKindness)),
                    ("$integration", CarEstimates.Milli(car.Concept.Integration)),
                    ("$power", CarEstimates.Milli(car.Levels.Power)),
                    ("$downforce", CarEstimates.Milli(car.Levels.Downforce)),
                    ("$grip", CarEstimates.Milli(car.Levels.MechanicalGrip)),
                    ("$braking", CarEstimates.Milli(car.Levels.Braking)),
                    ("$reliability", CarEstimates.Milli(car.Levels.Reliability)),
                    ("$ceiling", CarEstimates.Milli(car.ConceptCeiling)),
                    ("$understanding", CarEstimates.Milli(car.Understanding)),
                    ("$wear", CarEstimates.Milli(car.TyreWearMultiplier)),
                    ("$cost", car.SupplierChangeCost),
                    ("$engine", car.EngineKey),
                    ("$driver", car.Driver is PersonId driver ? driver.Value : null),
                ]);
        }

        foreach (var fit in cars.Profiles)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO driver_fits (person_id, balance, traction, braking_style, starts, wet_races, seasons_with_team) "
                + "VALUES ($person, $balance, $traction, $style, $starts, $wet, $seasons)",
                [
                    ("$person", fit.Person.Value),
                    ("$balance", CarEstimates.Milli(fit.Preferences.Balance)),
                    ("$traction", CarEstimates.Milli(fit.Preferences.Traction)),
                    ("$style", fit.Preferences.Braking.ToString()),
                    ("$starts", fit.Experience.Starts),
                    ("$wet", fit.Experience.WetRaces),
                    ("$seasons", fit.Experience.SeasonsWithTeam),
                ]);
            foreach (var lap in fit.Experience.LapsByTrack)
            {
                Run(
                    connection,
                    transaction,
                    "INSERT INTO driver_track_laps (person_id, track_id, laps) VALUES ($person, $track, $laps)",
                    [("$person", fit.Person.Value), ("$track", lap.TrackId), ("$laps", lap.Laps)]);
            }
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion != 1)
        {
            throw new InvalidDataException($"The stored cars section has schema version {storedSchemaVersion}, which this build cannot read.");
        }

        long nextCar;
        long nextDraw;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT next_car, next_draw FROM cars_counter WHERE id = 1";
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidDataException("The cars section is registered but its counter is missing.");
            }

            nextCar = reader.GetInt64(0);
            nextDraw = reader.GetInt64(1);
        }

        var laps = new Dictionary<string, List<TrackLaps>>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT person_id, track_id, laps FROM driver_track_laps ORDER BY person_id, track_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var person = reader.GetString(0);
                if (!laps.TryGetValue(person, out var list))
                {
                    list = [];
                    laps.Add(person, list);
                }

                list.Add(new TrackLaps(reader.GetString(1), (int)reader.GetInt64(2)));
            }
        }

        var profiles = new List<DriverFitProfile>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT person_id, balance, traction, braking_style, starts, wet_races, seasons_with_team FROM driver_fits ORDER BY person_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var person = PersonFrom(reader.GetString(0));
                var style = Enum.Parse<BrakingStyle>(reader.GetString(3));
                var trackLaps = laps.TryGetValue(person.Value, out var list) ? list : [];
                profiles.Add(new DriverFitProfile(
                    person,
                    new DriverHandlingPreferences(CarEstimates.FromMilli((int)reader.GetInt64(1)), CarEstimates.FromMilli((int)reader.GetInt64(2)), style),
                    new DriverExperience((int)reader.GetInt64(4), (int)reader.GetInt64(5), (int)reader.GetInt64(6), trackLaps)));
            }
        }

        var cars = new List<TeamCar>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT number, organization_id, season, aero, philosophy, window_axis, cooling, tyre, integration,
                       power, downforce, grip, braking, reliability, ceiling, understanding, wear, supplier_cost, engine_key, driver_id
                FROM cars ORDER BY number
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var number = reader.GetInt64(0);
                PersonId? driver = reader.IsDBNull(19) ? null : PersonFrom(reader.GetString(19));
                cars.Add(new TeamCar(
                    CarIds.Format(number),
                    OrganizationFrom(reader.GetString(1)),
                    (int)reader.GetInt64(2),
                    new CarConcept(
                        Milli(reader, 3),
                        Milli(reader, 4),
                        Milli(reader, 5),
                        Milli(reader, 6),
                        Milli(reader, 7),
                        Milli(reader, 8)),
                    PerformanceLevels.Of(Milli(reader, 9), Milli(reader, 10), Milli(reader, 11), Milli(reader, 12), Milli(reader, 13)),
                    Milli(reader, 14),
                    Milli(reader, 15),
                    Milli(reader, 16),
                    (int)reader.GetInt64(17),
                    reader.IsDBNull(18) ? null : reader.GetString(18),
                    driver));
            }
        }

        try
        {
            return CarsSection.Restore(nextCar, nextDraw, cars, profiles);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            throw new InvalidDataException("The stored cars section is not valid.", exception);
        }
    }

    private static double Milli(SqliteDataReader reader, int ordinal) =>
        CarEstimates.FromMilli((int)reader.GetInt64(ordinal));

    private static PersonId PersonFrom(string text)
    {
        var id = text.StartsWith("gen:", StringComparison.Ordinal)
            ? PersonId.Generated(ParseSequence(text, "gen:"))
            : PersonId.Real(text);
        return string.Equals(id.Value, text, StringComparison.Ordinal)
            ? id
            : throw new InvalidDataException($"Id '{text}' is not canonical.");
    }

    private static OrganizationId OrganizationFrom(string text)
    {
        var id = text.StartsWith("org:", StringComparison.Ordinal)
            ? OrganizationId.Generated(ParseSequence(text, "org:"))
            : OrganizationId.Real(text);
        return string.Equals(id.Value, text, StringComparison.Ordinal)
            ? id
            : throw new InvalidDataException($"Id '{text}' is not canonical.");
    }

    private static long ParseSequence(string text, string prefix)
    {
        var tail = text.AsSpan(prefix.Length);
        if (tail.Length == 0 || (tail.Length > 1 && tail[0] == '0')
            || !long.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) || sequence < 1)
        {
            throw new InvalidDataException($"Id '{text}' has a malformed sequence.");
        }

        return sequence;
    }

    private static void Run(SqliteConnection connection, SqliteTransaction transaction, string sql, (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        command.ExecuteNonQuery();
    }
}
