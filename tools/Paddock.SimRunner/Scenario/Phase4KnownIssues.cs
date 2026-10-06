using Paddock.Domain.Time;

namespace Paddock.SimRunner.Scenario;

/// <summary>
/// Temporary hang labels the gate may record instead of failing. When #252/#253/#254 merge,
/// delete the matching entries in <see cref="TemporaryHangClassifications"/> so any hang fails.
/// </summary>
public static class Phase4KnownIssues
{
    public const string ResumeAfterSeasonChange = "#252";

    public const string JulyRenewalFlood = "#253";

    public const string SponsorsDryUp = "#254";

    /// <summary>
    /// The only list the gate consults. Empty it (or drop a row) when that issue is gone.
    /// </summary>
    public static readonly IReadOnlyList<(string Id, string Why)> TemporaryHangClassifications =
    [
        (ResumeAfterSeasonChange, "Resume after a season change can diverge from the live run (#252)."),
        (JulyRenewalFlood, "1 July renewal flood and the five-talk cap (#253). The bot releases when a renew is refused."),
        (SponsorsDryUp, "Sponsor catalog / notices (#254)."),
    ];

    public static bool AllowsHang(string? issue) =>
        issue is not null
        && TemporaryHangClassifications.Any(row => string.Equals(row.Id, issue, StringComparison.Ordinal));

    public static string? Classify(GameDate date, bool playerHasOpenDecision, bool renewalPromptOpen, bool sponsorItemOpen)
    {
        if (playerHasOpenDecision)
        {
            return null;
        }

        if (renewalPromptOpen || (date.Month == 7 && date.Day == 1))
        {
            return AllowsHang(JulyRenewalFlood) ? JulyRenewalFlood : null;
        }

        if (sponsorItemOpen)
        {
            return AllowsHang(SponsorsDryUp) ? SponsorsDryUp : null;
        }

        return null;
    }
}
