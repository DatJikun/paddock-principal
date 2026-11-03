using Paddock.Application.Career;
using Paddock.Simulation.Time;

namespace Paddock.Application.Racing;

/// <summary>
/// The career's race of today, watched before the day books it (#316). Watching runs <see cref="RaceWeekendDay.Preview"/>, the
/// round the race day will run, and the pit walls steer it with the same orders and the same re-run as a quick race. Living the
/// day then books that race with those orders; living it without watching books the same race with none. So the career, the
/// quick race, watching and simulating are one race model (PP-058) and there is no race that cannot take orders.
/// </summary>
public static class CareerRace
{
    /// <summary>
    /// Opens today's race in the career's <see cref="RaceWatch"/>. A second call on the same race keeps the orders already given.
    /// Null on success, otherwise the refusal key.
    /// </summary>
    public static string? Watch(CareerModuleContext modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        var session = modules.Session;
        var watch = modules.Require<RaceWatch>();
        foreach (var scheduled in session.Clock.Queue.Events)
        {
            if (scheduled.TypeId != ScheduledEventType.Race
                || scheduled.Date != session.Date
                || scheduled.Payload is not RaceSessionPayload payload)
            {
                continue;
            }

            if (watch.Steering is not null && watch.Season == payload.Season && watch.Round == payload.Round)
            {
                return null;
            }

            var day = new RaceWeekendDay(modules, watch);
            return day.Preview(scheduled.Date, payload) ? null : CareerRaceKeys.NotRaced;
        }

        return CareerRaceKeys.NoRace;
    }
}

/// <summary>Refusals of watching the career's race.</summary>
public static class CareerRaceKeys
{
    /// <summary>There is no race today.</summary>
    public const string NoRace = "race.watch.noRace";

    /// <summary>Today's round cannot be raced (no rules for the season, no field).</summary>
    public const string NotRaced = "race.watch.notRaced";
}
