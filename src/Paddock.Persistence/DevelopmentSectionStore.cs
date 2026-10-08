using Microsoft.Data.Sqlite;
using Paddock.Domain.Development;
using Paddock.Domain.World;
using static Paddock.Persistence.SectionRows;

namespace Paddock.Persistence;

/// <summary>Saves the <c>development</c> section into the tables made by <see cref="V016_DevelopmentSection"/>.</summary>
public sealed class DevelopmentSectionStore : ISectionStore
{
    public string SectionName => DevelopmentSection.SectionName;

    public int SchemaVersion => DevelopmentSection.Empty.SchemaVersion;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var development = section switch
        {
            null => null,
            DevelopmentSection typed => typed,
            _ => throw new ArgumentException($"The development store cannot save a {section.GetType().Name}.", nameof(section)),
        };

        // A file saved while it was still on an older schema has no development tables, and no section to clear in them.
        if (development is null && !TableExists(connection, transaction, "development_counter"))
        {
            return;
        }

        foreach (var table in new[] { "development_notes", "development_projects", "development_accounts", "development_plans", "development_counter" })
        {
            Run(connection, transaction, "DELETE FROM " + table);
        }

        if (development is null)
        {
            return;
        }

        Run(connection, transaction, "INSERT INTO development_counter (id, next_project) VALUES (1, $next)", ("$next", development.NextProject));
        foreach (var plan in development.Plans)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO development_plans (organization_id, current_percent, account_percent, next_year_percent, aero_priority, "
                + "chassis_priority, reliability_priority, tyres_priority, spent_current, spent_account, spent_next_year, changed_on, "
                + "next_philosophy_milli, next_aero_milli) "
                + "VALUES ($org, $current, $account, $next, $aero, $chassis, $reliability, $tyres, $sc, $sa, $sn, $changed, $philosophy, $nextAero)",
                ("$org", plan.Organization.Value),
                ("$current", (long)plan.CurrentPercent),
                ("$account", (long)plan.AccountPercent),
                ("$next", (long)plan.NextYearPercent),
                ("$aero", (long)plan.AeroPriority),
                ("$chassis", (long)plan.ChassisPriority),
                ("$reliability", (long)plan.ReliabilityPriority),
                ("$tyres", (long)plan.TyresPriority),
                ("$sc", plan.SpentCurrentCents),
                ("$sa", plan.SpentAccountCents),
                ("$sn", plan.SpentNextYearCents),
                ("$changed", plan.ChangedOn?.ToString()),
                ("$philosophy", (long)plan.NextPhilosophyMilli),
                ("$nextAero", (long)plan.NextAeroMilli));
        }

        foreach (var account in development.Accounts)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO development_accounts (organization_id, stock_milli, next_year_share_milli, rules_year, concept_year) VALUES ($org, $stock, $next, $year, $concept)",
                ("$org", account.Organization.Value),
                ("$stock", (long)account.StockMilli),
                ("$next", (long)account.NextYearShareMilli),
                ("$year", (long)account.RulesYear),
                ("$concept", (long)account.ConceptYear));
        }

        foreach (var project in development.Projects)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO development_projects (number, organization_id, kind, area, engineer, started, duration_days, progress_days, cost, posted, "
                + "share_milli, risk_milli, outcome_milli, status, timing, timing_races, races_waited, closed_on, "
                + "production_ends, production_cost, philosophy_milli, aero_milli, ceiling_milli, flags) "
                + "VALUES ($number, $org, $kind, $area, $engineer, $started, $duration, $progress, $cost, $posted, $share, $risk, $outcome, $status, $timing, $races, $waited, $closed, $prodEnds, $prodCost, $philosophy, $aeroMilli, $ceilingMilli, $flags)",
                ("$number", project.Number),
                ("$org", project.Organization.Value),
                ("$kind", project.Kind.ToString()),
                ("$area", project.Area?.ToString()),
                ("$engineer", project.Engineer),
                ("$started", project.Started.ToString()),
                ("$duration", (long)project.DurationDays),
                ("$progress", (long)project.ProgressDays),
                ("$cost", project.CostCents),
                ("$posted", project.PostedCents),
                ("$share", (long)project.ShareMilli),
                ("$risk", (long)project.RiskMilli),
                ("$outcome", project.OutcomeMilli is int outcome ? (long)outcome : null),
                ("$status", project.Status.ToString()),
                ("$timing", project.Timing.ToString()),
                ("$races", (long)project.TimingRaces),
                ("$waited", (long)project.RacesWaited),
                ("$closed", project.ClosedOn?.ToString()),
                ("$prodEnds", project.ProductionEnds?.ToString()),
                ("$prodCost", project.ProductionCostCents),
                ("$philosophy", (long)project.PhilosophyMilli),
                ("$aeroMilli", (long)project.AeroMilli),
                ("$ceilingMilli", (long)project.CeilingMilli),
                ("$flags", (long)project.Flags));
        }

        var seq = 0L;
        foreach (var note in development.Notes)
        {
            seq++;
            Run(
                connection,
                transaction,
                "INSERT INTO development_notes (seq, organization_id, on_date, source, delta_milli) VALUES ($seq, $org, $on, $source, $delta)",
                ("$seq", seq),
                ("$org", note.Organization.Value),
                ("$on", note.On.ToString()),
                ("$source", note.Source),
                ("$delta", (long)note.DeltaMilli));
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion != 1)
        {
            throw new InvalidDataException($"The stored development section has schema version {storedSchemaVersion}, which this build cannot read.");
        }

        long next;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT next_project FROM development_counter WHERE id = 1";
            next = command.ExecuteScalar() is long value
                ? value
                : throw new InvalidDataException("The development section has no counter row.");
        }

        var plans = new List<DevelopmentPlan>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT organization_id, current_percent, account_percent, next_year_percent, aero_priority, chassis_priority, "
                + "reliability_priority, tyres_priority, spent_current, spent_account, spent_next_year, changed_on, "
                + "next_philosophy_milli, next_aero_milli FROM development_plans ORDER BY organization_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                plans.Add(new DevelopmentPlan(
                    OrganizationIdFrom(reader.GetString(0)),
                    checked((int)reader.GetInt64(1)),
                    checked((int)reader.GetInt64(2)),
                    checked((int)reader.GetInt64(3)),
                    checked((int)reader.GetInt64(4)),
                    checked((int)reader.GetInt64(5)),
                    checked((int)reader.GetInt64(6)),
                    checked((int)reader.GetInt64(7)),
                    reader.GetInt64(8),
                    reader.GetInt64(9),
                    reader.GetInt64(10),
                    ParseOptionalDate(reader, 11),
                    checked((int)reader.GetInt64(12)),
                    checked((int)reader.GetInt64(13))));
            }
        }

        var accounts = new List<DevelopmentAccount>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT organization_id, stock_milli, next_year_share_milli, rules_year, concept_year FROM development_accounts ORDER BY organization_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                accounts.Add(new DevelopmentAccount(
                    OrganizationIdFrom(reader.GetString(0)),
                    checked((int)reader.GetInt64(1)),
                    checked((int)reader.GetInt64(2)),
                    checked((int)reader.GetInt64(3)),
                    checked((int)reader.GetInt64(4))));
            }
        }

        var projects = new List<DevProject>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT number, organization_id, kind, area, engineer, started, duration_days, progress_days, cost, posted, share_milli, risk_milli, "
                + "outcome_milli, status, timing, timing_races, races_waited, closed_on, production_ends, production_cost, "
                + "philosophy_milli, aero_milli, ceiling_milli, flags FROM development_projects ORDER BY number";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                projects.Add(new DevProject(
                    reader.GetInt64(0),
                    OrganizationIdFrom(reader.GetString(1)),
                    ParseEnum<DevKind>(reader.GetString(2)),
                    reader.IsDBNull(3) ? null : ParseEnum<DevArea>(reader.GetString(3)),
                    reader.GetString(4),
                    ParseDate(reader.GetString(5)),
                    checked((int)reader.GetInt64(6)),
                    checked((int)reader.GetInt64(7)),
                    reader.GetInt64(8),
                    reader.GetInt64(9),
                    checked((int)reader.GetInt64(10)),
                    checked((int)reader.GetInt64(11)),
                    reader.IsDBNull(12) ? null : checked((int)reader.GetInt64(12)),
                    ParseEnum<ProjectStatus>(reader.GetString(13)),
                    ParseEnum<ConceptTiming>(reader.GetString(14)),
                    checked((int)reader.GetInt64(15)),
                    checked((int)reader.GetInt64(16)),
                    ParseOptionalDate(reader, 17),
                    ParseOptionalDate(reader, 18),
                    reader.GetInt64(19),
                    checked((int)reader.GetInt64(20)),
                    checked((int)reader.GetInt64(21)),
                    checked((int)reader.GetInt64(22)),
                    checked((int)reader.GetInt64(23))));
            }
        }

        var notes = new List<UnderstandingNote>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT organization_id, on_date, source, delta_milli FROM development_notes ORDER BY seq";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                notes.Add(new UnderstandingNote(
                    OrganizationIdFrom(reader.GetString(0)),
                    ParseDate(reader.GetString(1)),
                    reader.GetString(2),
                    checked((int)reader.GetInt64(3))));
            }
        }

        return DevelopmentSection.Restore(next, plans, accounts, projects, notes);
    }
}
