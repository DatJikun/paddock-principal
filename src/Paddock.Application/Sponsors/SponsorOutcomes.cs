using System.Globalization;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Finance;
using Paddock.Domain.Sponsors;
using Paddock.Simulation.Objectives;
using Paddock.Simulation.Time;

namespace Paddock.Application.Sponsors;

/// <summary>
/// Applies the outcome of a sponsor's objective, which the objective day handler emitted (T36). Like
/// <see cref="ObjectiveOutcomes.Apply"/> the host calls this after the day with the day's events. Not a command and not a day
/// handler: it only turns an event that already happened into its effect, and it draws no random number.
/// <para>
/// Met: the deal pays its bonus once (ledger category <c>sponsor</c>) and the sponsor's trust rises. Failed: the sponsor leaves,
/// so the deal ends on the day of the outcome (its objective's deadline) and trust falls. Both are posted to the inbox. A deal
/// whose outcome is already applied is left alone, so applying one day's events twice changes nothing.
/// </para>
/// </summary>
public static class SponsorOutcomes
{
    public static int Apply(
        SponsorBook book,
        SponsorEnvironment environment,
        IEnumerable<DomainEvent> events,
        InboxBook? inbox = null,
        ManagerRegistry? managers = null)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(events);
        var notices = new SponsorNotices(environment, inbox, managers);
        var sponsors = book.Section;
        var finance = book.Finance;
        var financeChanged = false;
        var applied = 0;
        foreach (var domainEvent in events)
        {
            if (domainEvent.TypeId is not (ObjectiveEventTypes.Met or ObjectiveEventTypes.Failed)
                || domainEvent.Payload is not ObjectiveOutcomePayload outcome
                || sponsors.DealByObjective(outcome.ObjectiveId) is not { } deal
                || deal.Outcome != DealObjectiveOutcome.None
                || !deal.IsActive)
            {
                continue;
            }

            var today = domainEvent.Date;
            var trust = sponsors.TrustOf(deal.SponsorId, deal.Organization);
            var name = notices.NameOf(deal.SponsorId);
            if (outcome.Met)
            {
                var reward = RewardOf(book, deal);
                var bonus = deal.AnnualCents * reward.BonusMilli / 1000;
                if (bonus > 0 && finance.HasBook(deal.Organization))
                {
                    finance = finance.Post(deal.Organization, today, LedgerCategories.Sponsor, deal.SponsorId, bonus, SponsorReason.Bonus);
                    financeChanged = true;
                }

                sponsors = sponsors.Replace(deal with { Outcome = DealObjectiveOutcome.Met, BonusCents = deal.BonusCents + bonus });
                sponsors = sponsors.WithTrust(deal.SponsorId, deal.Organization, trust + reward.Trust);
                notices.Post(deal.Organization, SponsorKeys.InboxMetSubject, today, ("sponsor", name), ("bonus", SponsorNotices.Dollars(bonus)));
            }
            else
            {
                sponsors = sponsors.Replace(deal with { Outcome = DealObjectiveOutcome.Failed, Status = DealStatus.Ended, EndedOn = today });
                sponsors = sponsors.WithTrust(deal.SponsorId, deal.Organization, trust - SponsorEstimates.TrustOnFailed);
                notices.Post(deal.Organization, SponsorKeys.InboxFailedSubject, today, ("sponsor", name));
            }

            applied++;
        }

        if (applied > 0)
        {
            book.Write(sponsors, null, financeChanged ? finance : null);
        }

        return applied;
    }

    /// <summary>The bonus and trust fixed when the deal was signed. A deal with no recorded scale pays the unscaled estimates.</summary>
    private static (int BonusMilli, int Trust) RewardOf(SponsorBook book, SponsorDeal deal)
    {
        var arguments = deal.ObjectiveId is { } id ? book.Objectives.Find(id)?.EffectOnMet.Arguments : null;
        var bonus = SponsorEstimates.BonusMilli;
        var trust = SponsorEstimates.TrustOnMet;
        if (arguments is not null && arguments.TryGetValue("bonusMilli", out var bonusText)
            && int.TryParse(bonusText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedBonus))
        {
            bonus = parsedBonus;
        }

        if (arguments is not null && arguments.TryGetValue("trust", out var trustText)
            && int.TryParse(trustText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedTrust))
        {
            trust = parsedTrust;
        }

        return (bonus, trust);
    }
}
