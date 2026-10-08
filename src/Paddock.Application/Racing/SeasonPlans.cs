using Paddock.Domain.Racing;
using Paddock.Domain.World;
using Paddock.Simulation.Regulation;
using Paddock.Simulation.Time;

namespace Paddock.Application.Racing;

/// <summary>
/// A season's weekend plan as the career stored it (#229). The plan is made once, when the season is first scheduled, from the
/// host's real race dates or the even spacing, and kept in the <c>race-calendar</c> section so it never changes afterwards.
/// A season with no stored plan (a save from before the section) is read with the even spacing it was scheduled with.
/// </summary>
public static class SeasonPlans
{
    /// <summary>The stored plan of <paramref name="season"/>, or the even-spacing plan when none is stored.</summary>
    public static IReadOnlyList<SeasonCalendar.PlannedSession> Read(
        WorldState world,
        int season,
        IReadOnlyList<TrackLayout> layouts,
        IReadOnlyList<RaceAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Stored(world, season) ?? SeasonCalendar.Plan(season, layouts, assignments);
    }

    /// <summary>The number of rounds of <paramref name="season"/> and its last round number, from the stored plan; null when none is stored.</summary>
    public static (int Rounds, int LastRound)? Rounds(WorldState world, int season)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (Stored(world, season) is not { } plan)
        {
            return null;
        }

        var rounds = 0;
        var last = 0;
        foreach (var session in plan)
        {
            if (session.TypeId == ScheduledEventType.Race)
            {
                rounds++;
                last = Math.Max(last, session.Round);
            }
        }

        return rounds == 0 ? null : (rounds, last);
    }

    /// <summary>The stored plan of <paramref name="season"/>, or null when the world has none for it.</summary>
    public static IReadOnlyList<SeasonCalendar.PlannedSession>? Stored(WorldState world, int season)
    {
        ArgumentNullException.ThrowIfNull(world);
        var section = world.Section<RaceCalendarSection>(RaceCalendarSection.SectionName);
        if (section is null || !section.HasSeason(season))
        {
            return null;
        }

        return section.SeasonSessions(season)
            .Select(session => new SeasonCalendar.PlannedSession(session.Date, session.TypeId, session.Season, session.Round, session.LayoutId))
            .ToArray();
    }

    /// <summary>
    /// The plan to schedule for <paramref name="season"/> and the world that keeps it. A plan already stored is returned as it is;
    /// otherwise a new one is made (real dates when <paramref name="dates"/> covers the season) and stored. An empty plan is not stored.
    /// </summary>
    public static (IReadOnlyList<SeasonCalendar.PlannedSession> Plan, WorldState World) Ensure(
        WorldState world,
        int season,
        IReadOnlyList<TrackLayout> layouts,
        IReadOnlyList<RaceAssignment> assignments,
        RaceDateBook? dates)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (Stored(world, season) is { } stored)
        {
            return (stored, world);
        }

        // A voted career lays a season out from the authored map with the calendar policy its teams voted (#275). The policy of a
        // season is fixed before the season is laid out (the votes of the season before are resolved by October), and a plan
        // that is already stored is returned above, so a vote never changes a season that is laid out.
        var policy = world.Section<RegulationsSection>(RegulationsSection.SectionName)?.Find(SeriesIds.WorldChampionship)?.CalendarFor(season);
        var (planAssignments, planDates) = policy is { Count: > 0 }
            ? CalendarPolicy.Resolve(season, layouts, assignments, dates, policy)
            : (assignments, dates);
        var plan = SeasonCalendar.Plan(season, layouts, planAssignments, planDates);
        if (plan.Count == 0)
        {
            return (plan, world);
        }

        var section = world.Section<RaceCalendarSection>(RaceCalendarSection.SectionName) ?? RaceCalendarSection.Empty;
        var rows = plan.Select(item => new CalendarSession(item.Season, item.Round, item.TypeId, item.Date, item.LayoutId)).ToArray();
        return (plan, world.WithSection(section.WithSeason(season, rows)));
    }
}
