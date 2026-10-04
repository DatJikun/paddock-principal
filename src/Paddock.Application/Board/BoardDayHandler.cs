using Paddock.Simulation.Objectives;
using Paddock.Simulation.Time;

namespace Paddock.Application.Board;

/// <summary>
/// Once per lived day, the board does its work (<see cref="BoardEngine.DailyWork"/>): it grants the season's objectives, reviews
/// the teams after a race, dismisses and replaces principals, settles the reputation of a season that ends, and sends job offers to
/// managers without a team. It emits <see cref="BoardEventTypes"/> facts for the dismissals, appointments and offers.
/// <para>
/// RNG: only keyed children of the Market stream, which leave every stream state of the clock untouched (see
/// <see cref="BoardEngine"/>), so this handler never moves another system's draws (INV-004). It runs after the objective handler
/// (which emits the outcomes) and before the host applies them with <see cref="BoardOutcomes.Apply"/>.
/// </para>
/// </summary>
public sealed class BoardDayHandler : IDayHandler
{
    /// <summary>ESTIMATE: right after the objective handler, which is at 900.</summary>
    public const int DefaultOrder = 910;

    private readonly BoardEngine _engine;

    public BoardDayHandler(BoardEngine engine, int order = DefaultOrder)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
        Order = order;
    }

    public int Order { get; }

    public void OnDay(DayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var raceToday = context.DueEvents.Any(due => due.TypeId == ScheduledEventType.Race);
        foreach (var fact in _engine.DailyWork(context.Today, raceToday))
        {
            context.Emit(fact.TypeId, fact.Payload);
        }
    }
}

/// <summary>Applies the effects of the objectives the board granted, then records the outcomes on the objectives section.</summary>
public static class BoardOutcomes
{
    /// <summary>
    /// For every objective event of <paramref name="events"/> that names an open objective of a board, moves the board's confidence by
    /// the effect the objective stated; then settles the objectives (<see cref="ObjectiveOutcomes.Apply"/>). Applying the same events
    /// twice is harmless: a settled objective is skipped.
    /// </summary>
    public static void Apply(BoardEngine engine, IEnumerable<DomainEvent> events)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(events);
        var list = events as IReadOnlyList<DomainEvent> ?? events.ToArray();
        var book = engine.Book;
        foreach (var domainEvent in list)
        {
            if (domainEvent.TypeId is not (ObjectiveEventTypes.Met or ObjectiveEventTypes.Failed)
                || domainEvent.Payload is not ObjectiveOutcomePayload outcome
                || book.Objectives.Find(outcome.ObjectiveId) is not { IsOpen: true } objective)
            {
                continue;
            }

            engine.ApplyObjectiveOutcome(objective, outcome.Met);
        }

        book.Update(ObjectiveOutcomes.Apply(book.Objectives, list));
    }
}
