using Paddock.Persistence;

namespace Paddock.Tests.Persistence;

/// <summary>V022 widens the status check of <c>objectives</c> without losing the rows or their effect arguments (#212).</summary>
public sealed class ObjectiveWithdrawnMigrationTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-v22-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void AV021SaveKeepsItsObjectivesAndEffectArgumentsAndNowAcceptsWithdrawn()
    {
        var path = Path.Combine(_directory, "v21.paddock");
        using (var created = SaveFile.Create(path, WorldFixtures.Meta(), [.. SaveMigrations.Production.Take(21)]))
        {
            Assert.Equal(21, created.ReadMeta().SchemaVersion);
            Exec(created, Objective(1, "Open") + Objective(2, "Met"));
            Exec(created, """
                INSERT INTO objective_effect_arguments (objective_number, effect, name, value) VALUES
                    (1, 'met', 'amount', '5000'), (1, 'failed', 'amount', '-2000'), (2, 'met', 'amount', '700');
                """);
            Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => Exec(created, Objective(3, "Withdrawn")));
        }

        using var opened = SaveFile.Open(path);
        Assert.Equal(SaveMigrations.CurrentVersion, opened.ReadMeta().SchemaVersion);
        Assert.Equal(2L, Scalar(opened, "SELECT COUNT(*) FROM objectives"));
        Assert.Equal(3L, Scalar(opened, "SELECT COUNT(*) FROM objective_effect_arguments"));
        Assert.Equal("-2000", (string)Scalar(opened, "SELECT value FROM objective_effect_arguments WHERE objective_number = 1 AND effect = 'failed' AND name = 'amount'"));
        Assert.Equal("Met", (string)Scalar(opened, "SELECT status FROM objectives WHERE number = 2"));

        Exec(opened, Objective(3, "Withdrawn"));
        Assert.Equal("Withdrawn", (string)Scalar(opened, "SELECT status FROM objectives WHERE number = 3"));

        // The foreign key still cascades: removing an objective takes its arguments with it.
        Exec(opened, "PRAGMA foreign_keys = ON; DELETE FROM objectives WHERE number = 2;");
        Assert.Equal(2L, Scalar(opened, "SELECT COUNT(*) FROM objective_effect_arguments"));
    }

    private static string Objective(int number, string status) => $"""
        INSERT INTO objectives (number, owner_id, grantor_id, kind_key, reason_key, predicate_name, predicate_parameter, baseline,
            created_on, deadline_on, met_key, failed_key, status, settled_on)
        VALUES ({number}, 'team:a', 'board:a', 'objective.kind', 'objective.reason', 'Predicate', '3', NULL,
            '1950-01-01', '1950-12-31', 'met.key', 'failed.key', '{status}', NULL);
        """;

    private static object Scalar(SaveFile save, string sql)
    {
        using var command = save.Connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar()!;
    }

    private static void Exec(SaveFile save, string sql)
    {
        using var command = save.Connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
