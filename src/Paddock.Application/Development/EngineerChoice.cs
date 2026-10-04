using System.Globalization;
using Paddock.Domain.Cars;
using Paddock.Domain.Development;
using Paddock.Domain.Random;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Development;

/// <summary>One proposal on the table: an engineer, a kind of project, an area, and why it scores what it scores.</summary>
public sealed record Proposal(
    string Id,
    Engineer Engineer,
    DevKind Kind,
    DevArea? Area,
    double Utility,
    IReadOnlyList<TraceFactor> Factors,
    int Skill);

/// <summary>The result of one choice: the winner, everything considered, and whether the <c>AiDecisions</c> stream broke a tie.</summary>
public sealed record EngineerDecision(Proposal Chosen, IReadOnlyList<Proposal> Considered, bool TieBroken);

/// <summary>
/// How the engineers pick the next project (PP-043, path A). Every engineer in post proposes work in their own areas; each
/// proposal is scored by a utility from the plan's funding deficit, the headroom of the area, the principal's priority, the
/// engineer's skill for it, their experience, and how settled they are in the team. The best score wins. Pure: it reads, it
/// scores, and only a tie draws, from a child of <c>AiDecisions</c> keyed by team, day and slot (INV-004, INV-005).
/// Weights are ESTIMATES. Path B (the player picks) would replace only this class.
/// </summary>
public static class EngineerChoice
{
    public static EngineerDecision? Choose(
        OrganizationId organization,
        IReadOnlyList<Engineer> engineers,
        DevelopmentPlan plan,
        IReadOnlyList<TeamCar> cars,
        DevelopmentAccount account,
        IReadOnlyList<(DevKind Kind, DevArea? Area)> running,
        GameDate today,
        ulong masterSeed,
        int slot)
    {
        ArgumentNullException.ThrowIfNull(engineers);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(cars);
        ArgumentNullException.ThrowIfNull(running);
        if (cars.Count == 0)
        {
            return null;
        }

        var reference = cars[0];
        var proposals = new List<Proposal>();
        var total = plan.TotalSpentCents;
        double Deficit(DevKind kind) => (plan.PercentOf(kind) / 100d) - (total > 0 ? (double)plan.SpentOf(kind) / total : 0d);

        double meanHeadroom = 0;
        foreach (var area in Enum.GetValues<DevArea>())
        {
            meanHeadroom += DevelopmentMath.Headroom(reference, area) / 4d;
        }

        foreach (var engineer in engineers)
        {
            if (plan.PercentOf(DevKind.Upgrade) > 0)
            {
                foreach (var (area, key) in engineer.Areas)
                {
                    var headroom = DevelopmentMath.Headroom(reference, area);
                    if (headroom < DevelopmentEstimates.MinWorthHeadroom || running.Contains((DevKind.Upgrade, (DevArea?)area)))
                    {
                        continue;
                    }

                    var skill = key == "-" ? CarEstimates.DefaultStaffAttribute : engineer.Attribute(key);
                    proposals.Add(Score(
                        engineer,
                        DevKind.Upgrade,
                        area,
                        skill,
                        Deficit(DevKind.Upgrade),
                        Math.Clamp(headroom / DevelopmentEstimates.HeadroomScale, 0d, 1d),
                        (plan.PriorityOf(area) - DevelopmentEstimates.DefaultPriority) / (double)DevelopmentEstimates.DefaultPriority,
                        0d));
                }
            }

            if (plan.PercentOf(DevKind.Research) > 0
                && account.StockMilli < DevelopmentEstimates.AccountFullMilli
                && !running.Contains((DevKind.Research, (DevArea?)null)))
            {
                var skills = engineer.Areas.Select(entry => entry.Key == "-" ? CarEstimates.DefaultStaffAttribute : engineer.Attribute(entry.Key)).ToArray();
                proposals.Add(Score(
                    engineer,
                    DevKind.Research,
                    null,
                    (int)Math.Round(skills.Average(), MidpointRounding.AwayFromZero),
                    Deficit(DevKind.Research),
                    0d,
                    0d,
                    0d));
            }

            if (plan.PercentOf(DevKind.Concept) > 0
                && meanHeadroom >= DevelopmentEstimates.MinWorthHeadroom
                && !running.Contains((DevKind.Concept, (DevArea?)null)))
            {
                proposals.Add(Score(
                    engineer,
                    DevKind.Concept,
                    null,
                    engineer.ConceptSkill,
                    Deficit(DevKind.Concept),
                    Math.Clamp(meanHeadroom / DevelopmentEstimates.HeadroomScale, 0d, 1d),
                    0d,
                    (engineer.Innovation / 20d) - 0.5d));
            }
        }

        if (proposals.Count == 0)
        {
            return null;
        }

        var ordered = proposals
            .OrderByDescending(proposal => proposal.Utility)
            .ThenBy(proposal => proposal.Id, StringComparer.Ordinal)
            .ToList();
        var top = ordered[0].Utility;
        var tied = ordered.Where(proposal => top - proposal.Utility <= DevelopmentEstimates.TieEpsilon).ToList();
        var chosen = ordered[0];
        var broken = false;
        if (tied.Count > 1)
        {
            var rng = RngStream
                .Derive(masterSeed, RngStreamName.AiDecisions, today.Year)
                .DeriveChild("dev-tie|" + organization.Value + "|" + today + "|" + slot.ToString(CultureInfo.InvariantCulture));
            chosen = tied[Math.Min(tied.Count - 1, (int)(rng.NextDouble() * tied.Count))];
            broken = true;
        }

        return new EngineerDecision(chosen, ordered, broken);
    }

