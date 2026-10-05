using Microsoft.Data.Sqlite;
using Paddock.Domain.Infrastructure;
using Paddock.Domain.World;
using static Paddock.Persistence.SectionRows;

namespace Paddock.Persistence;

/// <summary>Saves the <c>infrastructure</c> section into the tables made by <see cref="V027_InfrastructureSection"/>.</summary>
public sealed class InfrastructureSectionStore : ISectionStore
{
    public string SectionName => InfrastructureSection.SectionName;

    public int SchemaVersion => InfrastructureSection.Empty.SchemaVersion;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var infrastructure = section switch
        {
            null => null,
            InfrastructureSection typed => typed,
            _ => throw new ArgumentException($"The infrastructure store cannot save a {section.GetType().Name}.", nameof(section)),
        };

        if (infrastructure is null
            && !TableExists(connection, transaction, "infrastructure_facilities")
            && !TableExists(connection, transaction, "infrastructure_tests"))
        {
            return;
        }

        Run(connection, transaction, "DELETE FROM infrastructure_facilities");
        Run(connection, transaction, "DELETE FROM infrastructure_tests");
        if (infrastructure is null)
        {
            return;
        }

        foreach (var facility in infrastructure.Facilities)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO infrastructure_facilities (organization_id, kind, quality_milli, build_started, build_ends, target_quality_milli, cost_cents) "
                + "VALUES ($org, $kind, $quality, $started, $ends, $target, $cost)",
                ("$org", facility.Organization.Value),
                ("$kind", facility.Kind.ToString()),
                ("$quality", (long)facility.QualityMilli),
                ("$started", facility.BuildStarted?.ToString()),
                ("$ends", facility.BuildEnds?.ToString()),
                ("$target", facility.TargetQualityMilli is { } target ? (long)target : null),
                ("$cost", facility.CostCents));
        }

        foreach (var test in infrastructure.Tests)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO infrastructure_tests (organization_id, booked_on, cost_cents) VALUES ($org, $on, $cost)",
                ("$org", test.Organization.Value),
                ("$on", test.Date.ToString()),
                ("$cost", test.CostCents));
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion != 1)
        {
            throw new InvalidDataException($"Infrastructure schema {storedSchemaVersion} is newer than this build.");
        }

        var facilities = new List<Facility>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT organization_id, kind, quality_milli, build_started, build_ends, target_quality_milli, cost_cents
                FROM infrastructure_facilities
                ORDER BY organization_id, kind
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var target = reader.IsDBNull(5) ? (int?)null : (int)reader.GetInt64(5);
                facilities.Add(new Facility(
                    OrganizationIdFrom(reader.GetString(0)),
                    ParseEnum<FacilityKind>(reader.GetString(1)),
                    (int)reader.GetInt64(2),
                    ParseOptionalDate(reader, 3),
                    ParseOptionalDate(reader, 4),
                    target,
                    reader.GetInt64(6)));
            }
        }

        var tests = new List<TestBooking>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT organization_id, booked_on, cost_cents
                FROM infrastructure_tests
                ORDER BY organization_id, booked_on, cost_cents
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tests.Add(new TestBooking(
                    OrganizationIdFrom(reader.GetString(0)),
                    ParseDate(reader.GetString(1)),
                    reader.GetInt64(2)));
            }
        }

        return InfrastructureSection.Restore(facilities, tests);
    }
}
