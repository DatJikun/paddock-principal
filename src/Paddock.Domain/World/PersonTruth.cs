using Paddock.Domain.People;

namespace Paddock.Domain.World;

/// <summary>
/// What the simulation knows about a person: current attributes and the hidden per-attribute ceiling.
/// Not what an organization believes. That is <see cref="PersonKnowledge"/>.
/// Values are integers from 1 to 20 (<see cref="GenerationEstimates"/>). Potential is never below the current attribute.
/// </summary>
public sealed class PersonTruth
{
    public PersonTruth(IReadOnlyList<NamedAttribute> attributes, IReadOnlyList<NamedAttribute> potential)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentNullException.ThrowIfNull(potential);
        Attributes = Canonical(attributes, nameof(attributes));
        Potential = Canonical(potential, nameof(potential));
        if (Attributes.Count == 0)
        {
            throw new ArgumentException("A person needs at least one attribute.", nameof(attributes));
        }

        if (Attributes.Count != Potential.Count)
        {
            throw new ArgumentException("Potential must list the same attributes.", nameof(potential));
        }

        for (var i = 0; i < Attributes.Count; i++)
        {
            if (!string.Equals(Attributes[i].Key, Potential[i].Key, StringComparison.Ordinal))
            {
                throw new ArgumentException("Potential must list the same attributes.", nameof(potential));
            }

            if (Potential[i].Value < Attributes[i].Value)
            {
                throw new ArgumentException(
                    "Potential is below the current attribute for " + Attributes[i].Key + ".",
                    nameof(potential));
            }
        }
    }

    public IReadOnlyList<NamedAttribute> Attributes { get; }

    public IReadOnlyList<NamedAttribute> Potential { get; }

    public int Value(string key)
    {
        var attribute = Find(Attributes, key);
        return attribute.Value;
    }

    public int PotentialValue(string key)
    {
        var attribute = Find(Potential, key);
        return attribute.Value;
    }

    public static PersonTruth FromDriver(DriverAttributes current, DriverAttributes potential)
    {
        current.EnsureValid();
        potential.EnsureValid();
        var attributes = new NamedAttribute[GenerationEstimates.DriverAttributeKeys.Length];
        var ceilings = new NamedAttribute[attributes.Length];
        for (var i = 0; i < attributes.Length; i++)
        {
            var key = GenerationEstimates.DriverAttributeKeys[i];
            attributes[i] = new NamedAttribute(key, current.Get(key));
            ceilings[i] = new NamedAttribute(key, potential.Get(key));
        }

        return new PersonTruth(attributes, ceilings);
    }

    private static NamedAttribute Find(IReadOnlyList<NamedAttribute> attributes, string key)
    {
        var canonical = AttributeKeys.Require(key, nameof(key));
        foreach (var attribute in attributes)
        {
            if (string.Equals(attribute.Key, canonical, StringComparison.Ordinal))
            {
                return attribute;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown attribute.");
    }

    private static NamedAttribute[] Canonical(IReadOnlyList<NamedAttribute> source, string paramName)
    {
        var copy = new NamedAttribute[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            var attribute = source[i];
            copy[i] = new NamedAttribute(AttributeKeys.Require(attribute.Key, paramName), attribute.Value);
        }

        Array.Sort(copy, static (left, right) => string.CompareOrdinal(left.Key, right.Key));
        for (var i = 1; i < copy.Length; i++)
        {
            if (string.Equals(copy[i].Key, copy[i - 1].Key, StringComparison.Ordinal))
            {
                throw new ArgumentException("Duplicate attribute '" + copy[i].Key + "'.", paramName);
            }
        }

        return copy;
    }
}
