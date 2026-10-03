using System.Reflection;
using Paddock.Application.Managers;
using Paddock.Application.Queries;
using Paddock.Application.World;

namespace Paddock.Tests.Commands;

public class ManagerViewTests
{
    [Fact]
    public void ManagerViewCannotBeConstructedWithoutAManagerId()
    {
        var constructors = typeof(ManagerView).GetConstructors();
        var only = Assert.Single(constructors);
        var parameter = Assert.Single(only.GetParameters());
        Assert.Equal(typeof(ManagerId), parameter.ParameterType);
        Assert.Null(typeof(ManagerView).GetConstructor(Type.EmptyTypes));
        Assert.Throws<ArgumentException>(() => new ManagerView(default));
        Assert.Throws<ArgumentException>(() => new StubWorldQuery().ViewFor(default));
    }

    [Fact]
    public void ViewForReturnsAnEmptyViewScopedToThatManager()
    {
        var properties = typeof(ManagerView).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var only = Assert.Single(properties);
        Assert.Equal(nameof(ManagerView.ManagerId), only.Name);

        var query = new StubWorldQuery();
        IWorldQuery scoped = query;
        var anna = scoped.ViewFor(CommandLab.Anna);
        var bram = scoped.ViewFor(CommandLab.Bram);

        Assert.Equal(CommandLab.Anna, anna.ManagerId);
        Assert.Equal(CommandLab.Bram, bram.ManagerId);
        Assert.NotEqual(anna.ManagerId, bram.ManagerId);
    }

    [Fact]
    public void ViewForDoesNotChangeWorldState()
    {
        var world = new StubWorldState(CommandLab.OpeningDay);
        world.SetManagerName(CommandLab.Anna.Value, "Anna");
        var before = world.ContentHash();

        _ = new StubWorldQuery().ViewFor(CommandLab.Anna);

        Assert.Equal(before, world.ContentHash());
        Assert.Equal(CommandLab.OpeningDay, world.CurrentDate);
    }
}
