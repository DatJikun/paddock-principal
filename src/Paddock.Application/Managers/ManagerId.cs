namespace Paddock.Application.Managers;

/// <summary>
/// Stable id of a human or AI manager (INV-009). The default value is unassigned.
/// </summary>
public readonly record struct ManagerId
{
    private readonly string? _value;

    public ManagerId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _value = value;
    }

    public bool IsAssigned => !string.IsNullOrWhiteSpace(_value);

    public string Value
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_value))
            {
                throw new InvalidOperationException("Manager id is unassigned.");
            }

            return _value;
        }
    }

    public override string ToString() => _value ?? string.Empty;
}
