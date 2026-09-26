using System.Globalization;
using System.Text;

namespace Paddock.DataPipeline;

public static class EraStatsText
{
    public const string Methodology =
        """
        Main figures use Grand Prix result rows only. A row is left out when it is a shared drive
        (two or more drivers on the same car number and constructor in one race) or when the round
        is the Indianapolis 500 (circuit indianapolis, seasons 1950–1960). Those two groups are
        tallied below and are not mixed back in. Sprint results are not included.

        A starter is a row whose status is not a non-start (Withdrew, Did not start, Did not qualify,
        Did not prequalify, 107% Rule). Classified finishers are starters whose positionText is all
        digits. Every other starter is a DNF. DNF statuses are mapped to mechanical, accident, or
        other in FinishStatus. A status that is not in that table is unmapped and listed here; it
        is not folded into other. Bucket percents are shares of DNFs. Classified percent is the
        share of starters.

        Winning margin is second place minus the winner, for races where both have a time. Season
        and decade values are medians of those race margins. The count in parentheses is how many
        races had both times. Wins from pole are winners whose grid is 1. Winner and pole-sitter
        counts are distinct driver ids. Dominance is the share of wins of the constructor with the
        most wins. A tie uses the id that sorts first.

        Championship points sum the points Jolpica recorded on the included rows, after the era's
        drop-score rule (results_counted in data/authored/regulations/f1_timeline.json), including
        split-season quotas. The points scale is not reapplied. Champion's share is that total
        divided by every driver's counted points. Decade champion share is the unweighted mean of
        the season shares. Other decade rates pool every race in the decade.

        The title is decided in the final included round when, before that round, another driver
        could still match the leader. The most available there is a win on that year's scale, plus
        one fastest-lap point in seasons that paid one, doubled at the 2014 finale. A tie on points
        is still open. Countback (wins, then second places, and so on) names the champion only
        after the season; it does not clinch the title early.
        """;

    public static IReadOnlyList<string> SummaryLines(EraStatsReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var lines = new List<string>
        {
            "era stats " + Num(report.FromYear) + "-" + Num(report.ToYear),
            "main races: " + Num(report.Main.Seasons.Sum(season => season.Races)),
            "shared-drive rows: " + Num(report.SharedDrives.Seasons.Sum(season => season.Entries)),
            "shared-drive races: " + Num(report.SharedDrives.Seasons.Sum(season => season.Races)),
            "indianapolis 500 races: " + Num(report.Indianapolis500.Seasons.Sum(season => season.Races)),
            "unmapped statuses: " + Unmapped(report),
        };
        foreach (var decade in report.Main.Decades)
        {
            lines.Add(DecadeSummary(decade));
        }

        return lines;
    }

    public static string Markdown(EraStatsReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var text = new StringBuilder();
        text.Append("# Era statistics (");
        text.Append(Num(report.FromYear));
        text.Append('–');
        text.Append(Num(report.ToYear));
        text.Append(")\n\n");
        text.Append(Methodology);
        text.Append("\n\n## Unmapped statuses\n\n");
        text.Append(Unmapped(report));
        text.Append("\n\n## Main figures by decade\n\n");
        AppendPopulation(text, report.Main, championship: true);
        text.Append("\n## Main figures by season\n\n");
        AppendSeasons(text, report.Main, championship: true);
        text.Append("\n## Shared drives\n\n");
        text.Append("Rows from shared cars in rounds that are not the Indianapolis 500.\n\n");
        AppendPopulation(text, report.SharedDrives, championship: false);
        text.Append("\n");
        AppendSeasons(text, report.SharedDrives, championship: false);
        text.Append("\n## Indianapolis 500\n\n");
        text.Append("Championship rounds at Indianapolis, 1950–1960, including any shared-drive rows in those rounds.\n\n");
        AppendPopulation(text, report.Indianapolis500, championship: false);
        text.Append("\n");
        AppendSeasons(text, report.Indianapolis500, championship: false);
        return text.ToString();
    }

