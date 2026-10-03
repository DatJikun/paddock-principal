using Paddock.Application.Managers;

namespace Paddock.Tests.Commands;

public class ManagerRegistryTests
{
    [Fact]
    public void TheSameManagerIdCannotBeIssuedTwice()
    {
        var registry = new ManagerRegistry();
        registry.Register(CommandLab.Anna, ManagerKind.Human, "Anna");

        var error = Assert.Throws<InvalidOperationException>(
            () => registry.Register(CommandLab.Anna, ManagerKind.Ai, "Other"));
        Assert.Contains("cannot be reused", error.Message, StringComparison.Ordinal);
        Assert.Equal(ManagerKind.Human, registry.KindOf(CommandLab.Anna));
        Assert.Equal("Anna", registry.Get(CommandLab.Anna).DisplayName);
    }

    [Fact]
    public void RenameRejectsABlankName()
    {
        var registry = new ManagerRegistry();
        registry.Register(CommandLab.Anna, ManagerKind.Human, "Anna");

        Assert.Throws<ArgumentException>(() => registry.Rename(CommandLab.Anna, "  "));
        Assert.Equal("Anna", registry.Get(CommandLab.Anna).DisplayName);
    }

    [Fact]
    public void RegisterRejectsAnUnassignedId()
    {
        var registry = new ManagerRegistry();
        Assert.Throws<ArgumentException>(() => registry.Register(default, ManagerKind.Human, "Anna"));
    }
}
