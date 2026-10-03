using System.Globalization;
using System.Text;
using Paddock.Data.Historical;

namespace Paddock.DataPipeline;

public static class PeopleScheduleText
{
    public static string Methodology()
    {
        var split = Num(PeopleScheduleRules.PoolDebutSplitSeason);
        var age = Num(PeopleScheduleRules.PoolMinimumAgeYears);
        var substitute = Num(PeopleScheduleRules.SubstituteMaxStarts);
        var floor = Num((int)decimal.Floor(PeopleScheduleRules.DefaultPoolLeadYears));
        var ceiling = Num((int)decimal.Ceiling(PeopleScheduleRules.DefaultPoolLeadYears));
        var lead = PeopleScheduleRules.DefaultPoolLeadYears.ToString(CultureInfo.InvariantCulture);
        return
            "Grand Prix stints come from race results. A start is a result whose status is not a non-start "
            + "(Withdrew, Did not start, Did not qualify, Did not prequalify, 107% Rule). A retirement counts as a start. "
            + "One stint is one season and one constructor. firstRound and lastRound are the earliest and latest start, "
            + "so a return to the same constructor in that season stays one stint. The gap is a team-change event.\n\n"
            + "Role race means more than " + substitute + " starts for that constructor in that season. "
            + "Role substitute means " + substitute + " or fewer. Shared-drive rows stay on their own stints with role shared_drive, "
            + "including when there is only one, and they are not seat changes. A driver with a single start is kept.\n\n"
            + "Indianapolis 500 rounds (circuit indianapolis, seasons 1950-1960) are excluded from stints and listed on the driver. "
            + "That exclusion wins when the Indy row is also a shared drive.\n\n"
            + "Pool entry year is max(first Grand Prix season - lead, birth year + " + age + "). "
            + "The default lead " + lead + " is a placeholder until calibration (ROADMAP open question 4): "
            + "debut before " + split + " uses " + floor + " years, debut in " + split + " or later uses " + ceiling + ". "
            + "An explicit whole-year --pool-lead-years value is used for every debut. "
            + "An unknown birth year skips the age floor. The age floor is not pulled back when it falls after the debut.\n\n"
            + "Team changes walk Grand Prix starts in round order. Shared drives and Indianapolis starts are not moves. "
            + "A change inside one season records the round of the first start for the new constructor. "
            + "A change between seasons records the new season and no round.\n\n"
            + "Two ids that normalize to the same given name and family name (case, accents, hyphens, and apostrophes ignored), "
            + "or that share a birth date and family name, are duplicate suspects. They are not merged.\n\n"
            + "Decade driver counts are distinct drivers with a Grand Prix stint in that decade. "
            + "Stint counts are Grand Prix stints, including shared drives. Indianapolis-only drivers are outside those decades.";
    }

    public static IReadOnlyList<string> SummaryLines(PeopleScheduleReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var stints = report.Drivers.Sum(driver => driver.Stints.Count);
        var shared = report.Drivers.Sum(driver => driver.Stints.Count(stint => stint.Role == ScheduleRoles.SharedDrive));
        var indyStarts = report.Drivers.Sum(driver => driver.Indianapolis500.Count);
        var indyOnly = report.Drivers.Count(driver => driver.Stints.Count == 0 && driver.Indianapolis500.Count > 0);
        var lines = new List<string>
        {
            "people schedule",
            "drivers: " + Num(report.Drivers.Count),
            "stints: " + Num(stints),
            "shared-drive stints: " + Num(shared),
            "team changes: " + Num(report.TeamChanges.Count),
            "mid-season changes: " + Num(report.TeamChanges.Count(change => change.Round is not null)),
            "duplicate suspects: " + Num(report.DuplicateSuspects.Count),
            "single-start drivers: " + Num(report.SingleStartDriverIds.Count),
            "indianapolis 500 starts: " + Num(indyStarts),
            "indianapolis-only drivers: " + Num(indyOnly),
            LeadLine(report),
        };
        foreach (var decade in Decades(report))
        {
            lines.Add(
                "decade "
                + Num(decade.Decade)
                + "s: drivers "
                + Num(decade.Drivers)
                + ", stints "
                + Num(decade.Stints));
        }

        return lines;
    }