    private static void AppendPopulation(StringBuilder text, PopulationReport population, bool championship)
    {
        if (population.Decades.Count == 0)
        {
            text.Append("No races.\n");
            return;
        }

        text.Append("| Decade | Races | Starters/race | Classified | Mechanical | Accident | Other | Unmapped |\n");
        text.Append("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |\n");
        foreach (var decade in population.Decades)
        {
            text.Append("| ");
            text.Append(Num(decade.DecadeStart));
            text.Append("s | ");
            text.Append(Num(decade.Races));
            text.Append(" | ");
            text.Append(Avg(decade.Starters, decade.Races));
            text.Append(" | ");
            text.Append(Pct(decade.Classified, decade.Starters));
            text.Append(" | ");
            text.Append(Bucket(decade.DnfMechanical, decade));
            text.Append(" | ");
            text.Append(Bucket(decade.DnfAccident, decade));
            text.Append(" | ");
            text.Append(Bucket(decade.DnfOther, decade));
            text.Append(" | ");
            text.Append(Bucket(decade.DnfUnmapped, decade));
            text.Append(" |\n");
        }

        text.Append("\n| Decade | Median margin | Wins from pole | Winners | Pole sitters | Top constructor |");
        if (championship)
        {
            text.Append(" Champion share | Final-race titles |");
        }

        text.Append("\n| --- | ---: | ---: | ---: | ---: | --- |");
        if (championship)
        {
            text.Append(" ---: | ---: |");
        }

        text.Append('\n');
        foreach (var decade in population.Decades)
        {
            text.Append("| ");
            text.Append(Num(decade.DecadeStart));
            text.Append("s | ");
            text.Append(Margin(decade.MedianWinningMarginMillis, decade.RacesWithWinningMargin));
            text.Append(" | ");
            text.Append(Pct(decade.WinsFromPole, decade.Wins));
            text.Append(" | ");
            text.Append(Num(decade.DifferentWinners));
            text.Append(" | ");
            text.Append(Num(decade.DifferentPoleSitters));
            text.Append(" | ");
            text.Append(Constructor(decade.TopConstructorId, decade.TopConstructorName, decade.TopConstructorWins, decade.Wins));
            if (championship)
            {
                text.Append(" | ");
                text.Append(Share(decade.MeanChampionShare));
                text.Append(" | ");
                text.Append(Titles(decade.TitlesDecidedInFinalRace, decade.SeasonsWithChampionship));
            }

            text.Append(" |\n");
        }
    }

    private static void AppendSeasons(StringBuilder text, PopulationReport population, bool championship)
    {
        if (population.Seasons.Count == 0)
        {
            text.Append("No races.\n");
            return;
        }

        text.Append("| Season | Races | Starters/race | Classified | Mechanical | Accident | Other | Unmapped |\n");
        text.Append("| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |\n");
        foreach (var season in population.Seasons)
        {
            text.Append("| ");
            text.Append(Num(season.Season));
            text.Append(" | ");
            text.Append(Num(season.Races));
            text.Append(" | ");
            text.Append(Avg(season.Starters, season.Races));
            text.Append(" | ");
            text.Append(Pct(season.Classified, season.Starters));
            text.Append(" | ");
            text.Append(Bucket(season.DnfMechanical, season));
            text.Append(" | ");
            text.Append(Bucket(season.DnfAccident, season));
            text.Append(" | ");
            text.Append(Bucket(season.DnfOther, season));
            text.Append(" | ");
            text.Append(Bucket(season.DnfUnmapped, season));
            text.Append(" |\n");
        }

        text.Append("\n| Season | Median margin | Wins from pole | Winners | Pole sitters | Top constructor |");
        if (championship)
        {
            text.Append(" Champion | Final-race title |");
        }

        text.Append("\n| ---: | ---: | ---: | ---: | ---: | --- |");
        if (championship)
        {
            text.Append(" --- | --- |");
        }

        text.Append('\n');
        foreach (var season in population.Seasons)
        {
            text.Append("| ");
            text.Append(Num(season.Season));
            text.Append(" | ");
            text.Append(Margin(season.MedianWinningMarginMillis, season.RacesWithWinningMargin));
            text.Append(" | ");
            text.Append(Pct(season.WinsFromPole, season.Wins));
            text.Append(" | ");
            text.Append(Num(season.DifferentWinners));
            text.Append(" | ");
            text.Append(Num(season.DifferentPoleSitters));
            text.Append(" | ");
            text.Append(Constructor(season.TopConstructorId, season.TopConstructorName, season.TopConstructorWins, season.Wins));
            if (championship)
            {
                text.Append(" | ");
                text.Append(Champion(season));
                text.Append(" | ");
                text.Append(YesNo(season.TitleDecidedInFinalRace));
            }

            text.Append(" |\n");
        }
    }

