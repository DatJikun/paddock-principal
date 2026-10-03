using Paddock.Domain.Time;

namespace Paddock.Persistence;

/// <summary>
/// A save was requested away from a day boundary (INV-007). The world's date and the boundary the host
/// named differ, so the state may be in the middle of a day. Nothing was written.
/// </summary>
public sealed class UnstableSaveException : InvalidOperationException
{
    public UnstableSaveException(GameDate boundary, GameDate worldDate)
        : base($"Cannot save at {boundary}: the world is at {worldDate}. A save is only taken on a day boundary.")
    {
        Boundary = boundary;
        WorldDate = worldDate;
    }

    public GameDate Boundary { get; }

    public GameDate WorldDate { get; }
}