    /// <summary>The trace of a choice. Built only from what the choice already computed; never draws and never writes (INV-005, INV-006).</summary>
    public static DecisionTrace Trace(OrganizationId organization, EngineerDecision decision, GameDate today, string reason)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var options = decision.Considered
            .Select(proposal => new TraceOption(proposal.Id, proposal.Utility, proposal.Factors, true))
            .ToArray();
        var truth = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["organization"] = organization.Value,
            ["tieBroken"] = decision.TieBroken ? "1" : "0",
            ["skill"] = decision.Chosen.Skill.ToString(CultureInfo.InvariantCulture),
        };
        return new DecisionTrace(
            new WeekendKey(today.Season, 0),
            decision.Chosen.Engineer.Id,
            Math.Clamp(decision.Chosen.Skill, 1, 20),
            "development.choose",
            options,
            decision.Chosen.Id,
            reason,
            null,
            decision.Chosen.Kind == DevKind.Concept,
            truth);
    }

    private static Proposal Score(
        Engineer engineer,
        DevKind kind,
        DevArea? area,
        int skill,
        double deficit,
        double headroom,
        double priority,
        double innovation)
    {
        var factors = new List<TraceFactor>
        {
            new("fundingDeficit", DevelopmentEstimates.WeightDeficit * deficit, true),
            new("headroom", DevelopmentEstimates.WeightHeadroom * headroom, true),
            new("priority", DevelopmentEstimates.WeightPriority * priority, true),
            new("skill", DevelopmentEstimates.WeightSkill * ((skill / 20d) - 0.5d), false),
            new("experience", DevelopmentEstimates.WeightExperience * engineer.Experience, false),
            new("adaptation", DevelopmentEstimates.WeightAdaptation * engineer.Adaptation, false),
            new("innovation", DevelopmentEstimates.WeightInnovation * innovation, false),
        };
        var utility = 0d;
        foreach (var factor in factors)
        {
            utility += factor.Contribution;
        }

        var id = engineer.Id + "|" + kind + "|" + (area is { } a ? a.ToString() : "-");
        return new Proposal(id, engineer, kind, area, DevelopmentEstimates.Quantize(utility), factors, skill);
    }
}
