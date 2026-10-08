using System.Globalization;
using Paddock.Domain.Finance;

namespace Paddock.Domain.Sponsors;

/// <summary>
/// Every guessed number in the sponsor system. Not one is a measured fact, and none is calibrated: each is an ESTIMATE
/// until the economy is tuned against real results. The owner's decisions (PP-050) are the slot count and the minimum
/// number of candidates per slot; they are not estimates. Nothing here is sampled except the rival signing in
/// <see cref="SponsorPricing"/>'s callers, which uses the <c>Market</c> stream only.
/// </summary>
public static class SponsorEstimates
{
    /// <summary>PP-050 (owner decision): every team has three sponsor slots.</summary>
    public const int SlotsPerTeam = 3;

    /// <summary>PP-050 (owner decision): at least this many candidates for each slot at any time. Checked by the data validator.</summary>
    public const int MinCandidatesPerSlot = 3;

    /// <summary>ESTIMATE: a deal runs this many days (about one season).</summary>
    public const int DealDays = 365;

    /// <summary>ESTIMATE: the annual amount is paid in this many instalments, one a month.</summary>
    public const int InstalmentsPerYear = 12;

    /// <summary>ESTIMATE schedule: instalments are posted on this day of the month.</summary>
    public const int InstalmentDayOfMonth = 15;

    /// <summary>ESTIMATE: a sponsor opens talks asking this fraction of its full price from the team's side, in thousandths (800 is 80 percent).</summary>
    public const int OpeningTermsMilli = 800;

    /// <summary>ESTIMATE: waiting improves the terms by this many thousandths of the full price a day, up to the cap.</summary>
    public const int WaitingGainMilliPerDay = 6;

    /// <summary>ESTIMATE: the cap on improvement for a negotiator with no skill, in thousandths of the full price.</summary>
    public const int BaseCapMilli = 1050;

    /// <summary>ESTIMATE: each point of negotiator skill (0 to 20) raises the cap by this many thousandths.</summary>
    public const int CapMilliPerSkill = 5;

    /// <summary>ESTIMATE: the negotiator skill scale tops out here, like the staff attributes it is read from.</summary>
    public const int MaxSkill = 20;

    /// <summary>ESTIMATE: the negotiating skill every team counts with while the commercial director is hidden (#265): an average director.</summary>
    public const int NeutralSkill = 10;

    /// <summary>ESTIMATE: one more parallel talk for every this many points of negotiator skill, on top of the first.</summary>
    public const int SkillPerParallelTalk = 7;

    /// <summary>ESTIMATE: a negotiator at or above this skill can tell that a rival is also talking to the sponsor.</summary>
    public const int RivalInsightSkill = 12;

    /// <summary>
    /// ESTIMATE: the chance that a rival is talking to the sponsor when the talks open. Zero for now (PP-065): teams do not fight over
    /// sponsors, so nobody takes a sponsor from the player. A shared sponsor market, with a rival, comes later.
    /// </summary>
    public const double RivalPresenceChance = 0.0;

    /// <summary>ESTIMATE: the chance a day that a rival who is talking signs the sponsor.</summary>
    public const double RivalSignChance = 0.02;

    /// <summary>ESTIMATE: a sponsor that signed with a rival is off the market for this many days.</summary>
    public const int RivalDealDays = 365;

    /// <summary>ESTIMATE: sponsor trust, 0 to 100, at the first deal.</summary>
    public const int StartTrust = 50;

    public const int MinTrust = 0;

    public const int MaxTrust = 100;

    /// <summary>ESTIMATE: trust gained when a deal's objective is met.</summary>
    public const int TrustOnMet = 10;

    /// <summary>ESTIMATE: trust lost when a deal's objective is failed.</summary>
    public const int TrustOnFailed = 20;

    /// <summary>ESTIMATE: trust gained when a deal runs to its end without a failed objective.</summary>
    public const int TrustOnCompleted = 5;

    /// <summary>ESTIMATE: a sponsor with less trust than this makes no renewal offer.</summary>
    public const int RenewalMinTrust = 40;

    /// <summary>ESTIMATE: a renewal is offered this many days before the deal ends.</summary>
    public const int RenewalLeadDays = 60;

