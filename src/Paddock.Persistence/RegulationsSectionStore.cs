using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using static Paddock.Persistence.SectionRows;

namespace Paddock.Persistence;

/// <summary>
/// Saves the <c>regulations</c> section (schema 2) into the tables made by <see cref="V028_RegulationVoting"/>, and reads schema 1 from
/// the tables made by <see cref="V020_RaceSections"/> (one series, no teams, no ballot). A save writes the new tables and empties the
/// old ones, so a section is never stored twice.
/// </summary>
public sealed class RegulationsSectionStore : ISectionStore
{
    private static readonly string[] NewTables =
    [
        "regulation_item_stances",
        "regulation_item_tally",
        "regulation_item_votes",
        "regulation_item_proposers",
        "regulation_item_variants",
        "regulation_item_args",
        "regulation_items",
        "regulation_pending",
        "regulation_schedule_slots",
        "regulation_schedule",
        "regulation_teams",
        "regulation_series_rejected",
        "regulation_series_values",
        "regulation_series",
    ];

    public string SectionName => RegulationsSection.SectionName;

    public int SchemaVersion => 2;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var regulations = section switch
        {
            null => null,
            RegulationsSection typed => typed,
            _ => throw new ArgumentException("The regulations store cannot save a " + section.GetType().Name + ".", nameof(section)),
        };

        if (regulations is null && !TableExists(connection, transaction, "regulation_series"))
        {
            return;
        }

        foreach (var table in NewTables)
        {
            Run(connection, transaction, "DELETE FROM " + table);
        }

        if (TableExists(connection, transaction, "regulations_meta"))
        {
            Run(connection, transaction, "DELETE FROM regulations_rejected");
            Run(connection, transaction, "DELETE FROM regulations_values");
            Run(connection, transaction, "DELETE FROM regulations_meta");
        }

        if (regulations is null)
        {
            return;
        }

        foreach (var series in regulations.Series)
        {
            WriteSeries(connection, transaction, series);
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return storedSchemaVersion switch
        {
            1 => LoadSchema1(connection),
            2 => LoadSchema2(connection),
            _ => throw new InvalidDataException("Regulations section schema " + storedSchemaVersion.ToString(CultureInfo.InvariantCulture) + " is not readable."),
        };
    }

