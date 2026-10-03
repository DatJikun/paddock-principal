namespace Paddock.Application.Managers;

/// <summary>
/// Managers known to the host. Issued ids are never recycled (INV-009).
/// Removal is not supported yet; a second <see cref="Register"/> of the same id throws.
/// </summary>
public sealed class ManagerRegistry
{
    private readonly Dictionary<string, Entry> _byId = new(StringComparer.Ordinal);
    private readonly List<Entry> _order = [];

    public IReadOnlyList<ManagerSnapshot> All => _order.Select(entry => entry.Snapshot()).ToArray();

    public ManagerSnapshot Register(ManagerId id, ManagerKind kind, string displayName)
    {
        if (!id.IsAssigned)
        {
            throw new ArgumentException("Manager id is required.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (_byId.ContainsKey(id.Value))
        {
            throw new InvalidOperationException(
                $"Manager id '{id.Value}' was already issued and cannot be reused.");
        }

        var entry = new Entry(id, kind, displayName);
        _byId.Add(id.Value, entry);
        _order.Add(entry);
        return entry.Snapshot();
    }

    public bool Contains(ManagerId id) => id.IsAssigned && _byId.ContainsKey(id.Value);

    public ManagerSnapshot Get(ManagerId id) => Find(id).Snapshot();

    public ManagerKind KindOf(ManagerId id) => Find(id).Kind;

    public void Rename(ManagerId id, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Find(id).DisplayName = displayName;
    }

    public void SetReady(ManagerId id, bool isReady)
    {
        Find(id).IsReady = isReady;
    }

    public void PostBlockingItem(ManagerId id, BlockingItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var entry = Find(id);
        if (entry.BlockingItem is not null)
        {
            throw new InvalidOperationException($"Manager '{id.Value}' already has a blocking item.");
        }

        entry.BlockingItem = item;
    }

    public void ClearBlockingItem(ManagerId id)
    {
        var entry = Find(id);
        if (entry.BlockingItem is null)
        {
            throw new InvalidOperationException($"Manager '{id.Value}' has no blocking item.");
        }

        entry.BlockingItem = null;
    }

    public void ResetReadiness()
    {
        foreach (var entry in _order)
        {
            entry.IsReady = false;
        }
    }

    private Entry Find(ManagerId id)
    {
        if (!id.IsAssigned)
        {
            throw new ArgumentException("Manager id is required.", nameof(id));
        }

        if (!_byId.TryGetValue(id.Value, out var entry))
        {
            throw new ArgumentException($"Unknown manager '{id.Value}'.", nameof(id));
        }

        return entry;
    }

    private sealed class Entry
    {
        public Entry(ManagerId id, ManagerKind kind, string displayName)
        {
            Id = id;
            Kind = kind;
            DisplayName = displayName;
        }

        public ManagerId Id { get; }

        public ManagerKind Kind { get; }

        public string DisplayName { get; set; }

        public bool IsReady { get; set; }

        public BlockingItem? BlockingItem { get; set; }

        public ManagerSnapshot Snapshot() => new(Id, Kind, DisplayName, IsReady, BlockingItem);
    }
}
