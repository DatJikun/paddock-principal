namespace Paddock.Domain.World;

/// <summary>
/// Stable id of a contract (INV-009). Contracts are issued from the world counter as <c>con:{sequence}</c>.
/// The default value is unassigned.
/// </summary>
public readonly record struct ContractId : IComparable<ContractId>
{
    private readonly string? _value;

    private ContractId(string value)
    {
        _value = value;
    }

    public bool IsAssigned => !string.IsNullOrEmpty(_value);

    public string Value => _value ?? throw new InvalidOperationException("Contract id is unassigned.");

    public static ContractId Generated(long sequence) => new(IdText.Generated(IdText.ContractPrefix, sequence));

    public int CompareTo(ContractId other) => string.CompareOrdinal(_value, other._value);

    public override string ToString() => _value ?? string.Empty;
}
