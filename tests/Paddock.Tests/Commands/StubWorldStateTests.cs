using Paddock.Application.World;

namespace Paddock.Tests.Commands;

public class StubWorldStateTests
{
    [Fact]
    public void ContentHashIgnoresInsertionOrderAndChangesWithContent()
    {
        var left = new StubWorldState(new DateOnly(1950, 1, 1));
        left.SetManagerName("b", "Bram");
        left.SetManagerName("a", "Anna");
        left.SetTeamNote("zeta", "z");
        left.SetTeamNote("alfa", "a");

        var right = new StubWorldState(new DateOnly(1950, 1, 1));
        right.SetTeamNote("alfa", "a");
        right.SetTeamNote("zeta", "z");
        right.SetManagerName("a", "Anna");
        right.SetManagerName("b", "Bram");

        Assert.Equal(left.ContentHash(), right.ContentHash());

        right.SetTeamNote("alfa", "other");
        Assert.NotEqual(left.ContentHash(), right.ContentHash());
    }

    [Fact]
    public void AdvanceDateStepsAcrossALeapDay()
    {
        var world = new StubWorldState(new DateOnly(1952, 2, 28));
        world.AdvanceDate();
        Assert.Equal(new DateOnly(1952, 2, 29), world.CurrentDate);
        world.AdvanceDate();
        Assert.Equal(new DateOnly(1952, 3, 1), world.CurrentDate);
    }
}
