using Paddock.Application.Commands;
using Paddock.Application.Managers;

namespace Paddock.Tests.Commands;

public class ReadyGateTests
{
    [Fact]
    public void ZeroHumansNeverBlock()
    {
        var (managers, world, _) = CommandLab.Open((CommandLab.Bot, ManagerKind.Ai, "Bot"));
        managers.SetReady(CommandLab.Bot, true);
        managers.PostBlockingItem(CommandLab.Bot, new BlockingItem("negotiation"));
        var beforeDate = world.CurrentDate;

        var result = new ReadyGate().RequestAdvance(managers, world);

        Assert.IsType<AdvanceResult.Advanced>(result);
        Assert.Equal(beforeDate.AddDays(1), world.CurrentDate);
        Assert.False(managers.Get(CommandLab.Bot).IsReady);
        Assert.Equal("negotiation", managers.Get(CommandLab.Bot).BlockingItem?.Kind);
    }

    [Fact]
    public void OneHumanNotReadyRefusesAndLeavesTheDate()
    {
        var (managers, world, _) = CommandLab.Open((CommandLab.Anna, ManagerKind.Human, "Anna"));
        var before = world.ContentHash();

        var result = new ReadyGate().RequestAdvance(managers, world);

        var refused = Assert.IsType<AdvanceResult.Refused>(result);
        Assert.Equal(AdvanceRefusalKind.HumansNotReady, refused.Refusal.Kind);
        Assert.Equal(TranslationKeys.HumansNotReady, refused.Refusal.Reason.Key);
        var waiting = Assert.Single(refused.Refusal.WaitingFor);
        Assert.Equal(WaitingCause.NotReady, waiting.Cause);
        Assert.Equal("Anna", waiting.Reason.Parameters["manager"]);
        Assert.Equal(before, world.ContentHash());
        Assert.Equal(CommandLab.OpeningDay, world.CurrentDate);
    }

    [Fact]
    public void OneReadyHumanAdvancesAndResetsReadiness()
    {
        var (managers, world, _) = CommandLab.Open((CommandLab.Anna, ManagerKind.Human, "Anna"));
        managers.SetReady(CommandLab.Anna, true);

        var result = new ReadyGate().RequestAdvance(managers, world);

        Assert.IsType<AdvanceResult.Advanced>(result);
        Assert.Equal(CommandLab.OpeningDay.AddDays(1), world.CurrentDate);
        Assert.False(managers.Get(CommandLab.Anna).IsReady);
    }

    [Fact]
    public void OneHumanBlockingItemHoldsTheDayUntilCleared()
    {
        var (managers, world, _) = CommandLab.Open((CommandLab.Anna, ManagerKind.Human, "Anna"));
        managers.SetReady(CommandLab.Anna, true);
        managers.PostBlockingItem(CommandLab.Anna, new BlockingItem("negotiation"));

        var held = new ReadyGate().RequestAdvance(managers, world);

        var refused = Assert.IsType<AdvanceResult.Refused>(held);
        Assert.Equal(AdvanceRefusalKind.BlockingItem, refused.Refusal.Kind);
        Assert.Equal(TranslationKeys.BlockingItem, refused.Refusal.Reason.Key);
        var waiting = Assert.Single(refused.Refusal.WaitingFor);
        Assert.Equal(WaitingCause.BlockingItem, waiting.Cause);
        Assert.Equal(TranslationKeys.WaitingFor, waiting.Reason.Key);
        Assert.Equal("Anna", waiting.Reason.Parameters["manager"]);
        Assert.Equal(CommandLab.OpeningDay, world.CurrentDate);

        managers.ClearBlockingItem(CommandLab.Anna);
        var released = new ReadyGate().RequestAdvance(managers, world);

        Assert.IsType<AdvanceResult.Advanced>(released);
        Assert.Equal(CommandLab.OpeningDay.AddDays(1), world.CurrentDate);
        Assert.False(managers.Get(CommandLab.Anna).IsReady);
    }

