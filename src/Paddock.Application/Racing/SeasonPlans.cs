using Paddock.Domain.Racing;
using Paddock.Domain.World;
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

        var plan = SeasonCalendar.Plan(season, layouts, assignments, dates);
        if (plan.Count == 0)
        {
            return (plan, world);
        }

        var section = world.Section<RaceCalendarSection>(RaceCalendarSection.SectionName) ?? RaceCalendarSection.Empty;
        var rows = plan.Select(item => new CalendarSession(item.Season, item.Round, item.TypeId, item.Date, item.LayoutId)).ToArray();
        return (plan, world.WithSection(section.WithSeason(season, rows)));
    }
}
