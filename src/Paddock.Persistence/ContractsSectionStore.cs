using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.Contracts;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Persistence;

/// <summary>Saves the <c>contracts</c> section into the tables made by <see cref="V009_ContractsSection"/>.</summary>
public sealed class ContractsSectionStore : ISectionStore
{
    private const string LastReasons = "last";

    public string SectionName => ContractsSection.SectionName;

    public int SchemaVersion => ContractsSection.Empty.SchemaVersion;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var contracts = section switch
        {
            null => null,
            ContractsSection typed => typed,
            _ => throw new ArgumentException($"The contracts store cannot save a {section.GetType().Name}.", nameof(section)),
        };

        // A file saved while it was still on an older schema has no contracts tables, and no section to clear in them.
        if (contracts is null && !TableExists(connection, transaction, "contracts_counter"))
        {
            return;
        }

        // Children first, so foreign keys hold at every statement.
        foreach (var table in new[]
        {
            "negotiation_reasons", "negotiation_terms", "negotiation_rounds", "negotiations",
            "contract_renewal_prompts", "contract_terms", "contracts_counter",
        })
        {
            Run(connection, transaction, "DELETE FROM " + table, []);
        }

        if (contracts is null)
        {
            return;
        }

        Run(connection, transaction, "INSERT INTO contracts_counter (id, next_negotiation) VALUES (1, $next)", [("$next", contracts.NextNegotiation)]);
        foreach (var terms in contracts.Terms)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO contract_terms (contract_id, points_bonus, win_bonus, title_bonus, option_holder, exit_position) "
                + "VALUES ($id, $points, $win, $title, $holder, $exit)",
                [
                    ("$id", terms.ContractId.Value),
                    ("$points", terms.PointsBonus),
                    ("$win", terms.WinBonus),
                    ("$title", terms.TitleBonus),
                    ("$holder", terms.OptionHolder?.ToString()),
                    ("$exit", terms.Exit is ExitClause exit ? (long)exit.PositionWorseThan : null),
                ]);
        }

        foreach (var contract in contracts.PromptedRenewals)
        {
            Run(connection, transaction, "INSERT INTO contract_renewal_prompts (contract_id) VALUES ($id)", [("$id", contract.Value)]);
        }

        foreach (var negotiation in contracts.Negotiations)
        {
            WriteNegotiation(connection, transaction, negotiation);
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion != 1)
        {
            throw new InvalidDataException($"The stored contracts section has schema version {storedSchemaVersion}, which this build cannot read.");
        }

        long next;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT next_negotiation FROM contracts_counter WHERE id = 1";
            next = command.ExecuteScalar() is long value
                ? value
                : throw new InvalidDataException("The contracts section is registered but its counter is missing.");
        }

        var terms = new List<ContractTerms>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT contract_id, points_bonus, win_bonus, title_bonus, option_holder, exit_position FROM contract_terms ORDER BY contract_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                terms.Add(new ContractTerms(
                    ContractIdFrom(reader.GetString(0)),
                    reader.GetInt64(1),
                    reader.GetInt64(2),
                    reader.GetInt64(3),
                    reader.IsDBNull(4) ? null : ParseEnum<OptionHolder>(reader.GetString(4)),
                    reader.IsDBNull(5) ? null : new ExitClause(checked((int)reader.GetInt64(5)))));
            }
        }

        var prompts = new List<ContractId>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT contract_id FROM contract_renewal_prompts ORDER BY contract_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                prompts.Add(ContractIdFrom(reader.GetString(0)));
            }
        }

        var termRows = new Dictionary<(long Number, string Slot), OfferTerms>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT negotiation_number, slot, salary, points_bonus, win_bonus, title_bonus, years, seat, option_holder, option_years, exit_position "
                + "FROM negotiation_terms";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                termRows.Add((reader.GetInt64(0), reader.GetString(1)), ReadTerms(reader));
            }
        }

        var reasonRows = new Dictionary<(long Number, string Scope), List<string>>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT negotiation_number, scope, reason_key FROM negotiation_reasons ORDER BY negotiation_number, scope, ordinal";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var key = (reader.GetInt64(0), reader.GetString(1));
                if (!reasonRows.TryGetValue(key, out var list))
                {
                    list = [];
                    reasonRows.Add(key, list);
                }

                list.Add(reader.GetString(2));
            }
        }

        var roundRows = new Dictionary<long, List<(int Ordinal, int Number, RoundKind Kind, GameDate On)>>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT negotiation_number, ordinal, round_number, kind, on_date FROM negotiation_rounds ORDER BY negotiation_number, ordinal";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var number = reader.GetInt64(0);
                if (!roundRows.TryGetValue(number, out var list))
                {
                    list = [];
                    roundRows.Add(number, list);
                }

                list.Add((checked((int)reader.GetInt64(1)), checked((int)reader.GetInt64(2)), ParseEnum<RoundKind>(reader.GetString(3)), ParseDate(reader.GetString(4))));
            }
        }

        var negotiations = new List<Negotiation>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT number, manager_id, proposer_id, person_id, subject, renewal_of, opened, deadline, max_rounds, rounds_used, interest, "
                + "status, respond_on, considered_utility, closed_on, signed_contract FROM negotiations ORDER BY number";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var number = reader.GetInt64(0);
                var history = new List<NegotiationRound>();
                foreach (var (ordinal, roundNumber, kind, on) in roundRows.GetValueOrDefault(number) ?? [])
                {
                    var slot = "round:" + ordinal.ToString(CultureInfo.InvariantCulture);
                    history.Add(new NegotiationRound(
                        roundNumber,
                        kind,
                        on,
                        termRows.GetValueOrDefault((number, slot)),
                        reasonRows.GetValueOrDefault((number, slot)) ?? []));
                }

                negotiations.Add(new Negotiation(
                    number,
                    reader.GetString(1),
                    OrganizationIdFrom(reader.GetString(2)),
                    PersonIdFrom(reader.GetString(3)),
                    ParseSubject(reader.GetString(4)),
                    reader.IsDBNull(5) ? null : ContractIdFrom(reader.GetString(5)),
                    ParseDate(reader.GetString(6)),
                    ParseDate(reader.GetString(7)),
                    checked((int)reader.GetInt64(8)),
                    checked((int)reader.GetInt64(9)),
                    checked((int)reader.GetInt64(10)),
                    ParseEnum<NegotiationStatus>(reader.GetString(11)),
                    termRows.GetValueOrDefault((number, "offer")),
                    termRows.GetValueOrDefault((number, "counter")),
                    reader.IsDBNull(12) ? null : ParseDate(reader.GetString(12)),
                    reader.GetInt64(13),
                    reasonRows.GetValueOrDefault((number, LastReasons)) ?? [],
                    history,
                    reader.IsDBNull(14) ? null : ParseDate(reader.GetString(14)),
                    reader.IsDBNull(15) ? null : ContractIdFrom(reader.GetString(15))));
            }
        }

        return ContractsSection.Restore(next, terms, negotiations, prompts);
    }

    private static bool TableExists(SqliteConnection connection, SqliteTransaction transaction, string table)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name";
        command.Parameters.AddWithValue("$name", table);
        return command.ExecuteScalar() is not null;
    }

    private static void WriteNegotiation(SqliteConnection connection, SqliteTransaction transaction, Negotiation negotiation)
    {
        Run(
            connection,
            transaction,
            "INSERT INTO negotiations (number, manager_id, proposer_id, person_id, subject, renewal_of, opened, deadline, max_rounds, rounds_used, "
            + "interest, status, respond_on, considered_utility, closed_on, signed_contract) "
            + "VALUES ($n, $manager, $proposer, $person, $subject, $renewal, $opened, $deadline, $max, $used, $interest, $status, $respond, $utility, $closed, $signed)",
            [
                ("$n", negotiation.Number),
                ("$manager", negotiation.ManagerId),
                ("$proposer", negotiation.Proposer.Value),
                ("$person", negotiation.Counterparty.Value),
                ("$subject", negotiation.Subject.Key),
                ("$renewal", negotiation.RenewalOf?.Value),
                ("$opened", negotiation.Opened.ToString()),
                ("$deadline", negotiation.Deadline.ToString()),
                ("$max", (long)negotiation.MaxRounds),
                ("$used", (long)negotiation.RoundsUsed),
                ("$interest", (long)negotiation.Interest),
                ("$status", negotiation.Status.ToString()),
                ("$respond", negotiation.RespondOn?.ToString()),
                ("$utility", negotiation.ConsideredUtilityMicro),
                ("$closed", negotiation.ClosedOn?.ToString()),
                ("$signed", negotiation.SignedContract?.Value),
            ]);
        WriteTerms(connection, transaction, negotiation.Number, "offer", negotiation.CurrentOffer);
        WriteTerms(connection, transaction, negotiation.Number, "counter", negotiation.Counter);
        WriteReasons(connection, transaction, negotiation.Number, LastReasons, negotiation.Reasons);
        for (var i = 0; i < negotiation.History.Count; i++)
        {
            var round = negotiation.History[i];
            var slot = "round:" + i.ToString(CultureInfo.InvariantCulture);
            Run(
                connection,
                transaction,
                "INSERT INTO negotiation_rounds (negotiation_number, ordinal, round_number, kind, on_date) VALUES ($n, $ordinal, $round, $kind, $on)",
                [("$n", negotiation.Number), ("$ordinal", (long)i), ("$round", (long)round.Number), ("$kind", round.Kind.ToString()), ("$on", round.On.ToString())]);
            WriteTerms(connection, transaction, negotiation.Number, slot, round.Terms);
            WriteReasons(connection, transaction, negotiation.Number, slot, round.Reasons);
        }
    }

    private static void WriteTerms(SqliteConnection connection, SqliteTransaction transaction, long number, string slot, OfferTerms? terms)
    {
        if (terms is null)
        {
            return;
        }

        Run(
            connection,
            transaction,
            "INSERT INTO negotiation_terms (negotiation_number, slot, salary, points_bonus, win_bonus, title_bonus, years, seat, option_holder, option_years, exit_position) "
            + "VALUES ($n, $slot, $salary, $points, $win, $title, $years, $seat, $holder, $extra, $exit)",
            [
                ("$n", number),
                ("$slot", slot),
                ("$salary", terms.Salary),
                ("$points", terms.PointsBonus),
                ("$win", terms.WinBonus),
                ("$title", terms.TitleBonus),
                ("$years", (long)terms.Years),
                ("$seat", terms.Seat?.ToString()),
                ("$holder", terms.Option?.Holder.ToString()),
                ("$extra", terms.Option is OfferOption option ? (long)option.ExtraYears : null),
                ("$exit", terms.Exit is ExitClause exit ? (long)exit.PositionWorseThan : null),
            ]);
    }

    private static void WriteReasons(SqliteConnection connection, SqliteTransaction transaction, long number, string scope, IReadOnlyList<string> reasons)
    {
        for (var i = 0; i < reasons.Count; i++)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO negotiation_reasons (negotiation_number, scope, ordinal, reason_key) VALUES ($n, $scope, $ordinal, $key)",
                [("$n", number), ("$scope", scope), ("$ordinal", (long)i), ("$key", reasons[i])]);
        }
    }

    private static OfferTerms ReadTerms(SqliteDataReader reader) => new(
        reader.GetInt64(2),
        reader.GetInt64(3),
        reader.GetInt64(4),
        reader.GetInt64(5),
        checked((int)reader.GetInt64(6)),
        reader.IsDBNull(7) ? null : ParseEnum<SeatStatus>(reader.GetString(7)),
        reader.IsDBNull(8) ? null : new OfferOption(ParseEnum<OptionHolder>(reader.GetString(8)), checked((int)reader.GetInt64(9))),
        reader.IsDBNull(10) ? null : new ExitClause(checked((int)reader.GetInt64(10))));

    private static T ParseEnum<T>(string text)
        where T : struct, Enum
    {
        if (!Enum.TryParse<T>(text, out var value) || !Enum.IsDefined(value) || value.ToString() != text)
        {
            throw new InvalidDataException($"'{text}' is not a {typeof(T).Name}.");
        }

        return value;
    }

    private static NegotiationSubject ParseSubject(string text)
    {
        try
        {
            return NegotiationSubject.Parse(text);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    private static ContractId ContractIdFrom(string text)
    {
        var id = ContractId.Generated(ParseSequence(text, "con:"));
        return id.Value == text ? id : throw new InvalidDataException($"Id '{text}' is not canonical.");
    }

    private static PersonId PersonIdFrom(string text)
    {
        var id = text.StartsWith("gen:", StringComparison.Ordinal) ? PersonId.Generated(ParseSequence(text, "gen:")) : PersonId.Real(text);
        return id.Value == text ? id : throw new InvalidDataException($"Id '{text}' is not canonical.");
    }

    private static OrganizationId OrganizationIdFrom(string text)
    {
        var id = text.StartsWith("org:", StringComparison.Ordinal) ? OrganizationId.Generated(ParseSequence(text, "org:")) : OrganizationId.Real(text);
        return id.Value == text ? id : throw new InvalidDataException($"Id '{text}' is not canonical.");
    }

    private static long ParseSequence(string text, string prefix)
    {
        if (!text.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Id '{text}' does not start with '{prefix}'.");
        }

        var tail = text.AsSpan(prefix.Length);
        if (tail.Length == 0 || (tail.Length > 1 && tail[0] == '0')
            || !long.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) || sequence < 1)
        {
            throw new InvalidDataException($"Id '{text}' has a malformed sequence.");
        }

        return sequence;
    }

    private static GameDate ParseDate(string text)
    {
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new InvalidDataException($"Date '{text}' is malformed.");
        }

        return new GameDate(date.Year, date.Month, date.Day);
    }

    private static void Run(SqliteConnection connection, SqliteTransaction transaction, string sql, (string Name, object? Value)[] parameters)
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
}
