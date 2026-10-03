using Paddock.Application.Commands;
using Paddock.Application.World;

namespace Paddock.Application.Managers;

/// <summary>
/// Shared clock (PP-045). <see cref="RequestAdvance"/> moves <see cref="IWorldState.CurrentDate"/>
/// one day only when every human manager is ready and nobody has a blocking item.
/// Zero human managers is AI-only SimRunner mode and never blocks.
/// When at least one human exists, a blocking item from any manager (human or AI) holds the day.
/// Readiness of AI managers is ignored. After a day advances, every readiness flag resets.
/// Blocking items are cleared only by <see cref="ManagerRegistry.ClearBlockingItem"/>.
/// </summary>
public sealed class ReadyGate
{
    public AdvanceResult RequestAdvance(ManagerRegistry managers, IWorldState world)
    {
        ArgumentNullException.ThrowIfNull(managers);
        ArgumentNullException.ThrowIfNull(world);

        var refusal = Evaluate(managers);
        if (refusal is not null)
        {
            return new AdvanceResult.Refused(refusal);
        }

        world.AdvanceDate();
        managers.ResetReadiness();
        return new AdvanceResult.Advanced();
    }

    private static AdvanceRefusal? Evaluate(ManagerRegistry managers)
    {
        var humans = 0;
        foreach (var manager in managers.All)
        {
            if (manager.Kind == ManagerKind.Human)
            {
                humans++;
            }
        }

        if (humans == 0)
        {
            return null;
        }

        var waiting = new List<WaitingForManager>();
        foreach (var manager in managers.All.OrderBy(snapshot => snapshot.Id.Value, StringComparer.Ordinal))
        {
            if (manager.BlockingItem is not null)
            {
                waiting.Add(Describe(manager, WaitingCause.BlockingItem));
                continue;
            }

            if (manager.Kind == ManagerKind.Human && !manager.IsReady)
            {
                waiting.Add(Describe(manager, WaitingCause.NotReady));
            }
        }

        if (waiting.Count == 0)
        {
            return null;
        }

        var blocked = false;
        foreach (var entry in waiting)
        {
            if (entry.Cause == WaitingCause.BlockingItem)
            {
                blocked = true;
                break;
            }
        }

        var kind = blocked ? AdvanceRefusalKind.BlockingItem : AdvanceRefusalKind.HumansNotReady;
        var key = blocked ? TranslationKeys.BlockingItem : TranslationKeys.HumansNotReady;
        return new AdvanceRefusal(kind, TranslationMessage.Of(key), waiting);
    }

    private static WaitingForManager Describe(ManagerSnapshot manager, WaitingCause cause)
    {
        return new WaitingForManager(
            manager.Id,
            manager.DisplayName,
            cause,
            TranslationMessage.Of(TranslationKeys.WaitingFor, ("manager", manager.DisplayName)));
    }
}
