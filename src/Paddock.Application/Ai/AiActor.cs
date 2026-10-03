using Paddock.Application.Access;

namespace Paddock.Application.Ai;

/// <summary>
/// Placeholder entry point for AI managers (real decisions are a later task). It receives only an
/// <see cref="IKnowledgeView"/>; see that interface for the truth-vs-knowledge pattern.
/// </summary>
public sealed class AiActor
{
    public AiActor(ManagerId id)
    {
        Id = id;
    }

    public ManagerId Id { get; }

    public Known<double> Perceive(IKnowledgeView view, FactKey fact)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view.Get(fact);
    }
}