    private static void WriteSeries(SqliteConnection connection, SqliteTransaction transaction, SeriesRegulations series)
    {
        var id = series.SeriesId;
        Run(
            connection,
            transaction,
            "INSERT INTO regulation_series (series_id, season, agenda_season, fia_slots_done, team_ballot_season, has_next, has_next_calendar) VALUES ($id, $season, $agenda, $slots, $teamBallot, $hasNext, $hasCalendar)",
            ("$id", id),
            ("$season", series.Season),
            ("$agenda", series.AgendaSeason),
            ("$slots", series.FiaSlotsDone),
            ("$teamBallot", series.TeamBallotSeason),
            ("$hasNext", series.NextValues is null ? 0 : 1),
            ("$hasCalendar", series.NextCalendar is null ? 0 : 1));
        WriteValues(connection, transaction, id, "values", series.Values);
        WriteValues(connection, transaction, id, "calendar", series.Calendar);
        WriteValues(connection, transaction, id, "nextvalues", series.NextValues ?? []);
        WriteValues(connection, transaction, id, "nextcalendar", series.NextCalendar ?? []);
        foreach (var rejected in series.Rejected)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO regulation_series_rejected (series_id, dimension_id, value, season) VALUES ($id, $dimension, $value, $season)",
                ("$id", id),
                ("$dimension", rejected.DimensionId),
                ("$value", rejected.Value),
                ("$season", rejected.Season));
        }

        foreach (var team in series.Teams)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO regulation_teams (series_id, team_id, leaning, propose_from, bank) VALUES ($id, $team, $leaning, $from, $bank)",
                ("$id", id),
                ("$team", team.TeamId),
                ("$leaning", team.Leaning.ToString()),
                ("$from", team.ProposeFromSeason),
                ("$bank", team.Bank));
        }

        if (series.Schedule is { } schedule)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO regulation_schedule (series_id, season, proposals_open, proposals_close) VALUES ($id, $season, $open, $close)",
                ("$id", id),
                ("$season", schedule.Season),
                ("$open", schedule.ProposalsOpen.ToString()),
                ("$close", schedule.ProposalsClose.ToString()));
            for (var index = 0; index < schedule.Slots.Count; index++)
            {
                var slot = schedule.Slots[index];
                Run(
                    connection,
                    transaction,
                    "INSERT INTO regulation_schedule_slots (series_id, slot_index, kind, opens, closes) VALUES ($id, $index, $kind, $opens, $closes)",
                    ("$id", id),
                    ("$index", index),
                    ("$kind", slot.Kind.ToString()),
                    ("$opens", slot.Opens.ToString()),
                    ("$closes", slot.Closes.ToString()));
            }
        }

        foreach (var pending in series.Pending)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO regulation_pending (series_id, team_id, dimension_id, value, filed, fee_cents) VALUES ($id, $team, $dimension, $value, $filed, $fee)",
                ("$id", id),
                ("$team", pending.TeamId),
                ("$dimension", pending.DimensionId),
                ("$value", pending.Value),
                ("$filed", pending.Filed.ToString()),
                ("$fee", pending.FeeCents));
        }

        foreach (var item in series.Ballot)
        {
            WriteItem(connection, transaction, id, item);
        }
    }

    private static void WriteValues(SqliteConnection connection, SqliteTransaction transaction, string seriesId, string scope, IReadOnlyList<KeyValuePair<string, string>> values)
    {
        foreach (var (dimension, value) in values)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO regulation_series_values (series_id, scope, dimension_id, value) VALUES ($id, $scope, $dimension, $value)",
                ("$id", seriesId),
                ("$scope", scope),
                ("$dimension", dimension),
                ("$value", value));
        }
    }

    private static void WriteItem(SqliteConnection connection, SqliteTransaction transaction, string seriesId, BallotItem item)
    {
        var result = item.Result;
        Run(
            connection,
            transaction,
            "INSERT INTO regulation_items (series_id, item_id, season, dimension_id, origin, announced, deadline, current_value, reason_key, outcome, winning_option, result_reason, president_decided) VALUES ($id, $item, $season, $dimension, $origin, $announced, $deadline, $current, $reason, $outcome, $winner, $resultReason, $president)",
            ("$id", seriesId),
            ("$item", item.Id),
            ("$season", item.Season),
            ("$dimension", item.DimensionId),
            ("$origin", item.Origin.ToString()),
            ("$announced", item.Announced.ToString()),
            ("$deadline", item.Deadline.ToString()),
            ("$current", item.CurrentValue),
            ("$reason", item.ReasonKey),
            ("$outcome", result?.Outcome.ToString()),
            ("$winner", result?.WinningOption),
            ("$resultReason", result?.ReasonKey),
            ("$president", result is null ? null : result.PresidentDecided ? 1 : 0));
        foreach (var (name, value) in item.ReasonArguments)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO regulation_item_args (series_id, item_id, name, value) VALUES ($id, $item, $name, $value)",
                ("$id", seriesId),
                ("$item", item.Id),
                ("$name", name),
                ("$value", value));
        }

        foreach (var variant in item.Variants)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO regulation_item_variants (series_id, item_id, variant_id, value) VALUES ($id, $item, $variant, $value)",
                ("$id", seriesId),
                ("$item", item.Id),
                ("$variant", variant.Id),
                ("$value", variant.Value));
            foreach (var proposer in variant.ProposerTeamIds)
            {
                Run(
                    connection,
                    transaction,
                    "INSERT INTO regulation_item_proposers (series_id, item_id, variant_id, team_id) VALUES ($id, $item, $variant, $team)",
                    ("$id", seriesId),
                    ("$item", item.Id),
                    ("$variant", variant.Id),
                    ("$team", proposer));
            }
        }

        foreach (var vote in item.Votes)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO regulation_item_votes (series_id, item_id, team_id, option_id, spent) VALUES ($id, $item, $team, $option, $spent)",
                ("$id", seriesId),
                ("$item", item.Id),
                ("$team", vote.TeamId),
                ("$option", vote.Option),
                ("$spent", vote.Spent));
        }

        if (result is null)
        {
            return;
        }

        foreach (var entry in result.Tally)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO regulation_item_tally (series_id, item_id, option_id, weight) VALUES ($id, $item, $option, $weight)",
                ("$id", seriesId),
                ("$item", item.Id),
                ("$option", entry.Option),
                ("$weight", entry.Weight));
        }

        foreach (var stance in result.Stances)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO regulation_item_stances (series_id, item_id, team_id, option_id, weight, spent, banked, reason_key) VALUES ($id, $item, $team, $option, $weight, $spent, $banked, $reason)",
                ("$id", seriesId),
                ("$item", item.Id),
                ("$team", stance.TeamId),
                ("$option", stance.Option),
                ("$weight", stance.Weight),
                ("$spent", stance.Spent),
                ("$banked", stance.Banked ? 1 : 0),
                ("$reason", stance.ReasonKey));
        }
    }

    private static RegulationsSection LoadSchema1(SqliteConnection connection)
    {
        using var meta = connection.CreateCommand();
        meta.CommandText = "SELECT season FROM regulations_meta";
        var season = meta.ExecuteScalar() as long? ?? throw new InvalidDataException("The regulations section is registered but has no meta row.");

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        using var rows = connection.CreateCommand();
        rows.CommandText = "SELECT dimension_id, value FROM regulations_values ORDER BY dimension_id";
        using (var body = rows.ExecuteReader())
        {
            while (body.Read())
            {
                values.Add(body.GetString(0), body.GetString(1));
            }
        }

        var rejected = new List<RejectedRegulation>();
        using var memory = connection.CreateCommand();
        memory.CommandText = "SELECT dimension_id, value, season FROM regulations_rejected ORDER BY dimension_id, value, season";
        using (var body = memory.ExecuteReader())
        {
            while (body.Read())
            {
                rejected.Add(new RejectedRegulation(body.GetString(0), body.GetString(1), body.GetInt32(2)));
            }
        }

        return RegulationsSection.Create((int)season, values, rejected);
    }

    private static RegulationsSection LoadSchema2(SqliteConnection connection)
    {
        var series = new List<SeriesRegulations>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT series_id, season, agenda_season, fia_slots_done, team_ballot_season, has_next, has_next_calendar FROM regulation_series ORDER BY series_id";
        var headers = new List<(string Id, int Season, int Agenda, int Slots, int TeamBallot, bool HasNext, bool HasCalendar)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                headers.Add((reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5) == 1, reader.GetInt32(6) == 1));
            }
        }

        foreach (var header in headers)
        {
            var id = header.Id;
            var values = ReadValues(connection, id, "values");
            var calendar = ReadValues(connection, id, "calendar");
            var nextValues = ReadValues(connection, id, "nextvalues");
            var nextCalendar = ReadValues(connection, id, "nextcalendar");
            series.Add(new SeriesRegulations(
                id,
                header.Season,
                values,
                calendar,
                header.HasNext ? nextValues : null,
                header.HasCalendar ? nextCalendar : null,
                ReadRejected(connection, id),
                ReadTeams(connection, id),
                ReadPending(connection, id),
                ReadItems(connection, id),
                header.Agenda,
                header.Slots,
                header.TeamBallot)
            {
                Schedule = ReadSchedule(connection, id),
            });
        }

        if (series.Count == 0)
        {
            throw new InvalidDataException("The regulations section is registered but has no series.");
        }

        return RegulationsSection.Create(series);
    }

    private static Dictionary<string, string> ReadValues(SqliteConnection connection, string seriesId, string scope)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT dimension_id, value FROM regulation_series_values WHERE series_id = $id AND scope = $scope ORDER BY dimension_id";
        command.Parameters.AddWithValue("$id", seriesId);
        command.Parameters.AddWithValue("$scope", scope);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            values.Add(reader.GetString(0), reader.GetString(1));
        }

        return values;
    }

    private static List<RejectedRegulation> ReadRejected(SqliteConnection connection, string seriesId)
    {
        var list = new List<RejectedRegulation>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT dimension_id, value, season FROM regulation_series_rejected WHERE series_id = $id ORDER BY dimension_id, value, season";
        command.Parameters.AddWithValue("$id", seriesId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new RejectedRegulation(reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));
        }

        return list;
    }

    private static PoliticalSchedule? ReadSchedule(SqliteConnection connection, string seriesId)
    {
        int season;
        GameDate open;
        GameDate close;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT season, proposals_open, proposals_close FROM regulation_schedule WHERE series_id = $id";
            command.Parameters.AddWithValue("$id", seriesId);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            season = reader.GetInt32(0);
            open = ParseDate(reader.GetString(1));
            close = ParseDate(reader.GetString(2));
        }

        var slots = new List<BallotSlot>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT kind, opens, closes FROM regulation_schedule_slots WHERE series_id = $id ORDER BY slot_index";
            command.Parameters.AddWithValue("$id", seriesId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                slots.Add(new BallotSlot(ParseEnum<BallotSlotKind>(reader.GetString(0)), ParseDate(reader.GetString(1)), ParseDate(reader.GetString(2))));
            }
        }

        return new PoliticalSchedule(season, open, close, slots);
    }

    private static List<TeamPolitics> ReadTeams(SqliteConnection connection, string seriesId)
    {
        var list = new List<TeamPolitics>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT team_id, leaning, propose_from, bank FROM regulation_teams WHERE series_id = $id ORDER BY team_id";
        command.Parameters.AddWithValue("$id", seriesId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new TeamPolitics(reader.GetString(0), ParseEnum<PoliticalLeaning>(reader.GetString(1)), reader.GetInt32(2), reader.GetInt32(3)));
        }

        return list;
    }

    private static List<PendingProposal> ReadPending(SqliteConnection connection, string seriesId)
    {
        var list = new List<PendingProposal>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT team_id, dimension_id, value, filed, fee_cents FROM regulation_pending WHERE series_id = $id ORDER BY team_id";
        command.Parameters.AddWithValue("$id", seriesId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new PendingProposal(reader.GetString(0), reader.GetString(1), reader.GetString(2), ParseDate(reader.GetString(3)), reader.GetInt64(4)));
        }

        return list;
    }

    private static List<BallotItem> ReadItems(SqliteConnection connection, string seriesId)
    {
        var headers = new List<(string Id, int Season, string Dimension, BallotOrigin Origin, GameDate Announced, GameDate Deadline, string Current, string Reason, string? Outcome, string? Winner, string? ResultReason, bool President)>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT item_id, season, dimension_id, origin, announced, deadline, current_value, reason_key, outcome, winning_option, result_reason, president_decided FROM regulation_items WHERE series_id = $id ORDER BY item_id";
            command.Parameters.AddWithValue("$id", seriesId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                headers.Add((
                    reader.GetString(0),
                    reader.GetInt32(1),
                    reader.GetString(2),
                    ParseEnum<BallotOrigin>(reader.GetString(3)),
                    ParseDate(reader.GetString(4)),
                    ParseDate(reader.GetString(5)),
                    reader.GetString(6),
                    reader.GetString(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8),
                    reader.IsDBNull(9) ? null : reader.GetString(9),
                    reader.IsDBNull(10) ? null : reader.GetString(10),
                    !reader.IsDBNull(11) && reader.GetInt32(11) == 1));
            }
        }

        var items = new List<BallotItem>(headers.Count);
        foreach (var header in headers)
        {
            var arguments = new List<KeyValuePair<string, string>>();
            Query(connection, "SELECT name, value FROM regulation_item_args WHERE series_id = $id AND item_id = $item ORDER BY name", seriesId, header.Id, reader =>
                arguments.Add(new KeyValuePair<string, string>(reader.GetString(0), reader.GetString(1))));
            var proposers = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            Query(connection, "SELECT variant_id, team_id FROM regulation_item_proposers WHERE series_id = $id AND item_id = $item ORDER BY variant_id, team_id", seriesId, header.Id, reader =>
            {
                var variant = reader.GetString(0);
                if (!proposers.TryGetValue(variant, out var list))
                {
                    list = [];
                    proposers.Add(variant, list);
                }

                list.Add(reader.GetString(1));
            });
            var variants = new List<BallotVariant>();
            Query(connection, "SELECT variant_id, value FROM regulation_item_variants WHERE series_id = $id AND item_id = $item ORDER BY variant_id", seriesId, header.Id, reader =>
                variants.Add(new BallotVariant(reader.GetString(0), reader.GetString(1), proposers.TryGetValue(reader.GetString(0), out var list) ? list : [])));
            var votes = new List<CastVote>();
            Query(connection, "SELECT team_id, option_id, spent FROM regulation_item_votes WHERE series_id = $id AND item_id = $item ORDER BY team_id", seriesId, header.Id, reader =>
                votes.Add(new CastVote(reader.GetString(0), reader.GetString(1), reader.GetInt32(2))));

            BallotResult? result = null;
            if (header.Outcome is not null)
            {
                var tally = new List<OptionTally>();
                Query(connection, "SELECT option_id, weight FROM regulation_item_tally WHERE series_id = $id AND item_id = $item ORDER BY option_id", seriesId, header.Id, reader =>
                    tally.Add(new OptionTally(reader.GetString(0), reader.GetInt32(1))));
                var stances = new List<TeamStance>();
                Query(connection, "SELECT team_id, option_id, weight, spent, banked, reason_key FROM regulation_item_stances WHERE series_id = $id AND item_id = $item ORDER BY team_id", seriesId, header.Id, reader =>
                    stances.Add(new TeamStance(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4) == 1, reader.GetString(5))));
                result = new BallotResult(
                    ParseEnum<BallotOutcome>(header.Outcome),
                    header.Winner ?? throw new InvalidDataException("A resolved ballot item has no winning option."),
                    header.ResultReason ?? throw new InvalidDataException("A resolved ballot item has no reason."),
                    header.President,
                    tally,
                    stances);
            }

            items.Add(new BallotItem(header.Id, header.Season, header.Dimension, header.Origin, header.Announced, header.Deadline, header.Current, header.Reason, arguments, variants, votes, result));
        }

        return items;
    }

    private static void Query(SqliteConnection connection, string sql, string seriesId, string itemId, Action<SqliteDataReader> read)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", seriesId);
        command.Parameters.AddWithValue("$item", itemId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            read(reader);
        }
    }
}
