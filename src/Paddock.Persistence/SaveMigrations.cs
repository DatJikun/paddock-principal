namespace Paddock.Persistence;

public static class SaveMigrations
{
    // One migration per line, in version order: a system that adds a migration adds one line at the end, so two parallel branches
    // conflict only when they pick the same number, which MigrationList.Validate catches anyway.
    private static readonly ISaveMigration[] Items =
    [
        new V001_Initial(),
        new V002_CareerConfig(),
        new V003_WorldEntities(),
        new V004_WorldSections(),
        new V005_InboxSection(),
        new V006_PersonRetirement(),
        new V007_CareerRun(),
        new V008_TalentPoolSection(),
        new V009_ContractsSection(),
        new V010_FinanceSection(),
        new V011_CarsSection(),
        new V012_SponsorsAndObjectives(),
        new V013_BoardSection(),
        new V014_ContractActivity(),
        new V015_SupplySection(),
        new V016_DevelopmentSection(),
        new V017_ConceptProduction(),
        new V018_PrincipalsSection(),
        new V019_RaisesSection(),
        new V020_RaceSections(),
        new V021_StaffSection(),
        new V022_RaceResultsSection(),
    ];

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
