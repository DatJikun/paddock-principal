using System.Globalization;
using System.Text;

namespace Paddock.DataPipeline;

public static class RatingsMarkdown
{
    public static string Format(RatingsReportDocument report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();
        var fit = report.FitSummary;

        sb.AppendLine("# Driver ratings v0 — teammate comparison model");
        sb.AppendLine();
        sb.AppendLine($"Seasons: {fit.FromYear}–{fit.ToYear} | wRace={fit.WRace:F1}, wQuali={fit.WQuali:F1}, lambdaTime={fit.LambdaTime:F2}, lambda0={fit.Lambda0:F4}");
        sb.AppendLine();

        sb.AppendLine("## 1. Fit summary");
        sb.AppendLine();
        sb.AppendLine($"- **Races analyzed:** {fit.RacesCount}");
        sb.AppendLine($"- **Total duels:** {fit.TotalDuels} ({fit.RaceDuels} race, {fit.QualifyingDuels} qualifying)");
        sb.AppendLine($"- **Driver-seasons with duels:** {fit.DriverSeasonsCount}");
        sb.AppendLine($"- **Optimization:** {fit.Iterations} iterations, converged: {(fit.Converged ? "yes" : "no")} (max gradient component: {fit.MaxGradient:E2})");
        sb.AppendLine($"- **Teammate graph components:** {fit.ComponentsCount} (largest component: {fit.LargestComponentSize} drivers)");
        sb.AppendLine();

        sb.AppendLine("## 2. All-time top 50 by career peak");
        sb.AppendLine();
        sb.AppendLine("Ranked drivers belong to the largest teammate graph component and have ≥ 20 duels in total.");
        sb.AppendLine("Career peak is the best average skill `s` over 3 consecutive seasons with duels (flagged `*` if shorter career).");
        sb.AppendLine();
        sb.AppendLine("| Rank | Driver | Peak | SE | Peak Years | Duels |");
        sb.AppendLine("| ---: | :--- | ---: | ---: | :--- | ---: |");

        foreach (var entry in report.AllTimeTop50)
        {
            var shortFlag = entry.ShortCareer ? " *" : string.Empty;
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"| {entry.Rank} | {entry.Name}{shortFlag} | {entry.CareerPeak:F3} | {entry.PeakSe:F3} | {entry.PeakYears} | {entry.TotalDuels} |"));
        }
        sb.AppendLine();

        sb.AppendLine("## 3. Top 10 per decade");
        sb.AppendLine();
        sb.AppendLine("Ranked by the mean skill `s` of the driver's seasons in each decade (minimum 20 total career duels, largest component).");
        sb.AppendLine();

        foreach (var decade in report.TopByDecade)
        {
            sb.AppendLine($"### {decade.DecadeStart}s");
            sb.AppendLine();
            sb.AppendLine("| Rank | Driver | Decade Mean | Seasons | Career Duels |");
            sb.AppendLine("| ---: | :--- | ---: | ---: | ---: |");

            foreach (var d in decade.Drivers)
            {
                sb.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"| {d.Rank} | {d.Name} | {d.MeanSkill:F3} | {d.SeasonsCount} | {d.CareerDuels} |"));
            }
            sb.AppendLine();
        }

        sb.AppendLine("## 4. Comparison with reference rankings");
        sb.AppendLine();
        sb.AppendLine("Evaluated over drivers present in both rankings and ranked by our model (largest component, ≥ 20 duels).");
        sb.AppendLine();

        if (report.ReferenceComparisons.Count == 0)
        {
            sb.AppendLine("*No reference rankings found.*");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("| Reference Ranking | Overlap | Spearman ρ | Top Disagreements (driver: their rank vs our rank) |");
            sb.AppendLine("| :--- | ---: | ---: | :--- |");

            foreach (var comp in report.ReferenceComparisons)
            {
                var spearmanText = comp.SpearmanCorrelation.HasValue
                    ? comp.SpearmanCorrelation.Value.ToString("F3", CultureInfo.InvariantCulture)
                    : "n/a";

                var disagreementsText = comp.TopDisagreements.Count > 0
                    ? string.Join("; ", comp.TopDisagreements.Select(d => $"{d.Name} ({d.TheirRank} vs {d.OurRank})"))
                    : "none";

                sb.AppendLine($"| {comp.RankingTitle} | {comp.OverlapCount} | {spearmanText} | {disagreementsText} |");
            }
            sb.AppendLine();

            foreach (var comp in report.ReferenceComparisons)
            {
                if (comp.TopDisagreements.Count == 0)
                {
                    continue;
                }

                sb.AppendLine($"### Disagreements: {comp.RankingTitle}");
                sb.AppendLine();
                sb.AppendLine("| Driver | Their Rank | Our Rank | Difference |");
                sb.AppendLine("| :--- | ---: | ---: | ---: |");

                foreach (var d in comp.TopDisagreements)
                {
                    sb.AppendLine($"| {d.Name} | {d.TheirRank} | {d.OurRank} | {d.Difference} |");
                }
                sb.AppendLine();
            }
        }

        if (report.InsufficientDataDrivers.Count > 0)
        {
            sb.AppendLine($"## Insufficient data ({report.InsufficientDataDrivers.Count} drivers with < 20 duels)");
            sb.AppendLine();
            sb.AppendLine("| Driver | Total Duels | Seasons |");
            sb.AppendLine("| :--- | ---: | ---: |");
            foreach (var d in report.InsufficientDataDrivers.Take(25))
            {
                sb.AppendLine($"| {d.Name} | {d.TotalDuels} | {d.SeasonsCount} |");
            }
            if (report.InsufficientDataDrivers.Count > 25)
            {
                sb.AppendLine($"| ... and {report.InsufficientDataDrivers.Count - 25} more | | |");
            }
            sb.AppendLine();
        }

        if (report.DisconnectedDrivers.Count > 0)
        {
            sb.AppendLine($"## Disconnected components ({report.DisconnectedDrivers.Count} drivers outside largest component)");
            sb.AppendLine();
            sb.AppendLine("| Driver | Component Index | Component Size | Total Duels |");
            sb.AppendLine("| :--- | ---: | ---: | ---: |");
            foreach (var d in report.DisconnectedDrivers)
            {
                sb.AppendLine($"| {d.Name} | {d.ComponentIndex} | {d.ComponentSize} | {d.TotalDuels} |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static IReadOnlyList<string> SummaryLines(RatingsReportDocument report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var fit = report.FitSummary;
        var lines = new List<string>
        {
            $"ratings fit {fit.FromYear}–{fit.ToYear}",
            $"races: {fit.RacesCount}, duels: {fit.TotalDuels} (race {fit.RaceDuels}, quali {fit.QualifyingDuels})",
            $"driver-seasons: {fit.DriverSeasonsCount}, iterations: {fit.Iterations}, converged: {(fit.Converged ? "yes" : "no")}",
            $"components: {fit.ComponentsCount} (largest: {fit.LargestComponentSize} drivers)",
            $"all-time ranked drivers (>= 20 duels): {report.AllTimeTop50.Count} (top 50 listed)",
        };

        if (report.AllTimeTop50.Count > 0)
        {
            lines.Add("top 10 all-time:");
            foreach (var d in report.AllTimeTop50.Take(10))
            {
                lines.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"  #{d.Rank}: {d.Name} ({d.CareerPeak:F3} ± {d.PeakSe:F3}, {d.PeakYears}, {d.TotalDuels} duels)"));
            }
        }

        if (report.ReferenceComparisons.Count > 0)
        {
            lines.Add("reference comparisons:");
            foreach (var comp in report.ReferenceComparisons)
            {
                var spearman = comp.SpearmanCorrelation.HasValue
                    ? comp.SpearmanCorrelation.Value.ToString("F3", CultureInfo.InvariantCulture)
                    : "n/a";
                lines.Add($"  {comp.RankingId}: n={comp.OverlapCount}, rho={spearman}");
            }
        }

        return lines;
    }
}