    /// <summary>ESTIMATE: renewal price in thousandths of the full price is this plus <see cref="RenewalPerTrustMilli"/> times trust.</summary>
    public const int RenewalBaseMilli = 850;

    public const int RenewalPerTrustMilli = 3;

    /// <summary>ESTIMATE: bonus for a met objective, in thousandths of the annual amount, before the target is scaled to the team.</summary>
    public const int BonusMilli = 150;

    /// <summary>
    /// ESTIMATE (PP-058): an "at least" target (podiums, points) for the team expected to finish first, in thousandths of the authored base.
    /// 2000 is double. A harder target pays more than an easier one.
    /// </summary>
    public const int ObjectiveAtLeastTopMilli = 2000;

    /// <summary>ESTIMATE (PP-058): the same target for the team expected to finish last, in thousandths of the authored base. 250 is a quarter.</summary>
    public const int ObjectiveAtLeastBottomMilli = 250;

    /// <summary>
    /// ESTIMATE (PP-058): a championship-position target for the team expected to finish first, in thousandths of the authored base.
    /// 500 is half, which is harder because a smaller position is better.
    /// </summary>
    public const int ObjectivePositionTopMilli = 500;

    /// <summary>ESTIMATE (PP-058): the same target for the team expected to finish last, in thousandths of the authored base. A larger position is easier.</summary>
    public const int ObjectivePositionBottomMilli = 2000;

    /// <summary>ESTIMATE: the scaled bonus never exceeds this many thousandths of the annual amount.</summary>
    public const int MaxObjectiveBonusMilli = 1000;

    /// <summary>ESTIMATE: trust gained for a scaled objective never exceeds this.</summary>
    public const int MaxScaledTrustOnMet = 40;

    /// <summary>
    /// ESTIMATE (#254, PP-065): how many new backers each team gets every season for each family (technical or livery). Every team has its
    /// own pool, different each season, and no sponsor is shared between teams. A handful is enough to choose from.
    /// </summary>
    public const int LocalBackersPerSeason = 4;

    /// <summary>ESTIMATE (#254): a team backer can start a new deal only in its own season. It can renew, so a deal can run on.</summary>
    public const int LocalBackerSeasons = 1;

    /// <summary>ESTIMATE: a secondary livery slot pays this fraction of a main slot, in thousandths.</summary>
    public const int SecondarySlotMilli = 500;

    /// <summary>
    /// ESTIMATE (#268): every full price is multiplied by this many thousandths. The owner found the amounts small: a named sponsor
    /// paid about 7 percent of a typical team budget a year and a team backer 3 to 6 percent. 1700 is 1.7 times, so a named sponsor
    /// pays about 12 percent and a backer 4 to 10 percent. A guess, to be calibrated with the whole economy.
    /// </summary>
    public const int PayScaleMilli = 1700;

    /// <summary>ESTIMATE (#268, owner decision): the shortest deal is one year.</summary>
    public const int MinYears = 1;

    /// <summary>ESTIMATE (#268, owner decision): the longest deal is three years.</summary>
    public const int MaxYears = 3;

    /// <summary>
    /// ESTIMATE (#268, owner decision): how many points a deal completed with the same sponsor adds to how open it is to a long partnership.
    /// A longer deal pays the same a year as a short one ("then you would always sign for one year"); a long partnership is worth more
    /// through the raises below.
    /// </summary>
    public const int OpennessPerCompletedDeal = 15;

    /// <summary>ESTIMATE (#268): a sponsor this big (<see cref="BigSponsorBudgetLevel"/>) plans further ahead, which adds this many points of openness.</summary>
    public const int OpennessBigSponsorBonus = 10;

    /// <summary>ESTIMATE (#268): from this many points a sponsor keeps an eye on results and raises the amount for a good year. The starting trust is 50, so a new small backer is below it.</summary>
    public const int OpennessOpenScore = 55;

    /// <summary>ESTIMATE (#268): from this many points a sponsor wants a long partnership.</summary>
    public const int OpennessEagerScore = 75;

    /// <summary>ESTIMATE (#268): at an anniversary after a year in which the condition was met, an open sponsor raises the annual amount by this many thousandths.</summary>
    public const int AnniversaryRaiseOpenMilli = 30;

    /// <summary>ESTIMATE (#268): the same raise for a sponsor that wants a long partnership.</summary>
    public const int AnniversaryRaiseEagerMilli = 60;

