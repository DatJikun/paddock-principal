using Paddock.Domain.Time;

namespace Paddock.SimRunner.Scenario;

/// <summary>
/// Parallel issues that can hold the clock. The gate records them; it does not change those systems (#113).
/// </summary>
public static class Phase4KnownIssues
{
    public const string AiInboxHoldsClock = "#251";

    public const string JulyRenewalFlood = "#253";

    public const string SponsorsDryUp = "#254";

    public static string? Classify(GameDate date, bool playerHasOpenDecision, bool otherManagerHoldsClock, bool renewalPromptOpen, bool sponsorItemOpen)
    {
        if (playerHasOpenDecision)
        {
            return null;
        }

        if (otherManagerHoldsClock)
        {
            return AiInboxHoldsClock;
        }

        if (renewalPromptOpen || (date.Month == 7 && date.Day == 1))
        {
            return JulyRenewalFlood;
        }

        if (sponsorItemOpen)
        {
            return SponsorsDryUp;
        }

        return null;
    }
}
