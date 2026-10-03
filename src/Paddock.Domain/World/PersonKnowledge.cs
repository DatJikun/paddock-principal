namespace Paddock.Domain.World;

/// <summary>
/// One attribute band believed by an organization. Not a true rating.
/// </summary>
public readonly record struct KnownAttribute
{
    public KnownAttribute(string key, AttributeBand band)
    {
        Key = AttributeKeys.Require(key, nameof(key));
        Band = band;
    }

    public string Key { get; }

    public AttributeBand Band { get; }
}

/// <summary>
/// What one organization believes about one person (INV-003).
/// Bands only. This type does not store or accept <see cref="PersonTruth"/>.
/// Scouting, which narrows the bands, is out of scope.
/// </summary>
public sealed class PersonKnowledge
{
    public PersonKnowledge(
        OrganizationId observerId,
        PersonId subjectId,
        IReadOnlyList<KnownAttribute> attributes,
        AttributeBand? potential)
    {
        if (!observerId.IsAssigned)
        {
            throw new ArgumentException("Observer id is unassigned.", nameof(observerId));
        }

        if (!subjectId.IsAssigned)
        {
            throw new ArgumentException("Subject id is unassigned.", nameof(subjectId));
        }

        ArgumentNullException.ThrowIfNull(attributes);
        ObserverId = observerId;
        SubjectId = subjectId;
        Attributes = Canonical(attributes);
        Potential = potential;
    }

    public OrganizationId ObserverId { get; }

    public PersonId SubjectId { get; }

    public IReadOnlyList<KnownAttribute> Attributes { get; }

    public AttributeBand? Potential { get; }

    private static KnownAttribute[] Canonical(IReadOnlyList<KnownAttribute> source)
    {
        var copy = source.ToArray();
        Array.Sort(copy, static (left, right) => string.CompareOrdinal(left.Key, right.Key));
        for (var i = 1; i < copy.Length; i++)
        {
            if (string.Equals(copy[i].Key, copy[i - 1].Key, StringComparison.Ordinal))
            {
                throw new ArgumentException("Duplicate knowledge attribute '" + copy[i].Key + "'.", nameof(source));
            }
        }

        return copy;
    }
}

/// <summary>
/// Knowledge handed to a caller that must not see simulation truth.
/// </summary>
/// <remarks>
/// Compile-time separation (INV-003): <see cref="PersonKnowledge"/> and <see cref="PersonKnowledgeView"/>
/// expose observer, subject, and bands only. They have no field, property, or method of type
/// <see cref="Person"/> or <see cref="PersonTruth"/>, so a method that receives this view cannot
/// read true attributes. The compiler rejects <c>view.Truth</c> because that member does not exist.
/// <see cref="Person"/> is the only type that stores <see cref="PersonTruth"/>.
/// <see cref="WorldState.TruthOf"/> is the explicit truth query.
/// <see cref="WorldState.KnowledgeOf"/> returns this view and does not substitute truth when the
/// organization has recorded no belief.
/// </remarks>
public readonly record struct PersonKnowledgeView
{
    public PersonKnowledgeView(PersonKnowledge knowledge)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        ObserverId = knowledge.ObserverId;
        SubjectId = knowledge.SubjectId;
        Attributes = knowledge.Attributes;
        Potential = knowledge.Potential;
    }

    public OrganizationId ObserverId { get; }

    public PersonId SubjectId { get; }

    public IReadOnlyList<KnownAttribute> Attributes { get; }

    public AttributeBand? Potential { get; }
}
