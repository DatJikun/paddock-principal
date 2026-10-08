using Paddock.Domain.Random;
using Paddock.Domain.Racing;

namespace Paddock.Simulation.Regulation;

/// <summary>
/// What an AI team may know about itself and the series when it weighs a rule (INV-003): its place in the public table, how tight its
/// money is and the country it races from. Never a hidden attribute of a car or a person, and never the future.
/// </summary>
/// <param name="TeamId">Organization id of the team.</param>
/// <param name="Strength">0 (last in the public table) to 1 (first). Neutral 0.5 when no table exists yet.</param>
/// <param name="CashStress">0 (comfortable) to 1 (in debt or close to it), from the team's own ledger.</param>
/// <param name="Country">ISO-3 home country of the team, or null when unknown.</param>
public readonly record struct TeamView(string TeamId, double Strength, double CashStress, string? Country);

/// <summary>
/// The one place that reads a team's political leaning (#275, owner decision 3). A team's leaning is a stable field of the team in
/// the <c>regulations</c> section for now; when it later changes over time (by era, by the car-industry situation) only this method
/// changes, which is why it takes the season.
/// </summary>
public static class TeamLeaningSeam
{
    public static PoliticalLeaning Of(SeriesRegulations series, string teamId, int season)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentException.ThrowIfNullOrWhiteSpace(teamId);
        _ = season;
        return series.TeamOf(teamId)?.Leaning
            ?? throw new InvalidOperationException("Team '" + teamId + "' has no political life in series '" + series.SeriesId + "'.");
    }
}

/// <summary>
/// How a team values moving a rule from one value to another: its own interest (its place in the table, its money, a race at home,
/// a taste of its own) plus its political leaning. The same function serves a vote and a decision to spend a proposal, for the AI
/// teams; it reads only what a <see cref="TeamView"/> holds. Every factor carries a translation key so the outcome can be explained.
/// </summary>
public static class VoteInterest
{
    public const string ReasonStatusQuo = "vote.reason.statusQuo";

    public const string ReasonTaste = "vote.reason.taste";

    public const string ReasonPosition = "vote.reason.position";

    public const string ReasonMoney = "vote.reason.money";

    public const string ReasonSafety = "vote.reason.safety";

    public const string ReasonHome = "vote.reason.home";

    public const string ReasonBanked = "vote.reason.banked";

    public static string ReasonLeaning(PoliticalLeaning leaning) => leaning switch
    {
        PoliticalLeaning.Traditionalist => "vote.reason.leaning.traditionalist",
        PoliticalLeaning.Progressive => "vote.reason.leaning.progressive",
        PoliticalLeaning.Egalitarian => "vote.reason.leaning.egalitarian",
        PoliticalLeaning.Gimmicky => "vote.reason.leaning.gimmicky",
        _ => throw new ArgumentOutOfRangeException(nameof(leaning), leaning, null),
    };

    /// <summary>
    /// The factors for a move of <paramref name="dimensionId"/> from <paramref name="from"/> to <paramref name="to"/>.
    /// <paramref name="delta"/> is the difference of the traits of the two values, <paramref name="homeEffect"/> is -1, 0 or 1
    /// (see <see cref="CalendarPolicy.HomeEffect"/>). Positive favours the move. Zero factors are left out.
    /// </summary>
    public static IReadOnlyList<UtilityFactor> Evaluate(
        TeamView team,
        PoliticalLeaning leaning,
        string dimensionId,
        string to,
        ValueTraits delta,
        double homeEffect)
    {
        var factors = new List<UtilityFactor>(8)
        {
            new(ReasonStatusQuo, RegulationEstimates.StatusQuoBias),
        };
        Add(factors, ReasonTaste, Taste(team.TeamId, dimensionId, to) * RegulationEstimates.TasteScale);
        Add(factors, ReasonPosition, (0.5 - team.Strength) * 2.0 * delta.Equal * RegulationEstimates.EqualWeight);
        Add(factors, ReasonMoney, (0.25 + team.CashStress) * delta.Cheap * RegulationEstimates.MoneyWeight);
        Add(factors, ReasonSafety, delta.Safety * RegulationEstimates.SafetyWeight);
        Add(factors, ReasonHome, homeEffect * RegulationEstimates.HomeWeight);
        Add(factors, ReasonLeaning(leaning), leaning switch
        {
            PoliticalLeaning.Traditionalist => -RegulationEstimates.TraditionalistResistance - (RegulationEstimates.TraditionalistWeight * delta.Modern),
            PoliticalLeaning.Progressive => RegulationEstimates.ProgressiveWeight * delta.Modern,
            PoliticalLeaning.Egalitarian => RegulationEstimates.EgalitarianWeight * (delta.Equal + (0.3 * delta.Cheap)),
            PoliticalLeaning.Gimmicky => RegulationEstimates.GimmickyWeight * delta.Show,
            _ => throw new ArgumentOutOfRangeException(nameof(leaning), leaning, null),
        });
        return factors;
    }

    /// <summary>The sum of the factors.</summary>
    public static double Total(IReadOnlyList<UtilityFactor> factors)
    {
        ArgumentNullException.ThrowIfNull(factors);
        double total = 0;
        foreach (var factor in factors)
        {
            total += factor.Value;
        }

        return total;
    }

    /// <summary>
    /// The reason that weighed most (largest absolute value; the status quo bias counts too). Ties go to the earlier factor, so
    /// the choice is deterministic.
    /// </summary>
    public static string DominantReason(IReadOnlyList<UtilityFactor> factors)
    {
        ArgumentNullException.ThrowIfNull(factors);
        var best = "";
        var size = double.NegativeInfinity;
        foreach (var factor in factors)
        {
            if (Math.Abs(factor.Value) > size)
            {
                size = Math.Abs(factor.Value);
                best = factor.Reason;
            }
        }

        return best;
    }

    private static void Add(List<UtilityFactor> factors, string reason, double value)
    {
        if (Math.Abs(value) > 1e-12)
        {
            factors.Add(new UtilityFactor(reason, value));
        }
    }

    /// <summary>A stable taste in [-1, 1] of one team for one value of one dimension, independent of the career seed (same as the old vote).</summary>
    private static double Taste(string teamId, string dimensionId, string value) =>
        (RngStreams.Derive(0, teamId + "|" + dimensionId + "|" + value, 0).NextDouble() * 2) - 1;
}
