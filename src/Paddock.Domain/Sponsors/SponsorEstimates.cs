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

    /// <summary>ESTIMATE: one more parallel talk for every this many points of negotiator skill, on top of the first.</summary>
    public const int SkillPerParallelTalk = 7;

    /// <summary>ESTIMATE: a negotiator at or above this skill can tell that a rival is also talking to the sponsor.</summary>
    public const int RivalInsightSkill = 12;

    /// <summary>ESTIMATE: the chance that a rival is talking to the sponsor when the talks open.</summary>
    public const double RivalPresenceChance = 0.5;

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

    /// <summary>ESTIMATE: bonus for a met objective, in thousandths of the annual amount.</summary>
    public const int BonusMilli = 150;

    /// <summary>ESTIMATE: a secondary livery slot pays this fraction of a main slot, in thousandths.</summary>
    public const int SecondarySlotMilli = 500;
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
        var dollars = typicalDollars * sponsor.BudgetLevel * (popularityMilli / 1000.0) * slot;
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
}
