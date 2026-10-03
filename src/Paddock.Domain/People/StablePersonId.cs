using System.Globalization;

namespace Paddock.Domain.People;

/// <summary>
/// Counter id for a generated person. The canonical string is <c>gen:{sequence}</c>.
/// Real people keep their own ids and do not go through this type (INV-009).
/// </summary>
public readonly record struct StablePersonId : IComparable<StablePersonId>
{
    public StablePersonId(long sequence)
    {
        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Generated ids start at 1.");
        }

        Sequence = sequence;
    }

    public long Sequence { get; }

    public string Value => "gen:" + Sequence.ToString(CultureInfo.InvariantCulture);

    public int CompareTo(StablePersonId other) => Sequence.CompareTo(other.Sequence);

    public override string ToString() => Value;
}
