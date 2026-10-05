using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.Racing;
using Paddock.Domain.World;
using static Paddock.Persistence.SectionRows;

namespace Paddock.Persistence;

/// <summary>Saves the <c>championship</c> section into the tables made by <see cref="V020_RaceSections"/>.</summary>
public sealed class ChampionshipSectionStore : ISectionStore
{
    public string SectionName => ChampionshipSection.SectionName;

    public int SchemaVersion => 1;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var championship = section switch
        {
            null => null,
            ChampionshipSection typed => typed,
            _ => throw new ArgumentException("The championship store cannot save a " + section.GetType().Name + ".", nameof(section)),
        };

        if (championship is null && !TableExists(connection, transaction, "championship_meta"))
        {
            return;
        }

        Run(connection, transaction, "DELETE FROM championship_rows");
        Run(connection, transaction, "DELETE FROM championship_meta");
        if (championship is null)
        {
            return;
        }

        Run(
            connection,
            transaction,
            "INSERT INTO championship_meta (singleton, season, total_rounds, rounds_completed, settled) VALUES (1, $season, $total, $done, $settled)",
            ("$season", championship.Season),
            ("$total", championship.TotalRounds),
            ("$done", championship.RoundsCompleted),
            ("$settled", championship.Settled ? 1 : 0));
        foreach (var row in championship.Drivers)
        {
            Insert(connection, transaction, "driver", row);
        }

        foreach (var row in championship.Constructors)
        {
            Insert(connection, transaction, "constructor", row);
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion != SchemaVersion)
        {
            throw new InvalidDataException("Championship section schema " + storedSchemaVersion.ToString(CultureInfo.InvariantCulture) + " is not readable.");
        }

        using var meta = connection.CreateCommand();
        meta.CommandText = "SELECT season, total_rounds, rounds_completed, settled FROM championship_meta";
        using var reader = meta.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidDataException("The championship section is registered but has no meta row.");
        }

        var season = reader.GetInt32(0);
        var total = reader.GetInt32(1);
        var done = reader.GetInt32(2);
        var settled = reader.GetInt32(3) == 1;
        reader.Close();

        var drivers = new List<ChampionshipLedger>();
        var constructors = new List<ChampionshipLedger>();
        using var rows = connection.CreateCommand();
        rows.CommandText = "SELECT kind, entry_id, round_points, positions FROM championship_rows ORDER BY kind, entry_id";
        using var body = rows.ExecuteReader();
        while (body.Read())
        {
            var ledger = new ChampionshipLedger(body.GetString(1), ParseDecimals(body.GetString(2)), ParseInts(body.GetString(3)));
            if (body.GetString(0) == "driver")
            {
                drivers.Add(ledger);
            }
            else
            {
                constructors.Add(ledger);
            }
        }

        return ChampionshipSection.Create(season, total, done, settled, drivers, constructors);
    }

    private static void Insert(SqliteConnection connection, SqliteTransaction transaction, string kind, ChampionshipLedger row)
    {
        Run(
            connection,
            transaction,
            "INSERT INTO championship_rows (kind, entry_id, round_points, positions) VALUES ($kind, $id, $points, $positions)",
            ("$kind", kind),
            ("$id", row.Id),
            ("$points", string.Join(',', row.RoundPoints.Select(point => point.ToString(CultureInfo.InvariantCulture)))),
            ("$positions", string.Join(',', row.Positions.Select(position => position.ToString(CultureInfo.InvariantCulture)))));
    }

    private static decimal[] ParseDecimals(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        var parts = text.Split(',');
        var values = new decimal[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            values[i] = decimal.Parse(parts[i], CultureInfo.InvariantCulture);
        }

        return values;
    }

    private static int[] ParseInts(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        var parts = text.Split(',');
        var values = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            values[i] = int.Parse(parts[i], CultureInfo.InvariantCulture);
        }

        return values;
    }
}
