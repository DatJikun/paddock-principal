namespace Paddock.Domain.Sponsors;

/// <summary>How open a sponsor is to a long partnership (#268): what the team can tell from its own history with the sponsor and from what the sponsor is.</summary>
public enum PartnershipBand
{
    /// <summary>Pays what is agreed and nothing more.</summary>
    Reserved = 0,

    /// <summary>Keeps an eye on results and raises the amount for a year that went well.</summary>
    Open = 1,

    /// <summary>Wants a long partnership and raises the amount more for good results.</summary>
    Eager = 2,
}

/// <summary>What a sponsor answers to an amount the player asks above the quote: it pays it, or it holds at the most it will pay.</summary>
public enum SponsorAskOutcome
{
    Accepted = 0,
    Countered = 1,
}

/// <summary>The answer to an ask: what happened and how many thousandths above the quote the deal will pay.</summary>
public readonly record struct SponsorAskAnswer(SponsorAskOutcome Outcome, int AppliedMilli);

/// <summary>
/// The sponsor's side of a long partnership (#268, owner decision): a longer deal pays the same a year as a short one, and a satisfied sponsor
/// raises the amount at each anniversary when the team met the condition. Which sponsors do is a signal the team can read before it signs, from
/// what it knows (the trust the sponsor has in it, the deals they already completed together, how big the sponsor is), never from hidden truth
/// (INV-003). Pure: no state, no random number. Every figure is an ESTIMATE in <see cref="SponsorEstimates"/>.
/// </summary>
public static class SponsorPartnership
{
    /// <summary>The points that say how open a sponsor is: its trust in the team, plus the deals completed together, plus a bonus for a big sponsor.</summary>
    public static int Score(int trust, int completedDeals, bool bigSponsor) =>
        trust
        + (Math.Max(0, completedDeals) * SponsorEstimates.OpennessPerCompletedDeal)
        + (bigSponsor ? SponsorEstimates.OpennessBigSponsorBonus : 0);

    public static PartnershipBand BandOf(int score) =>
        score >= SponsorEstimates.OpennessEagerScore ? PartnershipBand.Eager
        : score >= SponsorEstimates.OpennessOpenScore ? PartnershipBand.Open
        : PartnershipBand.Reserved;

    public static string KeyOf(PartnershipBand band) => band switch
    {
        PartnershipBand.Reserved => "reserved",
        PartnershipBand.Open => "open",
        PartnershipBand.Eager => "eager",
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, "Unknown partnership band."),
    };

    /// <summary>Thousandths of the annual amount a satisfied sponsor of this band adds at an anniversary after a year in which the condition was met.</summary>
    public static int AnniversaryRaiseMilli(PartnershipBand band) => band switch
    {
        PartnershipBand.Reserved => 0,
        PartnershipBand.Open => SponsorEstimates.AnniversaryRaiseOpenMilli,
        PartnershipBand.Eager => SponsorEstimates.AnniversaryRaiseEagerMilli,
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, "Unknown partnership band."),
    };

    /// <summary>The most thousandths above the quote a sponsor of this band will pay when asked.</summary>
    public static int AskLimitMilli(PartnershipBand band) => band switch
    {
        PartnershipBand.Reserved => 0,
        PartnershipBand.Open => SponsorEstimates.AskOpenLimitMilli,
        PartnershipBand.Eager => SponsorEstimates.AskEagerLimitMilli,
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, "Unknown partnership band."),
    };

    /// <summary>True when the ask is a number the sponsor will talk about at all.</summary>
    public static bool IsAskable(int askMilli) => askMilli >= 0 && askMilli <= SponsorEstimates.AskMaxMilli;

    /// <summary>
    /// The answer to an ask of <paramref name="askMilli"/> thousandths above the quote: paid in full when it is within what the sponsor will pay,
    /// otherwise the sponsor holds at the most it will pay (a counter, which can be nothing above the quote for a reserved sponsor).
    /// </summary>
    public static SponsorAskAnswer Answer(int askMilli, PartnershipBand band)
    {
        if (!IsAskable(askMilli))
        {
            throw new ArgumentOutOfRangeException(nameof(askMilli), askMilli, "An ask is 0 to " + SponsorEstimates.AskMaxMilli + " thousandths.");
        }

        var limit = AskLimitMilli(band);
        return askMilli <= limit
            ? new SponsorAskAnswer(SponsorAskOutcome.Accepted, askMilli)
            : new SponsorAskAnswer(SponsorAskOutcome.Countered, limit);
    }

    /// <summary>The cents a year pays when <paramref name="quoteCents"/> is raised by <paramref name="milli"/> thousandths.</summary>
    public static long Raised(long quoteCents, int milli) => quoteCents + (quoteCents * milli / 1000);
}
