using Microsoft.Data.Sqlite;
using Paddock.Domain.Board;
using Paddock.Domain.World;
using static Paddock.Persistence.SectionRows;

namespace Paddock.Persistence;

/// <summary>Saves the <c>board</c> section into the tables made by <see cref="V010_BoardAndObjectivesSections"/>.</summary>
public sealed class BoardSectionStore : ISectionStore
{
    public string SectionName => BoardSection.SectionName;

    public int SchemaVersion => BoardSection.Empty.SchemaVersion;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var board = section switch
        {
            null => null,
            BoardSection typed => typed,
            _ => throw new ArgumentException($"The board store cannot save a {section.GetType().Name}.", nameof(section)),
        };

        // A file saved while it was still on an older schema has no board tables, and no section to clear in them.
        if (board is null && !TableExists(connection, transaction, "boards"))
        {
            return;
        }

        foreach (var table in new[] { "board_unemployed", "board_reputation_changes", "board_reputations", "boards" })
        {
            Run(connection, transaction, "DELETE FROM " + table);
        }

        if (board is null)
        {
            return;
        }

        foreach (var record in board.Boards)
        {
            var principal = record.Principal;
            Run(
                connection,
                transaction,
                "INSERT INTO boards (organization_id, patience, confidence, low_streak, expected_position, archetype, last_review, last_target, last_cash, "
                + "principal_kind, principal_subject, principal_since, principal_protected_until, principal_founder) "
                + "VALUES ($org, $patience, $confidence, $streak, $expected, $archetype, $review, $target, $cash, $kind, $subject, $since, $until, $founder)",
                ("$org", record.Organization.Value),
                ("$patience", (long)record.Patience),
                ("$confidence", (long)record.ConfidenceTenths),
                ("$streak", (long)record.LowStreak),
                ("$expected", (long)record.ExpectedPosition),
                ("$archetype", record.Archetype),
                ("$review", record.LastReview?.ToString()),
                ("$target", record.LastTargetTenths is int target ? (long)target : null),
                ("$cash", record.LastCash),
                ("$kind", principal?.Kind.ToString()),
                ("$subject", principal?.Subject),
                ("$since", principal?.Since.ToString()),
                ("$until", principal?.ProtectedUntil.ToString()),
                ("$founder", principal is null ? null : principal.Founder ? 1L : 0L));
        }

        foreach (var (subject, tenths) in board.Reputations)
        {
            Run(connection, transaction, "INSERT INTO board_reputations (subject, tenths) VALUES ($subject, $tenths)", ("$subject", subject), ("$tenths", (long)tenths));
        }

        var ordinal = 0L;
        foreach (var change in board.Changes)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO board_reputation_changes (ordinal, subject, on_date, delta, result, reason_key) VALUES ($ordinal, $subject, $on, $delta, $result, $reason)",
                ("$ordinal", ordinal++),
                ("$subject", change.Subject),
                ("$on", change.On.ToString()),
                ("$delta", (long)change.DeltaTenths),
                ("$result", (long)change.ResultTenths),
                ("$reason", change.ReasonKey));
        }

        foreach (var record in board.Unemployed)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO board_unemployed (manager_id, since, former_organization, severance, offers_made, last_offer_on) "
                + "VALUES ($manager, $since, $former, $severance, $offers, $last)",
                ("$manager", record.Manager),
                ("$since", record.Since.ToString()),
                ("$former", record.FormerOrganization?.Value),
                ("$severance", record.Severance),
                ("$offers", (long)record.OffersMade),
                ("$last", record.LastOfferOn?.ToString()));
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion != 1)
        {
            throw new InvalidDataException($"The stored board section has schema version {storedSchemaVersion}, which this build cannot read.");
        }

        var boards = new List<BoardRecord>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT organization_id, patience, confidence, low_streak, expected_position, archetype, last_review, last_target, last_cash, "
                + "principal_kind, principal_subject, principal_since, principal_protected_until, principal_founder FROM boards ORDER BY organization_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                PrincipalRecord? principal = reader.IsDBNull(9)
                    ? null
                    : new PrincipalRecord(
                        ParseEnum<PrincipalKind>(reader.GetString(9)),
                        reader.GetString(10),
                        ParseDate(reader.GetString(11)),
                        ParseDate(reader.GetString(12)),
                        reader.GetInt64(13) == 1);
                boards.Add(new BoardRecord(
                    OrganizationIdFrom(reader.GetString(0)),
                    checked((int)reader.GetInt64(1)),
                    checked((int)reader.GetInt64(2)),
                    checked((int)reader.GetInt64(3)),
                    checked((int)reader.GetInt64(4)),
                    reader.GetString(5),
                    principal,
                    ParseOptionalDate(reader, 6),
                    reader.IsDBNull(7) ? null : checked((int)reader.GetInt64(7)),
                    reader.IsDBNull(8) ? null : reader.GetInt64(8)));
            }
        }

        var reputations = new List<KeyValuePair<string, int>>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT subject, tenths FROM board_reputations ORDER BY subject";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                reputations.Add(new KeyValuePair<string, int>(reader.GetString(0), checked((int)reader.GetInt64(1))));
            }
        }

        var changes = new List<ReputationChange>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT subject, on_date, delta, result, reason_key FROM board_reputation_changes ORDER BY ordinal";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                changes.Add(new ReputationChange(
                    reader.GetString(0),
                    ParseDate(reader.GetString(1)),
                    checked((int)reader.GetInt64(2)),
                    checked((int)reader.GetInt64(3)),
                    reader.GetString(4)));
            }
        }

        var unemployed = new List<UnemployedRecord>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT manager_id, since, former_organization, severance, offers_made, last_offer_on FROM board_unemployed ORDER BY manager_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                unemployed.Add(new UnemployedRecord(
                    reader.GetString(0),
                    ParseDate(reader.GetString(1)),
                    reader.IsDBNull(2) ? null : OrganizationIdFrom(reader.GetString(2)),
                    reader.GetInt64(3),
                    checked((int)reader.GetInt64(4)),
                    ParseOptionalDate(reader, 5)));
            }
        }

        return BoardSection.Restore(boards, reputations, changes, unemployed);
    }
}
