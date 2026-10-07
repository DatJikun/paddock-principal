using Paddock.Application.Career;
using Paddock.Domain.World;
using Paddock.Simulation.Time;

namespace Paddock.Application.Racing;

/// <summary>One round of a season that a quick race can be run on, with the day a new career would race it and the track's outline.</summary>
public sealed record QuickRoundView(
    int Round,
    string LayoutId,
    string? CircuitName,
    string? Country,
    string Date,
    IReadOnlyList<TrackPointView> Points);

/// <summary>The rounds of <see cref="Season"/>, in order. Empty when the season has no calendar.</summary>
public sealed record QuickRoundsView(int Season, IReadOnlyList<QuickRoundView> Rounds);

/// <summary>
/// The quick race (#280): one round raced on its own in a world built exactly as a new career builds it. The weekend is
/// <see cref="RaceWeekendDay.RunAlone"/>, the career's own race day without the championship, so a quick race has no rules of
/// its own (PP-058) and the same season, team, round and seed give the same race (INV-002).
/// </summary>
public static class QuickRace
{
    /// <summary>The race days of <paramref name="season"/> as a new career would schedule them. No state, no RNG.</summary>
    public static QuickRoundsView Rounds(
        int season,
        IReadOnlyList<TrackLayout> layouts,
        IReadOnlyList<RaceAssignment> assignments,
        RaceDateBook? dates,
        IReadOnlyDictionary<string, CircuitLabel> circuits,
        IReadOnlyDictionary<string, TrackFacts> tracks)
    {
        ArgumentNullException.ThrowIfNull(layouts);
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(circuits);
        ArgumentNullException.ThrowIfNull(tracks);
        var rounds = new List<QuickRoundView>();
        foreach (var planned in SeasonCalendar.Plan(season, layouts, assignments, dates))
        {
            if (planned.TypeId != ScheduledEventType.Race)
            {
                continue;
            }

            circuits.TryGetValue(planned.LayoutId, out var circuit);
            tracks.TryGetValue(planned.LayoutId, out var track);
            rounds.Add(new QuickRoundView(
                planned.Round,
                planned.LayoutId,
                circuit?.Name,
                circuit?.Country,
                planned.Date.ToString(),
                track?.Points ?? []));
        }

        rounds.Sort((a, b) => a.Round.CompareTo(b.Round));
        return new QuickRoundsView(season, rounds);
    }

    /// <summary>
    /// Races <paramref name="round"/> of the season the career opened in, on its planned day, and hands it to the career's
    /// <see cref="RaceWatch"/>. The career must not have lived a day yet. Null on success, otherwise the refusal key.
    /// </summary>
    public static string? Run(CareerModuleContext modules, int round)
    {
        ArgumentNullException.ThrowIfNull(modules);
        var session = modules.Session;
        var season = session.Date.Year;
        foreach (var scheduled in session.Clock.Queue.Events)
        {
            if (scheduled.TypeId != ScheduledEventType.Race
                || scheduled.Payload is not RaceSessionPayload payload
                || payload.Season != season
                || payload.Round != round)
            {
                continue;
            }

            var day = new RaceWeekendDay(modules, modules.Require<RaceWatch>());
            return day.RunAlone(scheduled.Date, payload) ? null : QuickRaceKeys.NotRaced;
        }

        return QuickRaceKeys.NoRound;
    }
}

/// <summary>Refusals of the quick race.</summary>
public static class QuickRaceKeys
{
    public const string NoRound = "quick.error.noRound";
    public const string NotRaced = "quick.error.notRaced";
    public const string NoTeam = "quick.error.noTeam";
}