    /// <summary>ESTIMATE (#268): the most the player can ask above the quote, in thousandths. Above what the sponsor will pay it holds at its own limit.</summary>
    public const int AskMaxMilli = 100;

    /// <summary>ESTIMATE (#268): an open sponsor pays up to this many thousandths above the quote when asked.</summary>
    public const int AskOpenLimitMilli = 30;

    /// <summary>ESTIMATE (#268): a sponsor that wants a long partnership pays up to this many thousandths above the quote when asked.</summary>
    public const int AskEagerLimitMilli = 60;

    /// <summary>ESTIMATE (#268): the amounts above the quote, in thousandths, that the screen offers to ask for.</summary>
    public static readonly IReadOnlyList<int> AskSteps = [0, 30, 60, 100];

    /// <summary>ESTIMATE (#268): an easier condition (a lower target) pays this many thousandths of the standard price.</summary>
    public const int LighterPayMilli = 930;

    /// <summary>ESTIMATE (#268): a harder condition (a higher target) pays this many thousandths of the standard price.</summary>
    public const int HarderPayMilli = 1100;

    /// <summary>ESTIMATE (#268): an easier condition asks this many thousandths of the standard target (a smaller number of podiums or points, a worse position).</summary>
    public const int LighterTargetMilli = 750;

    /// <summary>ESTIMATE (#268): a harder condition asks this many thousandths of the standard target.</summary>
    public const int HarderTargetMilli = 1300;

    /// <summary>ESTIMATE (#268): the most times the player can change the terms of one renewal offer before the sponsor says it is final.</summary>
    public const int MaxCounterRounds = 3;

    /// <summary>ESTIMATE (#268): a renewal offer is at least this many thousandths of the amount of the deal that ends, for a sponsor with the starting trust.</summary>
    public const int RenewalRaiseBaseMilli = 1030;

    /// <summary>ESTIMATE (#268): every point of trust above <see cref="StartTrust"/> adds this many thousandths to the raise of a renewal offer.</summary>
    public const int RenewalRaisePerTrustMilli = 2;

    /// <summary>ESTIMATE (#268): a sponsor whose budget level (share of the typical team budget) is at least this wants its nationality in a race seat; a smaller one accepts a reserve.</summary>
    public const double BigSponsorBudgetLevel = 0.10;

    /// <summary>ESTIMATE (#268): a met nationality wish pays this many thousandths of the annual amount a year on top, in the monthly instalments. A bonus only: a missed wish costs nothing else.</summary>
    public const int WishBonusMilli = 100;

    /// <summary>ESTIMATE (#268): a fuel, oil, tyre or motor sponsor supplies goods worth this many thousandths of the annual amount, paid with each instalment.</summary>
    public const int InKindMilli = 50;

    /// <summary>ESTIMATE (#268): a finance, consumer or electronics sponsor adds this many thousandths of the annual amount once, with the first instalment.</summary>
    public const int SigningBonusMilli = 80;
}

/// <summary>Prices and terms. Pure functions, no randomness.</summary>
public static class SponsorPricing
{
    /// <summary>
    /// The full annual price in cents: the era's typical team budget times the sponsor's budget level times the popularity index,
    /// times the slot factor. Rounded once to whole cents (ESTIMATE model, USD only).
    /// </summary>
    public static long FullAnnualCents(SponsorDefinition sponsor, SlotKind kind, long typicalDollars, int popularityMilli)
    {
        ArgumentNullException.ThrowIfNull(sponsor);
        ArgumentOutOfRangeException.ThrowIfNegative(typicalDollars);
        var slot = kind == SlotKind.Secondary ? SponsorEstimates.SecondarySlotMilli / 1000.0 : 1.0;
        var dollars = typicalDollars * sponsor.BudgetLevel * (popularityMilli / 1000.0) * slot * (SponsorEstimates.PayScaleMilli / 1000.0);
        return Money.RoundDollars(dollars).Cents;
    }

    /// <summary>The cap on the terms in thousandths of the full price, for a negotiator of the given skill (0 to 20).</summary>
    public static int CapMilli(int skill) =>
        SponsorEstimates.BaseCapMilli + (Math.Clamp(skill, 0, SponsorEstimates.MaxSkill) * SponsorEstimates.CapMilliPerSkill);

