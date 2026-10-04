using Paddock.Domain.Codec;

namespace Paddock.Application.Managers;

/// <summary>
/// Save form of the manager registry. The kind is a fixed word (<c>Human</c>, <c>Ai</c>) written here, not the name of
/// the enum member, so renaming the enum cannot change a save, and an unknown word fails with <see cref="UnknownTagException"/>.
/// Readiness is not saved: it resets every day and a save is only taken at a day boundary (INV-007).
/// A blocking item keeps its kind code.
/// </summary>
public static class ManagerCodec
{
    public const string HumanTag = "Human";

    public const string AiTag = "Ai";

    private const string Kind = "manager kind";

    public static string EncodeKind(ManagerKind kind) => kind switch
    {
        ManagerKind.Human => HumanTag,
        ManagerKind.Ai => AiTag,
        _ => throw new InvalidOperationException("No save codec for manager kind '" + kind + "'."),
    };

    public static ManagerKind DecodeKind(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return tag switch
        {
            HumanTag => ManagerKind.Human,
            AiTag => ManagerKind.Ai,
            _ => throw new UnknownTagException(Kind, tag),
        };
    }

    /// <summary>
    /// Rebuilds a registry from saved rows in their saved order. Everyone starts not ready, as after any day change.
    /// </summary>
    public static ManagerRegistry Restore(IEnumerable<(string Id, string Kind, string DisplayName, string? BlockingKind)> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var registry = new ManagerRegistry();
        foreach (var (id, kind, displayName, blockingKind) in rows)
        {
            var manager = new ManagerId(id);
            registry.Register(manager, DecodeKind(kind), displayName);
            if (blockingKind is not null)
            {
                registry.PostBlockingItem(manager, new BlockingItem(blockingKind));
            }
        }

        return registry;
    }
}
