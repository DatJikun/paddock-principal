using Paddock.Application.Commands;
using Paddock.Domain.Inbox;

namespace Paddock.Application.Inbox;

/// <summary>
/// The owning system of one kind of inbox item. When a manager answers a decision (or its default option applies
/// at expiry), the resolver carries the chosen option out. It runs inside the <c>ResolveInboxItem</c> or
/// <c>ExpireInboxItem</c> command, so the change is a command's change (INV-001) and is logged and replayed with it.
/// <see cref="Execute"/> must not draw RNG outside a named stream (INV-004) and must not read truth for the reply it gives.
/// </summary>
public interface IInboxResolver
{
    /// <summary>The <see cref="InboxItem.Kind"/> this resolver owns.</summary>
    string Kind { get; }

    /// <summary>A reason the option cannot be carried out now (for example the money is gone), or null.</summary>
    TranslationMessage? Validate(InboxItem item, string optionId, CommandContext context);

    /// <summary>Carries the option out and returns the domain events that describe it.</summary>
    IReadOnlyList<IDomainEvent> Execute(InboxItem item, string optionId, CommandContext context);
}

/// <summary>The resolvers of a game, one per item kind.</summary>
public sealed class InboxResolvers
{
    private readonly Dictionary<string, IInboxResolver> _byKind = new(StringComparer.Ordinal);

    public void Register(IInboxResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentException.ThrowIfNullOrWhiteSpace(resolver.Kind);
        if (!_byKind.TryAdd(resolver.Kind, resolver))
        {
            throw new InvalidOperationException($"A resolver for kind '{resolver.Kind}' is already registered.");
        }
    }

    public IInboxResolver? Find(string kind)
    {
        ArgumentNullException.ThrowIfNull(kind);
        return _byKind.GetValueOrDefault(kind);
    }
}