    /// <summary>Terms in thousandths of the full price after <paramref name="daysOpen"/> days of waiting, never above <paramref name="capMilli"/>.</summary>
    public static int TermsMilli(int daysOpen, int capMilli) =>
        Math.Min(capMilli, SponsorEstimates.OpeningTermsMilli + (Math.Max(0, daysOpen) * SponsorEstimates.WaitingGainMilliPerDay));

    /// <summary>How many talks a negotiator of this skill can run at once, between one and the slot count.</summary>
    public static int ParallelTalks(int skill) =>
        Math.Clamp(1 + (Math.Clamp(skill, 0, SponsorEstimates.MaxSkill) / SponsorEstimates.SkillPerParallelTalk), 1, SponsorEstimates.SlotsPerTeam);

    /// <summary>The cents of instalment number <paramref name="number"/> (1 to 12). The twelve add up to the annual amount exactly.</summary>
    public static long InstalmentCents(long annualCents, int number)
    {
        if (number < 1 || number > SponsorEstimates.InstalmentsPerYear)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, "Instalment number must be 1 to 12.");
        }

        return (annualCents * number / SponsorEstimates.InstalmentsPerYear)
            - (annualCents * (number - 1) / SponsorEstimates.InstalmentsPerYear);
    }

    public static int ClampTrust(int trust) => Math.Clamp(trust, SponsorEstimates.MinTrust, SponsorEstimates.MaxTrust);

    /// <summary>
    /// The annual amount of a renewal offer in cents (#268). A satisfied sponsor offers more than it paid: at least the amount of the deal that ends,
    /// raised by <see cref="SponsorEstimates.RenewalRaiseBaseMilli"/> and a little more for every point of trust above the starting one; and never
    /// less than the old formula from the trust and today's full price. Both are scaled from the terms of the old deal to the terms asked now.
    /// </summary>
    public static long RenewalCents(long fullCents, int trust, long previousAnnualCents, SponsorTerms previous, SponsorTerms terms)
    {
        var fromPrice = fullCents * (SponsorEstimates.RenewalBaseMilli + (SponsorEstimates.RenewalPerTrustMilli * trust)) / 1000;
        var raise = SponsorEstimates.RenewalRaiseBaseMilli + (Math.Max(0, trust - SponsorEstimates.StartTrust) * SponsorEstimates.RenewalRaisePerTrustMilli);
        var fromDeal = previousAnnualCents * raise / 1000 * terms.PayMilli / Math.Max(1, previous.PayMilli);
        return Math.Max(terms.AnnualCents(fromPrice), fromDeal);
    }
}

/// <summary>
/// Scales an authored sponsor objective to a team's expected championship position (1 is best). The position is the same
/// public blend the board uses. Nationality objectives are left as authored. The reward grows with difficulty and never
/// the other way. Pure: no state, no random number.
/// </summary>
public static class SponsorObjectiveScale
{
    public static SponsorObjectiveSpec Scale(
        SponsorObjectiveSpec spec,
        int expectedPosition,
        int fieldSize,
        SponsorAmbition ambition = SponsorAmbition.Standard)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (spec.Kind == SponsorObjectiveSpec.DriverNationalityInLineup)
        {
            return spec;
        }

        var size = Math.Max(1, fieldSize);
        var expected = Math.Clamp(expectedPosition, 1, size);
        var span = Math.Max(1, size - 1);
        var milli = Milli(spec.Kind, expected, span);

        // The player's choice moves the target on top of the team's strength: a harder condition asks for more podiums and points and a better
        // position, an easier one for less. A position target moves the other way round, because a smaller number is better.
        var target = SponsorAmbitions.TargetMilli(ambition);
        milli = spec.Kind == SponsorObjectiveSpec.ChampionshipPositionAtMost
            ? (int)Math.Round(milli * 1000.0 / target, MidpointRounding.AwayFromZero)
            : (int)Math.Round(milli * (target / 1000.0), MidpointRounding.AwayFromZero);
        if (spec.Kind == SponsorObjectiveSpec.ChampionshipPositionAtMost)
        {
            var authored = int.Parse(spec.Value, CultureInfo.InvariantCulture);
            var scaled = (int)Math.Round(authored * (milli / 1000.0), MidpointRounding.AwayFromZero);
            return spec with { Value = Math.Clamp(scaled, 1, size).ToString(CultureInfo.InvariantCulture) };
        }

