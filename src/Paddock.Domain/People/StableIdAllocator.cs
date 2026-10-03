namespace Paddock.Domain.People;

/// <summary>
/// Hands out generated person ids in order and never reuses one (INV-009).
/// There is no free list. Share one allocator across drivers and staff so their
/// ids stay in a single sequence. The next sequence is the only coupling between
/// successive people.
/// </summary>
public sealed class StableIdAllocator
{
    private long _next;

    public StableIdAllocator(long nextSequence = 1)
    {
        if (nextSequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(nextSequence), nextSequence, "The next id must be at least 1.");
        }

        _next = nextSequence;
    }

    public long NextSequence => _next;

    public StablePersonId Allocate()
    {
        if (_next == long.MaxValue)
        {
            throw new InvalidOperationException("The stable id counter is exhausted.");
        }

        var id = new StablePersonId(_next);
        _next++;
        return id;
    }
}
