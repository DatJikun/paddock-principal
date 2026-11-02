using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.Racing;
using Paddock.Domain.World;
using static Paddock.Persistence.SectionRows;

namespace Paddock.Persistence;

/// <summary>Saves the <c>race-weekend</c> section into the tables made by <see cref="V035_RaceWeekend"/>.</summary>
public sealed class RaceWeekendSectionStore : ISectionStore
{
    public string SectionName => RaceWeekendSection.SectionName;

    public int SchemaVersion => 1;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var weekend = section switch
        {
            null => null,
            RaceWeekendSection typed => typed,
            _ => throw new ArgumentException("The race-weekend store cannot save a " + section.GetType().Name + ".", nameof(section)),
        };

        if (weekend is null && !TableExists(connection, transaction, "race_weekend_meta"))
        {
            return;
        }

        Run(connection, transaction, "DELETE FROM race_weekend_orders");
        Run(connection, transaction, "DELETE FROM race_weekend_plans");
        Run(connection, transaction, "DELETE FROM race_weekend_meta");
        if (weekend is null)
        {
            return;
        }

        Run(
            connection,
            transaction,
            "INSERT INTO race_weekend_meta (singleton, season, round, watched_hash) VALUES (1, $season, $round, $hash)",
            ("$season", (long)weekend.Season),
            ("$round", (long)weekend.Round),
            ("$hash", weekend.WatchedTapeHash ?? ""));
        foreach (var plan in weekend.Plans)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO race_weekend_plans (car_id, team_id, start_compound, pace, engine, stops) VALUES ($car, $team, $start, $pace, $engine, $stops)",
                ("$car", plan.CarId),
                ("$team", plan.TeamId),
                ("$start", plan.StartCompoundId ?? ""),
                ("$pace", plan.Pace ?? ""),
                ("$engine", plan.Engine),
                ("$stops", RaceWeekendSection.StopsText(plan.Stops)));
        }

        var sequence = 0L;
        foreach (var order in weekend.Orders)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO race_weekend_orders (sequence, manager_id, at_ms, car_id, lap, kind, pace, compound, engine, on_flag) "
                + "VALUES ($seq, $manager, $at, $car, $lap, $kind, $pace, $compound, $engine, $on)",
                ("$seq", sequence++),
                ("$manager", order.ManagerId),
                ("$at", order.AtMs),
                ("$car", order.CarId),
                ("$lap", (long)order.Lap),
                ("$kind", order.Kind),
                ("$pace", order.Pace),
                ("$compound", order.CompoundId ?? ""),
                ("$engine", order.Engine),
                ("$on", order.On ? 1L : 0L));
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion != SchemaVersion)
        {
            throw new InvalidDataException("Race-weekend section schema " + storedSchemaVersion.ToString(CultureInfo.InvariantCulture) + " is not readable.");
        }

        using var meta = connection.CreateCommand();
        meta.CommandText = "SELECT season, round, watched_hash FROM race_weekend_meta";
        using var head = meta.ExecuteReader();
        if (!head.Read())
        {
            throw new InvalidDataException("The race-weekend section is registered but has no meta row.");
        }

        var season = checked((int)head.GetInt64(0));
        var round = checked((int)head.GetInt64(1));
        var hash = head.GetString(2);
        head.Close();

        var plans = new List<WeekendPlan>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT car_id, team_id, start_compound, pace, engine, stops FROM race_weekend_plans ORDER BY car_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                plans.Add(new WeekendPlan(
                    reader.GetString(0),
                    reader.GetString(1),
                    Optional(reader.GetString(2)),
                    Optional(reader.GetString(3)),
                    reader.GetString(4),
                    RaceWeekendSection.ParseStops(reader.GetString(5))));
            }
        }

        var orders = new List<WeekendOrder>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT manager_id, at_ms, car_id, lap, kind, pace, compound, engine, on_flag FROM race_weekend_orders ORDER BY sequence";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                orders.Add(new WeekendOrder(
                    reader.GetString(0),
                    reader.GetInt64(1),
                    reader.GetString(2),
                    checked((int)reader.GetInt64(3)),
                    reader.GetString(4),
                    reader.GetString(5),
                    Optional(reader.GetString(6)),
                    reader.GetString(7),
                    reader.GetInt64(8) == 1));
            }
        }

        return RaceWeekendSection.Create(season, round, Optional(hash), plans, orders);
    }

    private static string? Optional(string text) => text.Length == 0 ? null : text;
}
