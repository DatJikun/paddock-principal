namespace Paddock.Application.Access;

/// <summary>Stable identifier of a manager (player or AI). Never reused (INV-009).</summary>
public readonly record struct ManagerId
{
    public ManagerId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
