namespace Paddock.Simulation.Ai;

/// <summary>Why a principal looks at its team out of turn (the event triggers of issue #109, point 4).</summary>
public enum ReviewTrigger
{
    /// <summary>The scheduled date came (a quiet team is looked at every <see cref="AiEstimates.ScheduledReviewDays"/> days).</summary>
    Scheduled = 0,

    /// <summary>The first day of a season.</summary>
    NewSeason = 1,

    /// <summary>A seat or a key role is empty, or became empty.</summary>
    Vacancy = 2,

    /// <summary>A contract is about to end or has ended.</summary>
    Expiry = 3,

    /// <summary>A race result is in.</summary>
    RaceResult = 4,

    /// <summary>The regulations changed or were announced.</summary>
    RegulationChange = 5,

    /// <summary>A talk the team has is waiting for its answer.</summary>
    Talk = 6,

    /// <summary>The previous review filed a command and wants to read the result.</summary>
    FollowUp = 7,
}

/// <summary>
/// When a principal looks at its team next. Reviews are scheduled, not polled: a review computes the earliest day anything will need
/// the principal (a contract window, a talk answer, the next season) and the host skips the team until then, so a quiet day costs a
/// date comparison. The host can still pull a review forward by raising a trigger (a race result, a regulation change).
/// </summary>
public static class ReviewSchedule
{
    /// <summary>ESTIMATE: days between looks at an open sponsor talk (terms improve with waiting; a rival may take the sponsor).</summary>
    public const int SponsorCheckDays = 7;

    /// <summary>The day of the next review: the earliest of the given events strictly after <paramref name="today"/>, the next season and the scheduled date.</summary>
    public static DateOnly Next(DateOnly today, bool filedCommands, bool vacancyOpen, bool sponsorTalkOpen, IEnumerable<DateOnly> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var best = today.AddDays(AiEstimates.ScheduledReviewDays);
        var newSeason = new DateOnly(today.Year + 1, 1, 1);
        if (newSeason < best)
        {
            best = newSeason;
        }

        if (filedCommands)
        {
            best = Earlier(best, today.AddDays(AiEstimates.AfterActionDays));
        }

        if (vacancyOpen)
        {
            best = Earlier(best, today.AddDays(AiEstimates.VacancyRetryDays));
        }

        if (sponsorTalkOpen)
        {
            best = Earlier(best, today.AddDays(SponsorCheckDays));
        }

        foreach (var day in events)
        {
            if (day > today)
            {
                best = Earlier(best, day);
            }
        }

        return best;
    }

    private static DateOnly Earlier(DateOnly left, DateOnly right) => left <= right ? left : right;
}
