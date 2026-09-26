using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.Random;

namespace Paddock.Persistence;

/// <summary>
/// One career save: a SQLite file in WAL mode with foreign keys enabled.
/// Pending migrations run in a single transaction. A schema newer than this build is refused.
/// </summary>
public sealed class SaveFile : IDisposable
{
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";
    private const string GameDateFormat = "yyyy-MM-dd";

    private readonly SqliteConnection _connection;
    private bool _disposed;

    private SaveFile(SqliteConnection connection)
    {
        _connection = connection;
    }

    public static SaveFile Create(string path, SaveMeta meta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(meta);
        MigrationList.Validate(SaveMigrations.Production);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new IOException("Save path has no parent directory.");
        }

        if (File.Exists(fullPath))
        {
            throw new IOException($"Save file already exists: {fullPath}");
        }

        Directory.CreateDirectory(directory);
        SqliteConnection? connection = null;
        try
        {
            connection = OpenConnection(fullPath, SqliteOpenMode.ReadWriteCreate);
            Configure(connection);
            CreateSchemaAndMeta(connection, meta, SaveMigrations.Production);
            return new SaveFile(connection);
        }
        catch
        {
            connection?.Dispose();
            DeleteSaveFiles(fullPath);
            throw;
        }
    }

    public static SaveFile Open(string path) => Open(path, SaveMigrations.Production);

    internal static SaveFile Open(string path, IReadOnlyList<ISaveMigration> migrations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(migrations);
        MigrationList.Validate(migrations);

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Save file does not exist.", fullPath);
        }

        var connection = OpenConnection(fullPath, SqliteOpenMode.ReadWrite);
        try
        {
            Configure(connection);
            MigrateIfNeeded(connection, fullPath, migrations);
            return new SaveFile(connection);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public SaveMeta ReadMeta()
    {
        ThrowIfDisposed();
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT schema_version, created_at_utc, saved_at_utc, career_name, manager_name,
                   player_team_id, current_game_date, world_data_hash, master_seed
            FROM meta
            WHERE id = 1
            """;
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidDataException("Save meta row is missing.");
        }

        var meta = new SaveMeta(
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            ParseGameDate(reader.GetString(6)),
            reader.GetString(7),
            ParseMasterSeed(reader.GetString(8)))
        {
            SchemaVersion = ReadVersion(reader.GetValue(0)),
            CreatedAtUtc = ParseTimestamp(reader.GetString(1)),
            SavedAtUtc = ParseTimestamp(reader.GetString(2)),
        };

        if (reader.Read())
        {
            throw new InvalidDataException("Save meta table must contain exactly one row.");
        }

        return meta;
    }

    public void WriteRngStates(IReadOnlyDictionary<string, RngState> states)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(states);
        RngStateCodec.RequireEveryStream(states);
        var payload = RngStateCodec.Serialize(states);

        using var transaction = _connection.BeginTransaction();
        try
        {
            using var command = _connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE meta SET rng_states = $payload WHERE id = 1";
            command.Parameters.Add("$payload", SqliteType.Text).Value = payload;
            if (command.ExecuteNonQuery() != 1)
            {
                throw new InvalidDataException("Save meta row is missing.");
            }

            transaction.Commit();
        }
        catch
        {
            TryRollback(transaction);
            throw;
        }
    }

    public IReadOnlyDictionary<string, RngState> ReadRngStates()
    {
        ThrowIfDisposed();
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT rng_states FROM meta WHERE id = 1";
        var value = command.ExecuteScalar();
        if (value is null or DBNull)
        {
            throw new InvalidOperationException("RNG states have not been written to this save.");
        }

        if (value is not string payload || payload.Length == 0)
        {
            throw new InvalidDataException("RNG states payload is malformed.");
        }

        return RngStateCodec.Deserialize(payload);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection.Dispose();
    }

    internal string JournalMode
    {
        get
        {
            ThrowIfDisposed();
            using var command = _connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode";
            return command.ExecuteScalar() as string ?? "";
        }
    }

    internal long ForeignKeysEnabled
    {
        get
        {
            ThrowIfDisposed();
            using var command = _connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys";
            return command.ExecuteScalar() is long value ? value : 0;
        }
    }

    private static void CreateSchemaAndMeta(
        SqliteConnection connection,
        SaveMeta meta,
        IReadOnlyList<ISaveMigration> migrations)
    {
        if (TableExists(connection, "meta"))
        {
            throw new InvalidDataException("New save file already has a meta table.");
        }

        var now = DateTimeOffset.UtcNow;
        var version = migrations[^1].Version;
        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var migration in migrations)
            {
                migration.Apply(connection, transaction);
            }

            InsertMeta(connection, transaction, meta, version, now);
            transaction.Commit();
        }
        catch (Exception exception)
        {
            TryRollback(transaction);
            if (exception is SqliteException sqlite)
            {
                throw new IOException("SQLite save could not be created.", sqlite);
            }

            throw;
        }
    }

    private static void MigrateIfNeeded(
        SqliteConnection connection,
        string path,
        IReadOnlyList<ISaveMigration> migrations)
    {
        var supported = migrations[^1].Version;
        var current = ReadSchemaVersion(connection);
        if (current > supported)
        {
            throw new SaveSchemaTooNewException(path, current, supported);
        }

        if (current == supported)
        {
            return;
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var migration in migrations)
            {
                if (migration.Version > current)
                {
                    migration.Apply(connection, transaction);
                }
            }

            UpdateSchemaVersion(connection, transaction, supported);
            transaction.Commit();
        }
        catch (Exception exception)
        {
            TryRollback(transaction);
            if (exception is SqliteException sqlite)
            {
                throw new InvalidDataException("Save migration failed.", sqlite);
            }

            throw;
        }
    }

    private static void InsertMeta(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SaveMeta meta,
        int schemaVersion,
        DateTimeOffset now)
    {
        var timestamp = FormatTimestamp(now);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO meta (
                id, schema_version, created_at_utc, saved_at_utc,
                career_name, manager_name, player_team_id, current_game_date,
                world_data_hash, master_seed, rng_states)
            VALUES (
                1, $schemaVersion, $createdAt, $savedAt,
                $careerName, $managerName, $playerTeamId, $currentGameDate,
                $worldDataHash, $masterSeed, NULL)
            """;
        command.Parameters.Add("$schemaVersion", SqliteType.Integer).Value = schemaVersion;
        command.Parameters.Add("$createdAt", SqliteType.Text).Value = timestamp;
        command.Parameters.Add("$savedAt", SqliteType.Text).Value = timestamp;
        command.Parameters.Add("$careerName", SqliteType.Text).Value = meta.CareerName;
        command.Parameters.Add("$managerName", SqliteType.Text).Value = meta.ManagerName;
        command.Parameters.Add("$playerTeamId", SqliteType.Text).Value = meta.PlayerTeamId;
        command.Parameters.Add("$currentGameDate", SqliteType.Text).Value = FormatGameDate(meta.CurrentGameDate);
        command.Parameters.Add("$worldDataHash", SqliteType.Text).Value = meta.WorldDataHash;
        command.Parameters.Add("$masterSeed", SqliteType.Text).Value = meta.MasterSeed.ToString(CultureInfo.InvariantCulture);
        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidDataException("Save meta row was not inserted.");
        }
    }

    private static void UpdateSchemaVersion(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int schemaVersion)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE meta SET schema_version = $schemaVersion WHERE id = 1";
        command.Parameters.Add("$schemaVersion", SqliteType.Integer).Value = schemaVersion;
        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidDataException("Save meta row is missing.");
        }
    }

    private static int ReadSchemaVersion(SqliteConnection connection)
    {
        if (!TableExists(connection, "meta"))
        {
            throw new InvalidDataException("Save file has no meta table.");
        }

        using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM meta";
        if (count.ExecuteScalar() is not long rowCount || rowCount != 1)
        {
            throw new InvalidDataException("Save meta table must contain exactly one row.");
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT schema_version FROM meta WHERE id = 1";
        return ReadVersion(command.ExecuteScalar());
    }

    private static bool TableExists(SqliteConnection connection, string name)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name";
        command.Parameters.Add("$name", SqliteType.Text).Value = name;
        return command.ExecuteScalar() is not null;
    }

    private static int ReadVersion(object? value)
    {
        if (value is long version && version is >= 1 and <= int.MaxValue)
        {
            return (int)version;
        }

        throw new InvalidDataException("Save schema version is malformed.");
    }

    private static SqliteConnection OpenConnection(string fullPath, SqliteOpenMode mode)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = mode,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private static void Configure(SqliteConnection connection)
    {
        using (var foreignKeys = connection.CreateCommand())
        {
            foreignKeys.CommandText = "PRAGMA foreign_keys = ON";
            foreignKeys.ExecuteNonQuery();
        }

        using (var check = connection.CreateCommand())
        {
            check.CommandText = "PRAGMA foreign_keys";
            if (check.ExecuteScalar() is not long enabled || enabled != 1)
            {
                throw new InvalidOperationException("SQLite foreign keys could not be enabled.");
            }
        }

        using (var journal = connection.CreateCommand())
        {
            journal.CommandText = "PRAGMA journal_mode = WAL";
            var mode = journal.ExecuteScalar() as string;
            if (!string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"SQLite journal mode is '{mode}', expected WAL.");
            }
        }

        using var busy = connection.CreateCommand();
        busy.CommandText = "PRAGMA busy_timeout = 5000";
        busy.ExecuteNonQuery();
    }

    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset ParseTimestamp(string text)
    {
        if (!DateTimeOffset.TryParseExact(
                text,
                TimestampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed) ||
            parsed.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException("Save timestamp is not a UTC ISO-8601 value.");
        }

        return parsed;
    }

    private static string FormatGameDate(DateOnly date)
    {
        return date.ToString(GameDateFormat, CultureInfo.InvariantCulture);
    }

    private static DateOnly ParseGameDate(string text)
    {
        if (!DateOnly.TryParseExact(text, GameDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new InvalidDataException("Save game date is malformed.");
        }

        return date;
    }

    private static ulong ParseMasterSeed(string text)
    {
        if (!ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var seed))
        {
            throw new InvalidDataException("Save master seed is malformed.");
        }

        return seed;
    }

    private static void TryRollback(SqliteTransaction transaction)
    {
        try
        {
            transaction.Rollback();
        }
        catch (SqliteException)
        {
            // Surface the original failure. Rollback can also fail if the connection is already broken.
        }
    }

    private static void DeleteSaveFiles(string fullPath)
    {
        TryDelete(fullPath);
        TryDelete(fullPath + "-wal");
        TryDelete(fullPath + "-shm");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
