using System.Globalization;
using System.Text;
using Paddock.Domain.Time;

namespace Paddock.SimRunner.Scenario;

/// <summary>Markdown the owner reads. Reports go under <c>data/cache/reports/</c> (git-ignored).</summary>
public static class Phase4Report
{
    public static string Write(string directory, Phase4Pack pack)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "phase4-1955.md");
        File.WriteAllText(path, Markdown(pack));
        return path;
    }

    public static string Markdown(Phase4Pack pack)
    {
        var text = new StringBuilder();
        text.AppendLine("# Phase 4 gate — 1955 season");
        text.AppendLine();
        text.AppendLine("Owner judgement: whether decisions have felt consequences. This report is evidence, not a pass of the gate.");
        text.AppendLine("No comparison with real history (PP-062).");
        text.AppendLine();
        text.AppendLine("## Checklist");
        text.AppendLine();
        text.AppendLine("| Check | Result | Number | Threshold | Notes |");
        text.AppendLine("|---|---|---|---|---|");
        Row(text, "scripted story hash (twice)", pack.Story.WorldHash == pack.Repeat.WorldHash && pack.Story.WorldHash.Length == 64, pack.Story.WorldHash, "identical second run", "INV-002");
        Row(text, "calendar to 1 Mar 1957", pack.Story.ReachedUntil, pack.Story.Reached.ToString(), Phase4Estimates.RobustUntil.ToString(), pack.Story.KnownIssue is { } known ? "known " + known : pack.Story.SoftLock ?? "");
        Row(text, "no unclassified soft lock (story)", pack.Story.SoftLock is null || pack.Story.KnownIssue is not null, pack.Story.SoftLock ?? "none", "none", string.Join("; ", pack.Story.Notes));
        Row(text, "player raced or skipped with reason", pack.Story.RacesEntered + pack.Story.RacesSkipped > 0 || pack.Story.Reached < new GameDate(1955, 5, 1), pack.Story.RacesEntered + "/" + pack.Story.RacesSkipped, "entered or skipped", "skip names running and transport");
        Row(text, "determinism twice", pack.Story.WorldHash == pack.Repeat.WorldHash, pack.Repeat.WorldHash, pack.Story.WorldHash, "");
        var monkeyOk = pack.Monkey.Count == 0 || pack.Monkey.All(row => row.SoftLock is null || row.KnownIssue is not null);
        Row(text, "monkey unclassified soft lock", monkeyOk, pack.Monkey.Count.ToString(CultureInfo.InvariantCulture) + " seeds", Phase4Estimates.MonkeySeeds.ToString(CultureInfo.InvariantCulture), "CI uses " + Phase4Estimates.TestMonkeySeeds);
        Row(text, "monkey reached 1 Mar 1957", pack.Monkey.Count == 0 || pack.Monkey.All(row => row.ReachedUntil || row.KnownIssue is not null), pack.Monkey.Count(row => row.ReachedUntil).ToString(CultureInfo.InvariantCulture), "all or known issue", "");
        Row(text, "refused talks have a reason key", pack.Explain.RefusedWithoutReason == 0, pack.Explain.RefusedWithoutReason.ToString(CultureInfo.InvariantCulture), "0", "");
        Row(text, "objectives STATE/WHY/FORECAST", pack.Explain.ObjectivesMissingSlice == 0, pack.Explain.ObjectivesMissingSlice.ToString(CultureInfo.InvariantCulture), "0", "");
        Row(text, "`why` leaks no hidden truth", pack.Explain.WhyHiddenLeaks == 0, pack.Explain.WhyHiddenLeaks.ToString(CultureInfo.InvariantCulture), "0", pack.Explain.WhyLines + " visible lines");
        Row(text, "dismissal path", pack.Dismissal.Dismissed, pack.Dismissal.Reached.ToString(), "unemployed", pack.Dismissal.KnownIssue ?? "");
        Row(text, "collapse path", pack.Collapse.Insolvent, pack.Collapse.Reached.ToString(), "insolvent", pack.Collapse.KnownIssue ?? "");
        var seconds = pack.Story.Wall.TotalSeconds;
        Row(text, "wall time (story)", seconds <= Phase4Estimates.SeasonWallWarnSeconds * 3, seconds.ToString("0.0", CultureInfo.InvariantCulture) + "s", "< " + (Phase4Estimates.SeasonWallWarnSeconds * 3) + "s for ~2 seasons", "ESTIMATE");
        Row(text, "save size", pack.Story.SaveBytes < Phase4Estimates.SaveSizeWarnBytes, Bytes(pack.Story.SaveBytes), "< 50 MB", "ESTIMATE / TECH §8");
        text.AppendLine();
        text.AppendLine("## Counterfactual pairs (1955)");
        text.AppendLine();
        text.AppendLine("Consequences are emergent (PP-062): there is no required number of differences. The owner judges.");
        text.AppendLine();
        foreach (var pair in pack.Counterfactuals)
        {
            text.AppendLine("### " + pair.Name);
            text.AppendLine();
            if (pair.Diverged.Count == 0)
            {
                text.AppendLine("No observable metric diverged.");
            }
            else
            {
                text.AppendLine("Diverged: " + string.Join(", ", pair.Diverged) + ".");
            }

            text.AppendLine();
        }

        text.AppendLine("## Monkey");
        text.AppendLine();
        foreach (var row in pack.Monkey)
        {
            text.AppendLine(
                "- seed " + row.Seed.ToString(CultureInfo.InvariantCulture)
                + " team `" + row.TeamId + "` reached " + row.Reached
                + (row.ReachedUntil ? " (target)" : "")
                + (row.KnownIssue is { } issue ? " known " + issue : row.SoftLock is { } lockReason ? " stop " + lockReason : "")
                + " hash `" + row.WorldHash[..8] + "…`");
        }

        if (pack.Monkey.Count == 0)
        {
            text.AppendLine("Monkey not run in this pack.");
        }

        text.AppendLine();
        text.AppendLine("## ESTIMATES");
        text.AppendLine();
        foreach (var line in pack.Estimates)
        {
            text.AppendLine("- " + line);
        }

        text.AppendLine();
        text.AppendLine("## Known issues (not fixed here)");
        text.AppendLine();
        text.AppendLine("- " + Phase4KnownIssues.AiInboxHoldsClock + ": AI inbox can hold the shared clock (#251).");
        text.AppendLine("- " + Phase4KnownIssues.JulyRenewalFlood + ": 1 July renewal flood and the five-talk cap (#253). The bot releases when a renew is refused.");
        text.AppendLine("- " + Phase4KnownIssues.SponsorsDryUp + ": sponsor catalog / notices (#254).");
        text.AppendLine();
        text.AppendLine("## Transcript");
        text.AppendLine();
        text.AppendLine("Team `" + pack.Story.TeamId + "`, seed " + pack.Story.Seed.ToString(CultureInfo.InvariantCulture) + ", hash `" + pack.Story.WorldHash + "`.");
        text.AppendLine("Second run hash `" + pack.Repeat.WorldHash + "`.");
        foreach (var note in pack.Story.Notes)
        {
            text.AppendLine("- " + note);
        }

        return text.ToString();
    }

    private static void Row(StringBuilder text, string check, bool pass, string number, string threshold, string notes)
    {
        var result = pass ? "pass" : "fail";
        if (!pass && notes.Contains("known #", StringComparison.Ordinal))
        {
            result = "warn";
        }

        text.AppendLine("| " + check + " | " + result + " | " + Escape(number) + " | " + Escape(threshold) + " | " + Escape(notes) + " |");
    }

    private static string Escape(string value) => value.Replace("|", "/", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    private static string Bytes(long value)
    {
        if (value < 1024)
        {
            return value.ToString(CultureInfo.InvariantCulture) + " B";
        }

        return (value / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
    }
}
