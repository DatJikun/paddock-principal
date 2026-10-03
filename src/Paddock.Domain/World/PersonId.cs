namespace Paddock.Domain.World;

/// <summary>
/// Stable id of a person (INV-009). Real people keep their authored or Jolpica id.
/// Generated people use <c>gen:{sequence}</c>, the same text as <see cref="People.StablePersonId"/>.
/// The default value is unassigned.
/// </summary>
public readonly record struct PersonId : IComparable<PersonId>
{
    private readonly string? _value;

    private PersonId(string value, bool isReal)
    {
        _value = value;
        IsReal = isReal;
    }

    public bool IsAssigned => !string.IsNullOrEmpty(_value);

    public bool IsReal { get; }

    public string Value => _value ?? throw new InvalidOperationException("Person id is unassigned.");

    public static PersonId Real(string authoredId) => new(IdText.RequireReal(authoredId, nameof(authoredId)), true);

    public static PersonId Generated(long sequence) => new(IdText.Generated(IdText.PersonPrefix, sequence), false);

    public int CompareTo(PersonId other) => string.CompareOrdinal(_value, other._value);

    public override string ToString() => _value ?? string.Empty;
}
