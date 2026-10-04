using Microsoft.Data.Sqlite;
using Paddock.Domain.World;

namespace Paddock.Persistence;

/// <summary>
/// How one world section is saved and loaded. The pattern for adding a section:
/// <list type="number">
/// <item>Write the section in Domain as an <see cref="IWorldSection"/> (immutable, with a canonical text).</item>
/// <item>Add a migration that creates its tables, one table per collection (<see cref="V005_InboxSection"/> is the example).
/// Never edit an earlier migration.</item>
/// <item>Write a store: <see cref="Replace"/> deletes the section's rows and writes the given section; <see cref="Load"/>
/// reads them back through the section's own restore method, which checks them.</item>
/// <item>Add the store to <see cref="SectionStores.Production"/>.</item>
/// </list>
/// Both methods run inside the transaction or connection of <see cref="WorldRepository"/>, so a section is saved or not
/// saved together with the rest of the world, and they must neither commit nor roll back. Every section the repository
/// is asked to save must have a store, otherwise the save is refused: data is never dropped silently. A section that was
/// saved and has no store when loading makes the load fail for the same reason.
/// When the stored rows of an older <see cref="IWorldSection.SchemaVersion"/> need converting, <see cref="Load"/> receives
/// that version and converts in memory; a migration changes the tables.
/// </summary>
public interface ISectionStore
{
    /// <summary>The <see cref="IWorldSection.Name"/> this store handles.</summary>
    string SectionName { get; }

    /// <summary>The schema version this build writes. Loading a newer one is refused.</summary>
    int SchemaVersion { get; }

    /// <summary>Deletes the stored rows and, when <paramref name="section"/> is not null, writes it.</summary>
    void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section);

    /// <summary>Reads the stored section. <paramref name="storedSchemaVersion"/> is the version recorded when it was saved.</summary>
    IWorldSection Load(SqliteConnection connection, int storedSchemaVersion);
}

/// <summary>The stores this build knows.</summary>
public static class SectionStores
{
    public static IReadOnlyList<ISectionStore> Production { get; } = Array.AsReadOnly<ISectionStore>([new InboxSectionStore(), new TalentPoolSectionStore(), new ContractsSectionStore(), new FinanceSectionStore(), new CarsSectionStore()]);
}
