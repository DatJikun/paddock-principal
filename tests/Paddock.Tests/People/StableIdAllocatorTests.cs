using Paddock.Domain.People;

namespace Paddock.Tests.People;

public class StableIdAllocatorTests
{
    [Fact]
    public void IdsIncreaseAndAreNeverReused()
    {
        var allocator = new StableIdAllocator();
        StablePersonId first = allocator.Allocate();
        StablePersonId second = allocator.Allocate();

        Assert.Equal("gen:1", first.Value);
        Assert.Equal("gen:2", second.Value);
        Assert.NotEqual(first, second);

        var continued = new StableIdAllocator(allocator.NextSequence);
        Assert.Equal("gen:3", continued.Allocate().Value);
        Assert.True(continued.Allocate().Sequence > second.Sequence);
    }

    [Fact]
    public void TheCounterCannotStartBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StableIdAllocator(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StablePersonId(0));
    }
}
