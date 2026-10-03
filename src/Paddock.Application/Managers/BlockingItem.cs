namespace Paddock.Application.Managers;

/// <summary>
/// A matter that holds the shared clock (for example an unresolved negotiation).
/// <see cref="Kind"/> is a stable code, not player-facing prose.
/// </summary>
public sealed record BlockingItem
{
    public BlockingItem(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        Kind = kind;
    }

    public string Kind { get; }
}
