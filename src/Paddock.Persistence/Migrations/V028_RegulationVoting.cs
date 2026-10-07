using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Regulation voting v2 (#275). Adds the tables of the political life of a racing series, keyed by series id: the rules in force and
/// the rules and calendar voted for the next season, the rejection memory, every team's leaning, cooldown and bank of votes, the
/// proposals that wait for a ballot, and the ballot items with their variants, votes, tally and stances. The tables of schema 1
/// (<c>regulations_meta</c>, <c>regulations_values</c>, <c>regulations_rejected</c>) stay: a section stored under schema 1 is read
/// from them as the series <c>f1</c> with no teams and no ballot, and the next save moves it into the new tables.
/// <para>
/// The vote mode of the career config needs no migration: <c>voteMode</c> is written only when it is not the default, so a save from
/// before this migration reads as one vote each, the only way a voted career counted votes, and keeps the config bytes it had. The
/// historical path is untouched: a career with historical rules never writes the section, so its world hash does not change.
/// </para>
/// </summary>
public sealed class V028_RegulationVoting : ISaveMigration
{
    public int Version => 28;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        CREATE TABLE regulation_series (
            series_id TEXT NOT NULL PRIMARY KEY CHECK (length(series_id) > 0),
            season INTEGER NOT NULL CHECK (season >= 1950),
            agenda_season INTEGER NOT NULL CHECK (agenda_season >= 0),
            fia_slots_done INTEGER NOT NULL CHECK (fia_slots_done >= 0),
            team_ballot_season INTEGER NOT NULL CHECK (team_ballot_season >= 0),
            has_next INTEGER NOT NULL CHECK (has_next IN (0, 1)),
            has_next_calendar INTEGER NOT NULL CHECK (has_next_calendar IN (0, 1))
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE regulation_series_values (
            series_id TEXT NOT NULL CHECK (length(series_id) > 0),
            scope TEXT NOT NULL CHECK (scope IN ('values', 'calendar', 'nextvalues', 'nextcalendar')),
            dimension_id TEXT NOT NULL CHECK (length(dimension_id) > 0),
            value TEXT NOT NULL CHECK (length(value) > 0),
            PRIMARY KEY (series_id, scope, dimension_id)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE regulation_series_rejected (
            series_id TEXT NOT NULL CHECK (length(series_id) > 0),
            dimension_id TEXT NOT NULL CHECK (length(dimension_id) > 0),
            value TEXT NOT NULL CHECK (length(value) > 0),
            season INTEGER NOT NULL CHECK (season >= 1950),
            PRIMARY KEY (series_id, dimension_id, value, season)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE regulation_teams (
            series_id TEXT NOT NULL CHECK (length(series_id) > 0),
            team_id TEXT NOT NULL CHECK (length(team_id) > 0),
            leaning TEXT NOT NULL CHECK (leaning IN ('Traditionalist', 'Progressive', 'Egalitarian', 'Gimmicky')),
            propose_from INTEGER NOT NULL CHECK (propose_from >= 1950),
            bank INTEGER NOT NULL CHECK (bank >= 0),
            PRIMARY KEY (series_id, team_id)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE regulation_pending (
            series_id TEXT NOT NULL CHECK (length(series_id) > 0),
            team_id TEXT NOT NULL CHECK (length(team_id) > 0),
            dimension_id TEXT NOT NULL CHECK (length(dimension_id) > 0),
            value TEXT NOT NULL CHECK (length(value) > 0),
            filed TEXT NOT NULL CHECK (length(filed) = 10),
            fee_cents INTEGER NOT NULL CHECK (fee_cents > 0),
            PRIMARY KEY (series_id, team_id)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE regulation_items (
            series_id TEXT NOT NULL CHECK (length(series_id) > 0),
            item_id TEXT NOT NULL CHECK (length(item_id) > 0),
            season INTEGER NOT NULL CHECK (season >= 1950),
            dimension_id TEXT NOT NULL CHECK (length(dimension_id) > 0),
            origin TEXT NOT NULL CHECK (origin IN ('Fia', 'Teams')),
            announced TEXT NOT NULL CHECK (length(announced) = 10),
            deadline TEXT NOT NULL CHECK (length(deadline) = 10),
            current_value TEXT NOT NULL CHECK (length(current_value) > 0),
            reason_key TEXT NOT NULL CHECK (length(reason_key) > 0),
            outcome TEXT CHECK (outcome IS NULL OR outcome IN ('Adopted', 'Rejected', 'NoVotes')),
            winning_option TEXT CHECK (winning_option IS NULL OR length(winning_option) > 0),
            result_reason TEXT CHECK (result_reason IS NULL OR length(result_reason) > 0),
            president_decided INTEGER CHECK (president_decided IS NULL OR president_decided IN (0, 1)),
            PRIMARY KEY (series_id, item_id)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE regulation_item_args (
            series_id TEXT NOT NULL,
            item_id TEXT NOT NULL,
            name TEXT NOT NULL CHECK (length(name) > 0),
            value TEXT NOT NULL,
            PRIMARY KEY (series_id, item_id, name)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE regulation_item_variants (
            series_id TEXT NOT NULL,
            item_id TEXT NOT NULL,
            variant_id TEXT NOT NULL CHECK (length(variant_id) > 0),
            value TEXT NOT NULL CHECK (length(value) > 0),
            PRIMARY KEY (series_id, item_id, variant_id)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE regulation_item_proposers (
            series_id TEXT NOT NULL,
            item_id TEXT NOT NULL,
            variant_id TEXT NOT NULL,
            team_id TEXT NOT NULL CHECK (length(team_id) > 0),
            PRIMARY KEY (series_id, item_id, variant_id, team_id)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE regulation_item_votes (
            series_id TEXT NOT NULL,
            item_id TEXT NOT NULL,
            team_id TEXT NOT NULL CHECK (length(team_id) > 0),
            option_id TEXT NOT NULL CHECK (length(option_id) > 0),
            spent INTEGER NOT NULL CHECK (spent >= 0),
            PRIMARY KEY (series_id, item_id, team_id)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE regulation_item_tally (
            series_id TEXT NOT NULL,
            item_id TEXT NOT NULL,
            option_id TEXT NOT NULL CHECK (length(option_id) > 0),
            weight INTEGER NOT NULL CHECK (weight >= 0),
            PRIMARY KEY (series_id, item_id, option_id)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE regulation_item_stances (
            series_id TEXT NOT NULL,
            item_id TEXT NOT NULL,
            team_id TEXT NOT NULL CHECK (length(team_id) > 0),
            option_id TEXT NOT NULL CHECK (length(option_id) > 0),
            weight INTEGER NOT NULL CHECK (weight >= 0),
            spent INTEGER NOT NULL CHECK (spent >= 0),
            banked INTEGER NOT NULL CHECK (banked IN (0, 1)),
            reason_key TEXT NOT NULL,
            PRIMARY KEY (series_id, item_id, team_id)
        ) STRICT, WITHOUT ROWID;
        """;
}