    [Fact]
    public void TwoHumansAdvanceOnlyWhenBothAreReady()
    {
        var (managers, world, _) = CommandLab.Open(
            (CommandLab.Anna, ManagerKind.Human, "Anna"),
            (CommandLab.Bram, ManagerKind.Human, "Bram"));
        managers.SetReady(CommandLab.Anna, true);

        var waiting = new ReadyGate().RequestAdvance(managers, world);

        var refused = Assert.IsType<AdvanceResult.Refused>(waiting);
        Assert.Equal(AdvanceRefusalKind.HumansNotReady, refused.Refusal.Kind);
        var heldFor = Assert.Single(refused.Refusal.WaitingFor);
        Assert.Equal(CommandLab.Bram, heldFor.ManagerId);
        Assert.Equal("Bram", heldFor.Reason.Parameters["manager"]);
        Assert.Equal(CommandLab.OpeningDay, world.CurrentDate);

        managers.SetReady(CommandLab.Bram, true);
        var advanced = new ReadyGate().RequestAdvance(managers, world);

        Assert.IsType<AdvanceResult.Advanced>(advanced);
        Assert.Equal(CommandLab.OpeningDay.AddDays(1), world.CurrentDate);
        Assert.False(managers.Get(CommandLab.Anna).IsReady);
        Assert.False(managers.Get(CommandLab.Bram).IsReady);
    }

    [Fact]
    public void AiReadinessDoesNotHoldAReadyHuman()
    {
        var (managers, world, _) = CommandLab.Open(
            (CommandLab.Anna, ManagerKind.Human, "Anna"),
            (CommandLab.Bot, ManagerKind.Ai, "Bot"));
        managers.SetReady(CommandLab.Anna, true);

        var result = new ReadyGate().RequestAdvance(managers, world);

        Assert.IsType<AdvanceResult.Advanced>(result);
        Assert.False(managers.Get(CommandLab.Bot).IsReady);
    }

    [Fact]
    public void AiBlockingItemHoldsTimeWhenAHumanIsPresent()
    {
        var (managers, world, _) = CommandLab.Open(
            (CommandLab.Anna, ManagerKind.Human, "Anna"),
            (CommandLab.Bot, ManagerKind.Ai, "Bot"));
        managers.SetReady(CommandLab.Anna, true);
        managers.PostBlockingItem(CommandLab.Bot, new BlockingItem("negotiation"));

        var result = new ReadyGate().RequestAdvance(managers, world);

        var refused = Assert.IsType<AdvanceResult.Refused>(result);
        Assert.Equal(AdvanceRefusalKind.BlockingItem, refused.Refusal.Kind);
        var waiting = Assert.Single(refused.Refusal.WaitingFor);
        Assert.Equal(CommandLab.Bot, waiting.ManagerId);
        Assert.Equal("Bot", waiting.Reason.Parameters["manager"]);
        Assert.Equal(CommandLab.OpeningDay, world.CurrentDate);
    }

    [Fact]
    public void BlockingAndNotReadyAreBothListed()
    {
        var (managers, world, _) = CommandLab.Open(
            (CommandLab.Anna, ManagerKind.Human, "Anna"),
            (CommandLab.Bram, ManagerKind.Human, "Bram"));
        managers.PostBlockingItem(CommandLab.Bram, new BlockingItem("negotiation"));

        var result = new ReadyGate().RequestAdvance(managers, world);

        var refused = Assert.IsType<AdvanceResult.Refused>(result);
        Assert.Equal(AdvanceRefusalKind.BlockingItem, refused.Refusal.Kind);
        Assert.Equal(2, refused.Refusal.WaitingFor.Count);
        Assert.Equal(CommandLab.Anna, refused.Refusal.WaitingFor[0].ManagerId);
        Assert.Equal(WaitingCause.NotReady, refused.Refusal.WaitingFor[0].Cause);
        Assert.Equal(CommandLab.Bram, refused.Refusal.WaitingFor[1].ManagerId);
        Assert.Equal(WaitingCause.BlockingItem, refused.Refusal.WaitingFor[1].Cause);
        Assert.Equal(CommandLab.OpeningDay, world.CurrentDate);
    }
}
