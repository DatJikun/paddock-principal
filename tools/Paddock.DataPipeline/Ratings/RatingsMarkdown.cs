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

        sb.AppendLine("# Driver ratings v1 — teammate comparison model with car effect");
        sb.AppendLine();
        sb.AppendLine($"Seasons: {fit.FromYear}–{fit.ToYear} | wRace={fit.WRace:F1}, wQuali={fit.WQuali:F1}, lambdaTime={fit.LambdaTime:F2}, lambda0={fit.Lambda0:F4}");
        sb.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Car effect: lambdaC0={fit.LambdaC0:F4}, lambdaCTime={fit.LambdaCTime:F2}, wCross={(fit.WCross.HasValue ? fit.WCross.Value.ToString("F4", CultureInfo.InvariantCulture) : "auto (1 / (field size - 1))")}; career curve: lambdaCurve={fit.LambdaCurve:F2}"));
        sb.AppendLine();
        sb.AppendLine("Identifiability of driver skill vs. car effect comes from teammate duels (the car cancels), driver transfers between constructors and the ridge terms. Overall, stars, ceiling and all mapping constants are **estimates** (see `RatingsMapping.cs`), not calibrated.");
        sb.AppendLine();

        sb.AppendLine("## 1. Fit summary");
        sb.AppendLine();
        sb.AppendLine($"- **Races analyzed:** {fit.RacesCount}");
        sb.AppendLine($"- **Teammate duels:** {fit.TotalDuels} ({fit.RaceDuels} race, {fit.QualifyingDuels} qualifying)");
        sb.AppendLine($"- **Cross-constructor duels:** {fit.CrossRaceDuels + fit.CrossQualifyingDuels} ({fit.CrossRaceDuels} race, {fit.CrossQualifyingDuels} qualifying)");
        sb.AppendLine($"- **Constructor-seasons with a car effect:** {fit.CarSeasonsCount}");
        sb.AppendLine($"- **Driver-seasons with duels:** {fit.DriverSeasonsCount}");
        sb.AppendLine($"- **Optimization:** {fit.Iterations} iterations, converged: {(fit.Converged ? "yes" : "no")} (max gradient component: {fit.MaxGradient:E2})");
        sb.AppendLine($"- **Teammate graph components:** {fit.ComponentsCount} (largest component: {fit.LargestComponentSize} drivers)");
        sb.AppendLine();

        sb.AppendLine("## 2. All-time top 50 by smoothed peak");
        sb.AppendLine();
        sb.AppendLine("Ranked drivers belong to the largest teammate graph component and have ≥ 20 duels in total.");
        sb.AppendLine("Peak is the maximum of the smoothed career curve (drivers with >= 4 seasons); shorter careers use the v0 peak, the best average skill `s` over 3 consecutive seasons (flagged `*`). SE and Peak Years belong to the v0 3-season peak. Overall (1-100) and stars (0-5) are percentile mappings with guessed anchors.");
        sb.AppendLine();
        sb.AppendLine("| Rank | Driver | Overall | Stars | Peak | v0 Peak | SE | Peak Years | Duels |");
        sb.AppendLine("| ---: | :--- | ---: | ---: | ---: | ---: | ---: | :--- | ---: |");

        foreach (var entry in report.AllTimeTop50)
        {
            var shortFlag = entry.ShortCareer ? " *" : string.Empty;
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"| {entry.Rank} | {entry.Name}{shortFlag} | {entry.Overall} | {entry.Stars:F1} | {entry.PeakValue:F3} | {entry.CareerPeak:F3} | {entry.PeakSe:F3} | {entry.PeakYears} | {entry.TotalDuels} |"));
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

        sb.AppendLine("## 4. Car effect per decade");
        sb.AppendLine();
        sb.AppendLine("Mean car effect `c` of each constructor lineage over the decade's seasons (top and bottom 5). Higher is a faster car. Estimates.");
        sb.AppendLine();

        foreach (var decade in report.CarEffectsByDecade)
        {
            sb.AppendLine($"### {decade.DecadeStart}s");
            sb.AppendLine();
            sb.AppendLine("| Best cars | Mean c | Seasons | Worst cars | Mean c | Seasons |");
            sb.AppendLine("| :--- | ---: | ---: | :--- | ---: | ---: |");
            var rows = Math.Max(decade.Top.Count, decade.Bottom.Count);
            for (var i = 0; i < rows; i++)
            {
                var top = i < decade.Top.Count ? decade.Top[i] : null;
                var bottom = i < decade.Bottom.Count ? decade.Bottom[i] : null;
                sb.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"| {top?.ConstructorKey} | {(top is null ? string.Empty : top.MeanEffect.ToString("F3", CultureInfo.InvariantCulture))} | {top?.SeasonsCount} | {bottom?.ConstructorKey} | {(bottom is null ? string.Empty : bottom.MeanEffect.ToString("F3", CultureInfo.InvariantCulture))} | {bottom?.SeasonsCount} |"));
            }
            sb.AppendLine();
        }

        sb.AppendLine("## 5. Career curves (top 50)");
        sb.AppendLine();
        sb.AppendLine("Penalised second-difference smoothing of `s[d,t]` for drivers with >= 4 seasons. Decline rate = mean yearly change after the peak. Ceiling = peak + 1 se of the raw peak-season skill (**an estimate**). Age = peak season minus birth year.");
        sb.AppendLine();
        sb.AppendLine("| Rank | Driver | Peak Season | Age | Peak | Decline / yr | Ceiling (est.) |");
        sb.AppendLine("| ---: | :--- | ---: | ---: | ---: | ---: | ---: |");
        foreach (var r in report.DriverRatings.Take(50))
        {
            if (r.Curve is null)
            {
                continue;
            }

            var c = r.Curve;
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"| {r.Rank} | {r.Name} | {c.PeakSeason} | {(c.AgeAtPeak.HasValue ? c.AgeAtPeak.Value.ToString(CultureInfo.InvariantCulture) : "n/a")} | {c.PeakValue:F3} | {c.DeclineRate:F3} | {c.Ceiling:F3} |"));
        }
        sb.AppendLine();

        sb.AppendLine("## 6. Comparison with reference rankings");
        sb.AppendLine();
        sb.AppendLine("Evaluated over drivers present in both rankings and ranked by our model (v1 peak; largest component, ≥ 20 teammate duels).");
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
            $"races: {fit.RacesCount}, teammate duels: {fit.TotalDuels} (race {fit.RaceDuels}, quali {fit.QualifyingDuels}), cross duels: {fit.CrossRaceDuels + fit.CrossQualifyingDuels}",
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
                    $"  #{d.Rank}: {d.Name} (peak {d.PeakValue:F3}, overall {d.Overall}, {d.Stars:F1} stars, {d.TotalDuels} duels)"));
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
