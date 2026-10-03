using Paddock.Domain.Inbox;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Objectives;

public enum ObjectiveStatus
{
    Open,
    Met,
    Failed,
}

/// <summary>
/// What happens when an objective is met or failed, named up front so the owner knows the stakes (DESIGN §3.2).
/// <see cref="Key"/> is a translation key for the player and also tells the owning system what to apply;
/// the effect itself is applied by that system when it sees the outcome event, never here.
/// </summary>
public sealed record ObjectiveEffect
{
    public ObjectiveEffect(string key, IEnumerable<KeyValuePair<string, string>>? arguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
        Arguments = InboxItemDraft.CopyArguments(arguments);
    }

    public string Key { get; }

    /// <summary>Invariant argument values for the text and for the system that applies the effect, in ordinal order of the name.</summary>
    public IReadOnlyDictionary<string, string> Arguments { get; }
}

/// <summary>
/// What a grantor (a board, a sponsor, a series) asks of an owner. <see cref="Baseline"/> is the value of the
/// predicate's number on the day it was granted (required for a numeric predicate, absent for the others);
/// the forecast needs it as a starting point.
/// An objective is evaluated on or after <see cref="Deadline"/>.
/// </summary>
public sealed class ObjectiveDraft
{
    public ObjectiveDraft(
        OrganizationId owner,
        OrganizationId grantor,
        string kindKey,
        string reasonKey,
        ObjectivePredicate predicate,
        decimal? baseline,
        GameDate deadline,
        ObjectiveEffect effectOnMet,
        ObjectiveEffect effectOnFailed)
    {
        if (!owner.IsAssigned || !grantor.IsAssigned)
        {
            throw new ArgumentException("Owner and grantor must be assigned organizations.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(kindKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonKey);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(effectOnMet);
        ArgumentNullException.ThrowIfNull(effectOnFailed);
        if ((baseline is not null) != (predicate is NumericPredicate))
        {
            throw new ArgumentException("A numeric predicate needs a baseline and any other predicate has none.", nameof(baseline));
        }

        Owner = owner;
        Grantor = grantor;
        KindKey = kindKey;
        ReasonKey = reasonKey;
        Predicate = predicate;
        Baseline = baseline;
        Deadline = deadline;
        EffectOnMet = effectOnMet;
        EffectOnFailed = effectOnFailed;
    }

    public OrganizationId Owner { get; }

    public OrganizationId Grantor { get; }

    /// <summary>Stable kind code, also a translation key for the objective's title.</summary>
    public string KindKey { get; }

    /// <summary>Translation key of the grantor's reason for asking (the WHY of the view).</summary>
    public string ReasonKey { get; }

    public ObjectivePredicate Predicate { get; }

    public decimal? Baseline { get; }

    public GameDate Deadline { get; }

    public ObjectiveEffect EffectOnMet { get; }

    public ObjectiveEffect EffectOnFailed { get; }
}

/// <summary>One objective in the world. Immutable; <see cref="ObjectivesSection"/> returns a new one when it is settled.</summary>
public sealed class Objective
{
    public Objective(
        long number,
        GameDate created,
        ObjectiveDraft draft,
        ObjectiveStatus status,
        GameDate? settledOn)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Deadline < created)
        {
            throw new ArgumentException("The deadline is before the day the objective was granted.", nameof(draft));
        }

        if (status == ObjectiveStatus.Open != (settledOn is null))
        {
            throw new ArgumentException("An open objective has no settling date, and a settled one has one.", nameof(settledOn));
        }

        Number = number;
        Created = created;
        Draft = draft;
        Status = status;
        SettledOn = settledOn;
    }

    public long Number { get; }

    public ObjectiveDraft Draft { get; }

    /// <summary>Stable id, <c>obj:{number}</c>.</summary>
    public string Id => ObjectivesSection.IdOf(Number);

    public OrganizationId Owner => Draft.Owner;

    public OrganizationId Grantor => Draft.Grantor;

    public string KindKey => Draft.KindKey;

    public string ReasonKey => Draft.ReasonKey;

    public ObjectivePredicate Predicate => Draft.Predicate;

    public decimal? Baseline => Draft.Baseline;

    public GameDate Created { get; }

    public GameDate Deadline => Draft.Deadline;

    public ObjectiveEffect EffectOnMet => Draft.EffectOnMet;

    public ObjectiveEffect EffectOnFailed => Draft.EffectOnFailed;

    public ObjectiveStatus Status { get; }

    public GameDate? SettledOn { get; }

    public bool IsOpen => Status == ObjectiveStatus.Open;

    internal Objective Settled(bool met, GameDate on) =>
        new(Number, Created, Draft, met ? ObjectiveStatus.Met : ObjectiveStatus.Failed, on);
}
