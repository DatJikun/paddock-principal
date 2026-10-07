namespace Paddock.Simulation.Regulation;

/// <summary>
/// Every number of regulation voting v2 (#275) that is a guess. Each one is an ESTIMATE until the owner calibrates it, and the
/// ones the owner asked to confirm are listed in the pull request. They sit here, in one place, so a calibration edits one file.
/// </summary>
public static class RegulationEstimates
{
    /// <summary>ESTIMATE: share of the last completed season's revenue a team pays for one proposal. Hurts a rich and a poor team about equally.</summary>
    public const double FeeRevenueShare = 0.06;

    /// <summary>ESTIMATE: the floor of the fee as a share of the era's typical budget, so a team with no revenue still pays.</summary>
    public const double FeeFloorShareOfTypicalBudget = 0.01;

    /// <summary>ESTIMATE: the absolute floor of the fee in cents (1000 dollars). The fee is never zero or negative.</summary>
    public const long MinimumFeeCents = 100_000;

    /// <summary>ESTIMATE: a (dimension, value) a vote rejected is not proposed again for this many seasons (the old memory, unchanged).</summary>
    public const int RejectionMemorySeasons = 5;

    /// <summary>Owner decision 6: a team that proposed in season N may not propose in the next two seasons (N + 1, N + 2) and may in N + 3.</summary>
    public const int CooldownSeasons = 2;

    /// <summary>ESTIMATE: the AI teams start with a cooldown of 0 to this many seasons, staggered from the career seed. The player starts with none.</summary>
    public const int StartingCooldownMaxSeasons = 2;

    /// <summary>ESTIMATE: the most votes a team can keep in its bank.</summary>
    public const int BankCap = 5;

    /// <summary>ESTIMATE: the most banked votes a team can spend on one ballot item.</summary>
    public const int MaxSpendPerItem = 3;

    /// <summary>Owner decision 5: the FIA brings at least this many votes per series per year.</summary>
    public const int FiaVotesMin = 4;

    /// <summary>Owner decision 5: the FIA brings at most this many votes per series per year.</summary>
    public const int FiaVotesMax = 6;

    /// <summary>ESTIMATE: days a ballot item stays open for votes.</summary>
    public const int VotingDays = 30;

    /// <summary>ESTIMATE: the smallest number of rounds a voted calendar may keep.</summary>
    public const int MinimumRounds = 5;

    /// <summary>ESTIMATE: how many seasons around the vote a layout may have been raced for the calendar to offer it.</summary>
    public const int CalendarWindowSeasons = 3;

    /// <summary>The president's weight when he decides a tie.</summary>
    public const int PresidentWeight = 1;

    /// <summary>ESTIMATE: a change that moves every trait of a rule by less than this in total makes no difference and is not offered.</summary>
    public const double MinimumEffect = 0.1;

    /// <summary>ESTIMATE: the status-quo bias every voter has against a change (same figure as the old vote).</summary>
    public const double StatusQuoBias = -0.2;

    /// <summary>ESTIMATE: half-width of the stable per-team taste for a value.</summary>
    public const double TasteScale = 0.25;

    /// <summary>ESTIMATE: a team votes or proposes for a rule that suits its place: weight of the equalising trait for a weak team.</summary>
    public const double EqualWeight = 0.8;

    /// <summary>ESTIMATE: weight of savings for a team in money trouble.</summary>
    public const double MoneyWeight = 0.7;

    /// <summary>ESTIMATE: weight of safety for every team.</summary>
    public const double SafetyWeight = 0.25;

    /// <summary>ESTIMATE: weight of a home race (a circuit in the team's own country).</summary>
    public const double HomeWeight = 0.9;

    /// <summary>ESTIMATE: weight of a team's leaning, per leaning.</summary>
    public const double TraditionalistResistance = 0.35;

    public const double TraditionalistWeight = 0.5;

    public const double ProgressiveWeight = 0.6;

    public const double EgalitarianWeight = 0.7;

    public const double GimmickyWeight = 0.7;

    /// <summary>ESTIMATE: an AI team in vote-bank mode banks its vote when the stake is below this.</summary>
    public const double LowStake = 0.25;

    /// <summary>ESTIMATE: an AI team in vote-bank mode spends banked votes when the stake is at least this.</summary>
    public const double HighStake = 0.8;

    /// <summary>ESTIMATE: the noise added to each variant's utility in one vote (half-width, same as the old vote).</summary>
    public const double VoteNoise = 0.1;

    /// <summary>ESTIMATE: what an AI team counts as the cost of spending its one proposal on a rule (the three-season wait).</summary>
    public const double CooldownCost = 0.45;

    /// <summary>ESTIMATE: converts the fee as a share of revenue into utility.</summary>
    public const double FeeBurdenScale = 5.0;

    /// <summary>ESTIMATE: converts debt left after the fee, as a share of revenue, into utility.</summary>
    public const double DebtPenaltyScale = 2.0;

    /// <summary>ESTIMATE: an AI team proposes when its net expected gain is at least this.</summary>
    public const double MinimumNetGain = 0.05;

    /// <summary>ESTIMATE: weight of the FIA's modernising drift when it picks a proposal.</summary>
    public const double FiaModernDrift = 0.15;

    /// <summary>ESTIMATE: how many of the best-scoring candidates the FIA draws its proposal from.</summary>
    public const int FiaShortlist = 4;
}
