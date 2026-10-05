using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Objectives;

/// <summary>
/// Every objective in the world, as the world section named <see cref="SectionName"/>. Like the inbox it keeps its own
/// counter (ids are <c>obj:{n}</c>, never reused) and is immutable. Settled objectives stay as records.
/// Saved by <c>ObjectivesSectionStore</c> (added with the first system that grants objectives, T38 sponsors).
/// <para>
/// Canonical text (<see cref="SchemaVersion"/> 1), after the section header written by the state hash:
/// <code>
/// next &lt;nextNumber&gt;
/// objectives &lt;count&gt;
/// objective &lt;len&gt;:&lt;id&gt;
/// owner &lt;len&gt;:&lt;organizationId&gt;
/// grantor &lt;len&gt;:&lt;organizationId&gt;
/// kind &lt;len&gt;:&lt;kindKey&gt;
/// reason &lt;len&gt;:&lt;reasonKey&gt;
/// predicate &lt;len&gt;:&lt;name&gt; &lt;len&gt;:&lt;parameter&gt;
/// baseline &lt;len&gt;:&lt;invariant decimal or -&gt;
/// created &lt;len&gt;:&lt;date&gt;
/// deadline &lt;len&gt;:&lt;date&gt;
/// met &lt;len&gt;:&lt;effectKey&gt;
/// met-arguments &lt;count&gt;
/// argument &lt;len&gt;:&lt;name&gt; &lt;len&gt;:&lt;value&gt;
/// failed &lt;len&gt;:&lt;effectKey&gt;
/// failed-arguments &lt;count&gt;
/// argument &lt;len&gt;:&lt;name&gt; &lt;len&gt;:&lt;value&gt;
/// status &lt;Open|Met|Failed|Withdrawn&gt;
/// settled &lt;len&gt;:&lt;date or -&gt;
/// </code>
/// </para>
/// </summary>
public sealed class ObjectivesSection : IWorldSection
{
    public const string SectionName = "objectives";

    public const string IdPrefix = "obj:";

    private readonly SortedDictionary<long, Objective> _objectives;

    private ObjectivesSection(long nextNumber, SortedDictionary<long, Objective> objectives)
    {
        NextNumber = nextNumber;
        _objectives = objectives;
    }

    public static ObjectivesSection Empty { get; } = new(1, []);

    public string Name => SectionName;

    public int SchemaVersion => 1;

    public long NextNumber { get; }

    public IReadOnlyList<Objective> Objectives => _objectives.Values.ToArray();

    public static string IdOf(long number) =>
        IdPrefix + number.ToString(CultureInfo.InvariantCulture);

    /// <summary>Rebuilds the section from stored objectives. Numbers are unique and below <paramref name="nextNumber"/>.</summary>
    public static ObjectivesSection Restore(long nextNumber, IEnumerable<Objective> objectives)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nextNumber, 1);
        ArgumentNullException.ThrowIfNull(objectives);
        var items = new SortedDictionary<long, Objective>();
        foreach (var objective in objectives)
        {
            if (objective.Number >= nextNumber || !items.TryAdd(objective.Number, objective))
            {
                throw new InvalidOperationException("Objective number " + objective.Number.ToString(CultureInfo.InvariantCulture) + " is duplicated or past the counter.");
            }
        }

        if (items.Count != nextNumber - 1)
        {
            throw new InvalidOperationException("The objective counter does not match the number of objectives.");
        }

        return new ObjectivesSection(nextNumber, items);
    }

    public Objective? Find(string objectiveId)
    {
        ArgumentNullException.ThrowIfNull(objectiveId);
        if (!objectiveId.StartsWith(IdPrefix, StringComparison.Ordinal)
            || !long.TryParse(objectiveId.AsSpan(IdPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || IdOf(number) != objectiveId)
        {
            return null;
        }

        return _objectives.GetValueOrDefault(number);
    }

    public IReadOnlyList<Objective> OwnedBy(OrganizationId owner) =>
        _objectives.Values.Where(objective => objective.Owner == owner).ToArray();

    /// <summary>Open objectives whose deadline is today or earlier, in order of their number.</summary>
    public IReadOnlyList<Objective> Due(GameDate today) =>
        _objectives.Values.Where(objective => objective.IsOpen && objective.Deadline <= today).ToArray();

    public (ObjectivesSection Section, Objective Objective) Add(ObjectiveDraft draft, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (NextNumber == long.MaxValue)
        {
            throw new InvalidOperationException("The objective id counter is exhausted.");
        }

        var objective = new Objective(NextNumber, today, draft, ObjectiveStatus.Open, null);
        var items = new SortedDictionary<long, Objective>(_objectives) { [objective.Number] = objective };
        return (new ObjectivesSection(NextNumber + 1, items), objective);
    }

    /// <summary>Records the outcome of an open objective. Applying the effect is the owning system's job.</summary>
    public ObjectivesSection Settle(string objectiveId, bool met, GameDate today)
    {
        var objective = Find(objectiveId) ?? throw new InvalidOperationException($"Unknown objective '{objectiveId}'.");
        if (!objective.IsOpen)
        {
            throw new InvalidOperationException($"Objective '{objectiveId}' is already {objective.Status}.");
        }

        var items = new SortedDictionary<long, Objective>(_objectives) { [objective.Number] = objective.Settled(met, today) };
        return new ObjectivesSection(NextNumber, items);
    }

    /// <summary>
    /// Closes an open objective without an outcome. Nothing is told to apply its effect: a withdrawn objective was not met and did not fail.
    /// </summary>
    public ObjectivesSection Withdraw(string objectiveId, GameDate today)
    {
        var objective = Find(objectiveId) ?? throw new InvalidOperationException($"Unknown objective '{objectiveId}'.");
        if (!objective.IsOpen)
        {
            throw new InvalidOperationException($"Objective '{objectiveId}' is already {objective.Status}.");
        }

        var items = new SortedDictionary<long, Objective>(_objectives) { [objective.Number] = objective.Withdrawn(today) };
        return new ObjectivesSection(NextNumber, items);
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Number("next", NextNumber);
        writer.Count("objectives", _objectives.Count);
        foreach (var objective in _objectives.Values)
        {
            writer.TextLine("objective", objective.Id);
            writer.TextLine("owner", objective.Owner.Value);
            writer.TextLine("grantor", objective.Grantor.Value);
            writer.TextLine("kind", objective.KindKey);
            writer.TextLine("reason", objective.ReasonKey);
            writer.Begin("predicate");
            writer.Field(objective.Predicate.Name);
            writer.Space();
            writer.Field(objective.Predicate.Parameter);
            writer.End();
            writer.TextLine("baseline", objective.Baseline is decimal baseline ? NumericPredicate.Invariant(baseline) : "-");
            writer.TextLine("created", objective.Created.ToString());
            writer.TextLine("deadline", objective.Deadline.ToString());
            WriteEffect(writer, "met", objective.EffectOnMet);
            WriteEffect(writer, "failed", objective.EffectOnFailed);
            writer.Line("status " + objective.Status);
            writer.TextLine("settled", objective.SettledOn?.ToString() ?? "-");
        }
    }

    private static void WriteEffect(CanonicalWriter writer, string label, ObjectiveEffect effect)
    {
        writer.TextLine(label, effect.Key);
        writer.Count(label + "-arguments", effect.Arguments.Count);
        foreach (var argument in effect.Arguments)
        {
            writer.Begin("argument");
            writer.Field(argument.Key);
            writer.Space();
            writer.Field(argument.Value);
            writer.End();
        }
    }
}
