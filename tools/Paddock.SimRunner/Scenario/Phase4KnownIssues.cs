using Paddock.Domain.Time;

namespace Paddock.SimRunner.Scenario;

/// <summary>
/// Temporary hang labels the gate may record instead of failing. #252, #253 and #254 are merged, so the list is empty and
/// any hang fails the gate. Add a row only for a known, filed issue, and remove it when that issue merges.
/// </summary>
public static class Phase4KnownIssues
{
    public const string ResumeAfterSeasonChange = "#252";

    public const string JulyRenewalFlood = "#253";

    public const string SponsorsDryUp = "#254";

    /// <summary>
    /// The only list the gate consults. Empty it (or drop a row) when that issue is gone.
    /// </summary>
    public static readonly IReadOnlyList<(string Id, string Why)> TemporaryHangClassifications = [];

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
