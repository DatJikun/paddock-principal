using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Persistence;

/// <summary>Small helpers the section stores share: running a statement, and the text forms of dates, enums and ids.</summary>
internal static class SectionRows
{
    public static void Run(SqliteConnection connection, SqliteTransaction transaction, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        command.ExecuteNonQuery();
    }

    public static bool TableExists(SqliteConnection connection, SqliteTransaction transaction, string table)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name";
        command.Parameters.AddWithValue("$name", table);
        return command.ExecuteScalar() is not null;
    }

    public static GameDate ParseDate(string text)
    {
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new InvalidDataException($"Date '{text}' is malformed.");
        }

        return new GameDate(date.Year, date.Month, date.Day);
    }

    public static GameDate? ParseOptionalDate(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : ParseDate(reader.GetString(ordinal));

    public static T ParseEnum<T>(string text)
        where T : struct, Enum
    {
        if (!Enum.TryParse<T>(text, out var value) || !Enum.IsDefined(value) || value.ToString() != text)
        {
            throw new InvalidDataException($"'{text}' is not a {typeof(T).Name}.");
        }

        return value;
    }

    public static OrganizationId OrganizationIdFrom(string text)
    {
        var id = text.StartsWith("org:", StringComparison.Ordinal)
            ? OrganizationId.Generated(ParseSequence(text, "org:"))
            : OrganizationId.Real(text);
        return id.Value == text ? id : throw new InvalidDataException($"Id '{text}' is not canonical.");
    }

    private static long ParseSequence(string text, string prefix)
    {
        var tail = text.AsSpan(prefix.Length);
        if (tail.Length == 0 || (tail.Length > 1 && tail[0] == '0')
            || !long.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) || sequence < 1)
        {
            throw new InvalidDataException($"Id '{text}' has a malformed sequence.");
        }

        return sequence;
    }
}
