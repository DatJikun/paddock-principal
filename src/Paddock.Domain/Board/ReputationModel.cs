using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Board;

/// <summary>
/// Pure rules of reputation and the board's confidence. No state, no RNG, only the ESTIMATES of <see cref="BoardEstimates"/>.
/// </summary>
public static class ReputationModel
{
    /// <summary>The last day a new principal cannot be dismissed: the end of the first full season plus extra days for reputation.</summary>
    public static GameDate ProtectedUntil(GameDate appointed, int reputationTenths)
    {
        var extra = BoardEstimates.Clamp(reputationTenths) / 10 * BoardEstimates.ProtectionDaysPerReputationPoint;
        return BoardEstimates.FirstFullSeasonEnd(appointed).AddDays(extra);
    }

    /// <summary>
    /// How far a position beats the expectation, from -1 to 1, where 0 is exactly as expected.
    /// <paramref name="fieldSize"/> counts the teams. A field of one has no margin.
    /// </summary>
    public static double Margin(int expected, int actual, int fieldSize)
    {
        if (fieldSize < 2)
        {
            return 0.0;
        }

        return Math.Clamp((expected - actual) / (double)(fieldSize - 1), -1.0, 1.0);
    }

    /// <summary>The confidence the board is heading for given the margin: 55 points at expectation, up to 100 and down to 10.</summary>
    public static int TargetConfidenceTenths(double margin) =>
        BoardEstimates.Clamp((int)Math.Round(
            BoardEstimates.TargetAtExpectationTenths + (BoardEstimates.TargetSwingTenths * Math.Clamp(margin, -1.0, 1.0)),
            MidpointRounding.AwayFromZero));

    /// <summary>One review: confidence closes part of the gap to the target, then cash takes its toll.</summary>
    public static int ReviewConfidence(int currentTenths, int targetTenths, int cashPenaltyTenths) =>
        BoardEstimates.Clamp(
            currentTenths
            + (int)Math.Round((targetTenths - currentTenths) * BoardEstimates.ReviewSmoothing, MidpointRounding.AwayFromZero)
            - cashPenaltyTenths);

    /// <summary>Confidence lost to money at a review. Unknown cash costs nothing.</summary>
    public static int CashPenaltyTenths(long? cash, long? previousCash)
    {
        if (cash is not long now)
        {
            return 0;
        }

        if (now < 0)
        {
            return BoardEstimates.NegativeCashPenaltyTenths;
        }

        if (previousCash is long before && before > 0 && now < before * (1.0 - BoardEstimates.FallingCashShare))
        {
            return BoardEstimates.FallingCashPenaltyTenths;
        }

        return 0;
    }

    /// <summary>Patience from 0 to 100 of a board, read from the profile of the organization: older teams wait longer.</summary>
    public static int PatienceFor(GameDate founded, GameDate today) =>
        Math.Clamp(
            BoardEstimates.PatienceBase + Math.Clamp(today.Year - founded.Year, 0, BoardEstimates.PatienceMaxAge),
            0,
            100);

    /// <summary>Confidence below which a review counts against the principal. A patient board has a lower bar.</summary>
    public static int DismissThresholdTenths(int patience) =>
        BoardEstimates.Clamp(
            BoardEstimates.DismissBelowAtPatience50Tenths
            - ((Math.Clamp(patience, 0, 100) - 50) * BoardEstimates.DismissThresholdTenthsPerPatiencePoint));

    /// <summary>Consecutive reviews below the threshold that dismiss: 4 for the least patient board, 8 for the most.</summary>
    public static int ReviewsToDismiss(int patience) =>
        BoardEstimates.ReviewsToDismissBase + (Math.Clamp(patience, 0, 100) / 25);

    /// <summary>The expected finishing position of a team from public facts: the last position and the budget rank, blended.</summary>
    public static int ExpectedPosition(int? lastPosition, int budgetRank, int fieldSize)
    {
        var size = Math.Max(1, fieldSize);
        var rank = Math.Clamp(budgetRank, 1, size);
        if (lastPosition is not int last)
        {
            return rank;
        }

        var blended = (BoardEstimates.LastPositionWeight * last) + ((1.0 - BoardEstimates.LastPositionWeight) * rank);
        return Math.Clamp((int)Math.Round(blended, MidpointRounding.AwayFromZero), 1, size);
    }

    /// <summary>
    /// The reputation change at a season end, in tenths: the margin over the expectation, the title, mismanaged money and the
    /// optional people-development fact (0 or more, capped).
    /// </summary>
    public static int SeasonReputationTenths(int expected, int finalPosition, int fieldSize, bool cashBelowZero, decimal? peopleDevelopment)
    {
        var margin = Margin(expected, finalPosition, fieldSize);
        var tenths = (int)Math.Round(margin * BoardEstimates.SeasonResultTenthsAtFullMargin, MidpointRounding.AwayFromZero);
        if (finalPosition == 1)
        {
            tenths += BoardEstimates.TitleTenths;
        }

        if (cashBelowZero)
        {
            tenths -= BoardEstimates.MismanagedMoneyTenths;
        }

        if (peopleDevelopment is decimal development && development > 0)
        {
            tenths += (int)Math.Min(BoardEstimates.PeopleDevelopmentCapTenths, Math.Round(development * 10m, MidpointRounding.AwayFromZero));
        }

        return tenths;
    }

    /// <summary>The reputation a team asks of a new principal, in tenths, from its prestige (0 to 1).</summary>
    public static int RequiredReputationTenths(double prestige) =>
        BoardEstimates.RequiredReputationFloorTenths
        + (int)Math.Round(Math.Clamp(prestige, 0.0, 1.0) * BoardEstimates.RequiredReputationSpanTenths, MidpointRounding.AwayFromZero);

    /// <summary>The shift of a team's prestige caused by its principal's reputation, from about -0.15 to +0.15.</summary>
    public static double PrestigeShift(double reputationPoints) =>
        (Math.Clamp(reputationPoints, 0.0, 100.0) - (BoardEstimates.NeutralReputationTenths / 10.0)) / 100.0 * BoardEstimates.ReputationPrestigeSwing;
}

/// <summary>
/// The reputation of the principal an organization has, in points from 0 to 100, for the one that asks: a negotiation weighs the
/// prestige of the team with who runs it (T39). A pure query: no state change, no RNG (INV-005).
/// </summary>
public interface IReputationSource
{
    double Reputation(OrganizationId organization, GameDate on);
}

/// <summary>Every principal has the middle reputation, which changes nothing. The default until a board is wired in.</summary>
public sealed class NeutralReputationSource : IReputationSource
{
    public double Reputation(OrganizationId organization, GameDate on) => BoardEstimates.NeutralReputationTenths / 10.0;
}
