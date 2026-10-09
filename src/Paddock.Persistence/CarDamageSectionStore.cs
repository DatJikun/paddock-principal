using Microsoft.Data.Sqlite;
using Paddock.Domain.Cars;
using Paddock.Domain.World;
using static Paddock.Persistence.SectionRows;

namespace Paddock.Persistence;

/// <summary>Saves the <c>damage</c> section into the tables made by <see cref="V033_CarDamage"/>.</summary>
public sealed class CarDamageSectionStore : ISectionStore
{
    public string SectionName => CarDamageSection.SectionName;

    public int SchemaVersion => CarDamageSection.Empty.SchemaVersion;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var damage = section switch
        {
            null => null,
            CarDamageSection typed => typed,
            _ => throw new ArgumentException($"The damage store cannot save a {section.GetType().Name}.", nameof(section)),
        };

        if (damage is null && !TableExists(connection, transaction, "car_damages"))
        {
            return;
        }

        Run(connection, transaction, "DELETE FROM car_damages");
        Run(connection, transaction, "DELETE FROM car_spares");
        if (damage is null)
        {
            return;
        }

        foreach (var item in damage.Damages)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO car_damages (car_id, organization_id, kind, source, damaged_on, ready_on, cost_cents) "
                + "VALUES ($car, $org, $kind, $source, $on, $ready, $cost)",
                ("$car", item.CarId),
                ("$org", item.Organization.Value),
                ("$kind", item.Kind.ToString()),
                ("$source", item.Source.ToString()),
                ("$on", item.DamagedOn.ToString()),
                ("$ready", item.ReadyOn.ToString()),
                ("$cost", item.CostCents));
        }

        foreach (var spare in damage.Spares)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO car_spares (organization_id, season, aero, philosophy, window_axis, cooling, tyre, integration, "
                + "power, downforce, grip, braking, reliability) "
                + "VALUES ($org, $season, $aero, $philosophy, $window, $cooling, $tyre, $integration, $power, $downforce, $grip, $braking, $reliability)",
                ("$org", spare.Organization.Value),
                ("$season", (long)spare.Season),
                ("$aero", (long)CarEstimates.Milli(spare.Concept.Aero)),
                ("$philosophy", (long)CarEstimates.Milli(spare.Concept.Philosophy)),
                ("$window", (long)CarEstimates.Milli(spare.Concept.Window)),
                ("$cooling", (long)CarEstimates.Milli(spare.Concept.Cooling)),
                ("$tyre", (long)CarEstimates.Milli(spare.Concept.TyreKindness)),
                ("$integration", (long)CarEstimates.Milli(spare.Concept.Integration)),
                ("$power", (long)CarEstimates.Milli(spare.Levels.Power)),
                ("$downforce", (long)CarEstimates.Milli(spare.Levels.Downforce)),
                ("$grip", (long)CarEstimates.Milli(spare.Levels.MechanicalGrip)),
                ("$braking", (long)CarEstimates.Milli(spare.Levels.Braking)),
                ("$reliability", (long)CarEstimates.Milli(spare.Levels.Reliability)));
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion != 1)
        {
            throw new InvalidDataException($"The stored damage section has schema version {storedSchemaVersion}, which this build cannot read.");
        }

        var damages = new List<CarDamage>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT car_id, organization_id, kind, source, damaged_on, ready_on, cost_cents FROM car_damages ORDER BY car_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                damages.Add(new CarDamage(
                    reader.GetString(0),
                    OrganizationIdFrom(reader.GetString(1)),
                    ParseEnum<DamageKind>(reader.GetString(2)),
                    ParseEnum<DamageSource>(reader.GetString(3)),
                    ParseDate(reader.GetString(4)),
                    ParseDate(reader.GetString(5)),
                    reader.GetInt64(6)));
            }
        }

        var spares = new List<SpareChassis>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT organization_id, season, aero, philosophy, window_axis, cooling, tyre, integration, power, downforce, grip, braking, reliability "
                + "FROM car_spares ORDER BY organization_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                double Milli(int ordinal) => CarEstimates.FromMilli((int)reader.GetInt64(ordinal));
                spares.Add(new SpareChassis(
                    OrganizationIdFrom(reader.GetString(0)),
                    (int)reader.GetInt64(1),
                    new CarConcept(Milli(2), Milli(3), Milli(4), Milli(5), Milli(6), Milli(7)),
                    PerformanceLevels.Of(Milli(8), Milli(9), Milli(10), Milli(11), Milli(12))));
            }
        }

        try
        {
            return CarDamageSection.Restore(damages, spares);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            throw new InvalidDataException("The stored damage section is not valid.", exception);
        }
    }
}
