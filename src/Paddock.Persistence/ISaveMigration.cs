using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// One forward schema step. Versions are contiguous and start at 1.
/// <see cref="Apply"/> runs inside the caller's transaction and must not commit or roll it back.
/// </summary>
public interface ISaveMigration
{
    int Version { get; }

    void Apply(SqliteConnection connection, SqliteTransaction transaction);
}
