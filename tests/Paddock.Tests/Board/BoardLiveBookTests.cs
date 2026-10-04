using Paddock.Domain.Board;
using Paddock.Domain.Objectives;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using static Paddock.Tests.Board.BoardKit;

namespace Paddock.Tests.Board;

/// <summary>
/// #160: when the contract book is bound to the career session's world the board keeps its sections in that world, not in a copy,
/// so the board, the sponsors and the finance ledger never hold two versions of the objectives. Fixtures are SYNTHETIC.
/// </summary>
public sealed class BoardLiveBookTests
{
    [Fact]
    public void ABoardOnABoundBookReadsAndWritesTheHostsWorld()
    {
        var lab = new Lab(11UL, free: 3);
        var host = lab.Contracts.World;
        lab.Contracts.Bind(() => host, next => host = next);

        lab.Appoint(Pam, T3);

        var inWorld = host.Section<BoardSection>(BoardSection.SectionName)!;
        Assert.Equal(Pam.Value, inWorld.Board(T3)!.Principal!.Subject);
        Assert.Same(inWorld, lab.Board.Section);
        Assert.Same(host, lab.Board.Into(host));
    }

    [Fact]
    public void ObjectivesOnABoundBookAreTheOnesInTheHostsWorldSoAnotherSystemsChangeIsNotLost()
    {
        var lab = new Lab(11UL, free: 3);
        var host = lab.Contracts.World;
        lab.Contracts.Bind(() => host, next => host = next);
        lab.Engine.EnsureBoards(new GameDate(1955, 1, 1));
        lab.Engine.GrantObjectives(new GameDate(1955, 1, 1));

        var granted = host.Section<ObjectivesSection>(ObjectivesSection.SectionName)!;
        var first = granted.OwnedBy(T3)[0];
        Assert.True(first.IsOpen);

        // Another system settles it straight in the world; the board must see that, not its own older copy.
        host = host.WithSection(granted.Settle(first.Id, met: true, new GameDate(1955, 6, 1)));

        Assert.False(lab.Board.Objectives.Find(first.Id)!.IsOpen);
    }
}