    public static string Markdown(PeopleScheduleReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var text = new StringBuilder();
        text.Append("# People schedule\n\n");
        text.Append(Methodology());
        text.Append("\n\n## Counts\n\n");
        foreach (var line in SummaryLines(report))
        {
            if (line == "people schedule")
            {
                continue;
            }

            text.Append("- ");
            text.Append(line);
            text.Append('\n');
        }

        text.Append("\n## Team changes\n\n");
        if (report.TeamChanges.Count == 0)
        {
            text.Append("None.\n");
        }
        else
        {
            text.Append("| Season | Round | Driver | From | To |\n");
            text.Append("| ---: | ---: | --- | --- | --- |\n");
            foreach (var change in report.TeamChanges)
            {
                text.Append("| ");
                text.Append(Num(change.Season));
                text.Append(" | ");
                text.Append(change.Round is null ? "" : Num(change.Round.Value));
                text.Append(" | ");
                text.Append(change.DriverId);
                text.Append(" | ");
                text.Append(change.FromConstructorId);
                text.Append(" | ");
                text.Append(change.ToConstructorId);
                text.Append(" |\n");
            }
        }

        text.Append("\n## Duplicate driver ids\n\n");
        if (report.DuplicateSuspects.Count == 0)
        {
            text.Append("None.\n");
        }
        else
        {
            text.Append("Listed, not merged.\n\n");
            text.Append("| Driver | Other | Reason |\n");
            text.Append("| --- | --- | --- |\n");
            foreach (var suspect in report.DuplicateSuspects)
            {
                text.Append("| ");
                text.Append(suspect.DriverId);
                text.Append(" | ");
                text.Append(suspect.OtherDriverId);
                text.Append(" | ");
                text.Append(suspect.Reason);
                text.Append(" |\n");
            }
        }

        text.Append("\n## Single-start drivers\n\n");
        if (report.SingleStartDriverIds.Count == 0)
        {
            text.Append("None.\n");
        }
        else
        {
            text.Append(string.Join(", ", report.SingleStartDriverIds));
            text.Append('\n');
        }

        text.Append("\n## Indianapolis 500\n\n");
        var indyRows = report.Drivers.SelectMany(driver => driver.Indianapolis500.Select(appearance => (driver.DriverId, appearance))).ToList();
        if (indyRows.Count == 0)
        {
            text.Append("None.\n");
        }
        else
        {
            text.Append("| Season | Round | Driver | Constructor |\n");
            text.Append("| ---: | ---: | --- | --- |\n");
            foreach (var row in indyRows)
            {
                text.Append("| ");
                text.Append(Num(row.appearance.Season));
                text.Append(" | ");
                text.Append(Num(row.appearance.Round));
                text.Append(" | ");
                text.Append(row.DriverId);
                text.Append(" | ");
                text.Append(row.appearance.ConstructorId);
                text.Append(" |\n");
            }
        }

        text.Append("\n## Drivers\n\n");
        if (report.Drivers.Count == 0)
        {
            text.Append("No drivers.\n");
            return text.ToString();
        }

        foreach (var driver in report.Drivers)
        {
            text.Append("### ");
            text.Append(driver.DriverId);
            text.Append("\n\n");
            text.Append("- Born: ");
            text.Append(driver.Born is null ? "unknown" : Num(driver.Born.Value));
            text.Append("\n- Nationality: ");
            text.Append(string.IsNullOrEmpty(driver.Nationality) ? "unknown" : driver.Nationality);
            text.Append("\n- Seasons: ");
            text.Append(driver.FirstSeason is null
                ? "none"
                : Num(driver.FirstSeason.Value) + "-" + Num(driver.LastSeason!.Value));
            text.Append("\n- Pool entry year: ");
            text.Append(driver.PoolEntryYear is null ? "none" : Num(driver.PoolEntryYear.Value));
            text.Append("\n\n");
            if (driver.Stints.Count == 0)
            {
                text.Append("No Grand Prix stints.\n\n");
            }
            else
            {
                text.Append("| Season | Constructor | First | Last | Starts | Role |\n");
                text.Append("| ---: | --- | ---: | ---: | ---: | --- |\n");
                foreach (var stint in driver.Stints)
                {
                    text.Append("| ");
                    text.Append(Num(stint.Season));
                    text.Append(" | ");
                    text.Append(stint.ConstructorId);
                    text.Append(" | ");
                    text.Append(Num(stint.FirstRound));
                    text.Append(" | ");
                    text.Append(Num(stint.LastRound));
                    text.Append(" | ");
                    text.Append(Num(stint.Starts));
                    text.Append(" | ");
                    text.Append(stint.Role);
                    text.Append(" |\n");
                }

                text.Append('\n');
            }
        }

        return text.ToString();
    }

    private static string LeadLine(PeopleScheduleReport report)
    {
        var lead = report.PoolLeadYears.ToString(CultureInfo.InvariantCulture);
        if (report.PoolLeadYears == decimal.Truncate(report.PoolLeadYears))
        {
            return "pool lead years: " + lead + " (whole years for every debut)";
        }

        var before = Num((int)decimal.Floor(report.PoolLeadYears));
        var after = Num((int)decimal.Ceiling(report.PoolLeadYears));
        return "pool lead years: " + lead + " (" + before + " before " + Num(report.DebutSplitSeason) + ", " + after + " from " + Num(report.DebutSplitSeason) + ")";
    }

    private static List<DecadeCount> Decades(PeopleScheduleReport report)
    {
        var drivers = new Dictionary<int, HashSet<string>>();
        var stints = new Dictionary<int, int>();
        foreach (var driver in report.Drivers)
        {
            foreach (var stint in driver.Stints)
            {
                var decade = stint.Season / 10 * 10;
                if (!drivers.TryGetValue(decade, out var ids))
                {
                    ids = new HashSet<string>(StringComparer.Ordinal);
                    drivers[decade] = ids;
                }

                ids.Add(driver.DriverId);
                stints.TryGetValue(decade, out var count);
                stints[decade] = count + 1;
            }
        }

        return drivers.Keys
            .OrderBy(decade => decade)
            .Select(decade => new DecadeCount(decade, drivers[decade].Count, stints[decade]))
            .ToList();
    }

    private static string Num(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private readonly record struct DecadeCount(int Decade, int Drivers, int Stints);
}
