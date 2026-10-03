using Paddock.Domain.Objectives;
using Paddock.Domain.Time;
using Paddock.Simulation.Time;

namespace Paddock.Simulation.Objectives;

/// <summary>Type ids of the domain events the objective handler emits. Machine identifiers, not player text.</summary>
public static class ObjectiveEventTypes
{
    public const string Met = "objective.met";

    public const string Failed = "objective.failed";
}

/// <summary>
/// The outcome of one objective on the day it was due. <see cref="Value"/> is the number the predicate read, when it reads one
/// and the owning system could supply it. Not scheduled, only emitted, so the event queue hash does not need a form for it.
/// </summary>
public sealed record ObjectiveOutcomePayload : EventPayload
{
    public ObjectiveOutcomePayload(string objectiveId, string ownerId, string grantorId, bool met, decimal? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectiveId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(grantorId);
        ObjectiveId = objectiveId;
        OwnerId = ownerId;
        GrantorId = grantorId;
        Met = met;
        Value = value;
    }

    public string ObjectiveId { get; }

    public string OwnerId { get; }

    public string GrantorId { get; }

    public bool Met { get; }

    public decimal? Value { get; }
}

/// <summary>
/// Once per day, finds the open objectives whose deadline is today or earlier, reads their predicate against the facts,
/// and emits <see cref="ObjectiveEventTypes.Met"/> or <see cref="ObjectiveEventTypes.Failed"/> for each, in objective order.
/// An unknown fact (the owning system cannot say) counts as not met: an objective is met only when it can be shown.
/// <para>
/// The handler changes nothing. The owning system reacts to the event and applies the objective's effect, and the host
/// records the outcome with <see cref="ObjectiveOutcomes.Apply"/> after the day, so the objective stops being due.
/// Until then a day that is lived again would emit the same event, which is why the host applies outcomes in the same step.
/// It draws no RNG (INV-004 is not touched) and reads only the objectives and facts it was given (INV-005).
/// </para>
/// </summary>
public sealed class ObjectiveDayHandler : IDayHandler
{
    /// <summary>ESTIMATE: after the handlers that update the facts (standings, finance) and before the ones that react.</summary>
    public const int DefaultOrder = 900;

    private readonly Func<ObjectivesSection> _objectives;
    private readonly IObjectiveFacts _facts;

    public ObjectiveDayHandler(Func<ObjectivesSection> objectives, IObjectiveFacts facts, int order = DefaultOrder)
    {
        ArgumentNullException.ThrowIfNull(objectives);
        ArgumentNullException.ThrowIfNull(facts);
        _objectives = objectives;
        _facts = facts;
        Order = order;
    }

    public int Order { get; }

    public void OnDay(DayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var objective in _objectives().Due(context.Today))
        {
            var met = objective.Predicate.Evaluate(objective.Owner, _facts) == true;
            decimal? value = objective.Predicate is NumericPredicate numeric ? _facts.Number(objective.Owner, numeric.FactKey) : null;
            context.Emit(
                met ? ObjectiveEventTypes.Met : ObjectiveEventTypes.Failed,
                new ObjectiveOutcomePayload(objective.Id, objective.Owner.Value, objective.Grantor.Value, met, value));
        }
    }
}

/// <summary>Records the outcomes the handler emitted onto the objectives section.</summary>
public static class ObjectiveOutcomes
{
    /// <summary>
    /// Settles every objective named by an objective event in <paramref name="events"/>. Other events are ignored,
    /// and an objective that is already settled is left alone, so applying a day's events twice is harmless.
    /// </summary>
    public static ObjectivesSection Apply(ObjectivesSection section, IEnumerable<DomainEvent> events)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(events);
        foreach (var domainEvent in events)
        {
            if (domainEvent.TypeId is not (ObjectiveEventTypes.Met or ObjectiveEventTypes.Failed)
                || domainEvent.Payload is not ObjectiveOutcomePayload outcome)
            {
                continue;
            }

            if (section.Find(outcome.ObjectiveId) is { IsOpen: true })
            {
                section = section.Settle(outcome.ObjectiveId, outcome.Met, domainEvent.Date);
            }
        }

        return section;
    }
}