    private static string DecadeSummary(DecadeReport decade)
    {
        return "decade " + Num(decade.DecadeStart) + "s: races " + Num(decade.Races)
            + ", starters/race " + Avg(decade.Starters, decade.Races)
            + ", classified " + Pct(decade.Classified, decade.Starters)
            + ", dnf mechanical " + Num(decade.DnfMechanical)
            + ", accident " + Num(decade.DnfAccident)
            + ", other " + Num(decade.DnfOther)
            + ", unmapped " + Num(decade.DnfUnmapped)
            + ", median margin " + Margin(decade.MedianWinningMarginMillis, decade.RacesWithWinningMargin)
            + ", wins from pole " + Pct(decade.WinsFromPole, decade.Wins)
            + ", winners " + Num(decade.DifferentWinners)
            + ", pole sitters " + Num(decade.DifferentPoleSitters)
            + ", champion share " + Share(decade.MeanChampionShare)
            + ", titles to final race " + Titles(decade.TitlesDecidedInFinalRace, decade.SeasonsWithChampionship)
            + ", top constructor " + (decade.TopConstructorId is null
                ? "n/a"
                : decade.TopConstructorId + " " + Pct(decade.TopConstructorWins, decade.Wins));
    }

    private static string Unmapped(EraStatsReport report)
    {
        if (report.UnmappedStatuses.Count == 0)
        {
            return "none";
        }

        return string.Join(", ", report.UnmappedStatuses.Select(item => item.Status + " (" + Num(item.Count) + ")"));
    }

    private static string Bucket(int count, DecadeReport decade)
    {
        return CountAndPct(count, decade.DnfMechanical + decade.DnfAccident + decade.DnfOther + decade.DnfUnmapped);
    }

    private static string Bucket(int count, SeasonReport season)
    {
        return CountAndPct(count, season.DnfMechanical + season.DnfAccident + season.DnfOther + season.DnfUnmapped);
    }

    private static string CountAndPct(int count, int total)
    {
        if (total == 0)
        {
            return Num(count);
        }

        return Num(count) + " (" + Pct(count, total) + ")";
    }

    private static string Constructor(string? id, string? name, int wins, int totalWins)
    {
        if (id is null)
        {
            return "n/a";
        }

        var label = name is null ? id : name + " (" + id + ")";
        return label + " " + Pct(wins, totalWins);
    }

    private static string Champion(SeasonReport season)
    {
        if (season.ChampionDriverId is null || season.ChampionShare is null)
        {
            return "n/a";
        }

        var who = season.ChampionName is null
            ? season.ChampionDriverId
            : season.ChampionName + " (" + season.ChampionDriverId + ")";
        return who + " " + Share(season.ChampionShare)
            + " (" + Points(season.ChampionPoints) + "/" + Points(season.PointsCounted) + ")";
    }

    private static string YesNo(bool? value)
    {
        return value switch
        {
            true => "yes",
            false => "no",
            _ => "n/a",
        };
    }

    private static string Titles(int? decided, int? seasons)
    {
        if (decided is null || seasons is null)
        {
            return "n/a";
        }

        return Num(decided.Value) + "/" + Num(seasons.Value);
    }

    private static string Share(decimal? share)
    {
        if (share is null)
        {
            return "n/a";
        }

        return decimal.Round(share.Value * 100m, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }

    private static string Margin(decimal? millis, int races)
    {
        if (millis is null)
        {
            return "n/a";
        }

        var seconds = decimal.Round(millis.Value / 1000m, 3, MidpointRounding.AwayFromZero);
        return seconds.ToString("0.000", CultureInfo.InvariantCulture) + "s (" + Num(races) + (races == 1 ? " race)" : " races)");
    }

    private static string Avg(int numerator, int denominator)
    {
        if (denominator == 0)
        {
            return "n/a";
        }

        return decimal.Round((decimal)numerator / denominator, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string Pct(int numerator, int denominator)
    {
        if (denominator == 0)
        {
            return "n/a";
        }

        return decimal.Round(100m * numerator / denominator, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }

    private static string Points(decimal? points)
    {
        return (points ?? 0m).ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string Num(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
