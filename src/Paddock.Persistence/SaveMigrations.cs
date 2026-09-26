namespace Paddock.Persistence;

public static class SaveMigrations
{
    private static readonly ISaveMigration[] Items = [new V001_Initial()];

    static SaveMigrations()
    {
        MigrationList.Validate(Items);
    }

    public static IReadOnlyList<ISaveMigration> Production { get; } = Array.AsReadOnly(Items);

    public static int CurrentVersion => Items[^1].Version;
}

internal static class MigrationList
{
    public static void Validate(IReadOnlyList<ISaveMigration> migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        if (migrations.Count == 0)
        {
            throw new ArgumentException("At least one migration is required.", nameof(migrations));
        }

        for (var i = 0; i < migrations.Count; i++)
        {
            var migration = migrations[i]
                ?? throw new ArgumentException("Migration list contains null.", nameof(migrations));
            var expected = i + 1;
            if (migration.Version != expected)
            {
                throw new ArgumentException(
                    $"Migration at index {i} has version {migration.Version}, expected {expected}. Versions must be contiguous and start at 1.",
                    nameof(migrations));
            }
        }
    }
}