        if (spec.Kind == SponsorObjectiveSpec.PodiumsAtLeast)
        {
            var authored = int.Parse(spec.Value, CultureInfo.InvariantCulture);
            var scaled = (int)Math.Round(authored * (milli / 1000.0), MidpointRounding.AwayFromZero);
            return spec with { Value = Math.Max(1, scaled).ToString(CultureInfo.InvariantCulture) };
        }

        if (spec.Kind == SponsorObjectiveSpec.PointsAtLeast)
        {
            var authored = decimal.Parse(spec.Value, CultureInfo.InvariantCulture);
            var scaled = Math.Round(authored * milli / 1000m, 0, MidpointRounding.AwayFromZero);
            if (scaled < 1)
            {
                scaled = 1;
            }

            return spec with { Value = scaled.ToString(CultureInfo.InvariantCulture) };
        }

        return spec;
    }

    /// <summary>
    /// The authored target moved by the player's choice alone, for a team whose strength is not known: a harder condition asks for more podiums
    /// or points (or a better position), an easier one for less. The standard ambition returns the spec as it is.
    /// </summary>
    public static SponsorObjectiveSpec Ambitious(SponsorObjectiveSpec spec, SponsorAmbition ambition)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (ambition == SponsorAmbition.Standard || spec.Kind == SponsorObjectiveSpec.DriverNationalityInLineup)
        {
            return spec;
        }

        var target = SponsorAmbitions.TargetMilli(ambition);
        var authored = decimal.Parse(spec.Value, CultureInfo.InvariantCulture);
        var scaled = spec.Kind == SponsorObjectiveSpec.ChampionshipPositionAtMost
            ? authored * 1000m / target
            : authored * target / 1000m;
        var rounded = Math.Max(1m, Math.Round(scaled, 0, MidpointRounding.AwayFromZero));
        return spec with { Value = rounded.ToString(CultureInfo.InvariantCulture) };
    }

    /// <summary>Bonus thousandths and trust gained for meeting <paramref name="scaled"/>, compared with the authored base.</summary>
    public static (int BonusMilli, int Trust) Reward(SponsorObjectiveSpec authored, SponsorObjectiveSpec scaled)
    {
        ArgumentNullException.ThrowIfNull(authored);
        ArgumentNullException.ThrowIfNull(scaled);
        if (authored.Kind == SponsorObjectiveSpec.DriverNationalityInLineup)
        {
            return (SponsorEstimates.BonusMilli, SponsorEstimates.TrustOnMet);
        }

        var baseValue = decimal.Parse(authored.Value, CultureInfo.InvariantCulture);
        var scaledValue = decimal.Parse(scaled.Value, CultureInfo.InvariantCulture);
        if (baseValue <= 0 || scaledValue <= 0)
        {
            return (SponsorEstimates.BonusMilli, SponsorEstimates.TrustOnMet);
        }

        var harder = authored.Kind == SponsorObjectiveSpec.ChampionshipPositionAtMost
            ? baseValue / scaledValue
            : scaledValue / baseValue;
        var bonus = (int)Math.Round(SponsorEstimates.BonusMilli * harder, MidpointRounding.AwayFromZero);
        var trust = (int)Math.Round(SponsorEstimates.TrustOnMet * harder, MidpointRounding.AwayFromZero);
        return (
            Math.Clamp(bonus, 1, SponsorEstimates.MaxObjectiveBonusMilli),
            Math.Clamp(trust, 1, SponsorEstimates.MaxScaledTrustOnMet));
    }

    private static int Milli(string kind, int expected, int span)
    {
        if (kind == SponsorObjectiveSpec.ChampionshipPositionAtMost)
        {
            return SponsorEstimates.ObjectivePositionTopMilli
                + ((SponsorEstimates.ObjectivePositionBottomMilli - SponsorEstimates.ObjectivePositionTopMilli) * (expected - 1) / span);
        }

        var fromFront = span - (expected - 1);
        return SponsorEstimates.ObjectiveAtLeastBottomMilli
            + ((SponsorEstimates.ObjectiveAtLeastTopMilli - SponsorEstimates.ObjectiveAtLeastBottomMilli) * fromFront / span);
    }
}
