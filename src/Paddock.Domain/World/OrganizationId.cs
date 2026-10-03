namespace Paddock.Domain.World;

/// <summary>
/// Stable id of an organization (INV-009). Real organizations keep their authored id.
/// Generated ones use <c>org:{sequence}</c>. The default value is unassigned.
/// </summary>
public readonly record struct OrganizationId : IComparable<OrganizationId>
{
    private readonly string? _value;

    private OrganizationId(string value, bool isReal)
    {
        _value = value;
        IsReal = isReal;
    }

    public bool IsAssigned => !string.IsNullOrEmpty(_value);

    public bool IsReal { get; }

    public string Value => _value ?? throw new InvalidOperationException("Organization id is unassigned.");

    public static OrganizationId Real(string authoredId) => new(IdText.RequireReal(authoredId, nameof(authoredId)), true);

    public static OrganizationId Generated(long sequence) => new(IdText.Generated(IdText.OrganizationPrefix, sequence), false);

    public int CompareTo(OrganizationId other) => string.CompareOrdinal(_value, other._value);

    public override string ToString() => _value ?? string.Empty;
}
