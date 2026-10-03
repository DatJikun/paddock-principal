namespace Paddock.Application.Access;

/// <summary>Names one piece of information a knowledge provider may or may not reveal, e.g. "car.123.engine.power".</summary>
public readonly record struct FactKey
{
    public FactKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

/// <summary>
/// A source of organisational knowledge (scouting, telemetry, reports). Later systems register one
/// each; they decide what a given manager has learned. Must be a pure query (INV-005).
/// </summary>
public interface IKnowledgeProvider
{
    bool Knows(ManagerId manager, FactKey fact);
}

public interface IVisibilityPolicy
{
    bool CanSee(AccessContext context, FactKey fact);
}

/// <summary>Developer sees everything; managers see only facts some registered provider reveals to them.</summary>
public sealed class DefaultVisibilityPolicy : IVisibilityPolicy
{
    private readonly IReadOnlyList<IKnowledgeProvider> _providers;

    public DefaultVisibilityPolicy(IEnumerable<IKnowledgeProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers.ToArray();
    }

    public bool CanSee(AccessContext context, FactKey fact)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Kind == AccessKind.Developer)
        {
            return true;
        }

        var manager = context.Manager ?? throw new InvalidOperationException("Non-developer context without a manager.");
        foreach (var provider in _providers)
        {
            if (provider.Knows(manager, fact))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Marks a type as simulation truth: exact values the organisation may not know. Code in
/// <c>Paddock.Application.Ai</c> must not reference such types; an architecture test enforces it.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum, Inherited = false)]
public sealed class SimulationTruthAttribute : Attribute
{
}

/// <summary>
/// The only window onto the world that AI code (<c>Paddock.Application.Ai</c>) receives (INV-003).
///
/// Pattern: truth lives in simulation types (mark them <see cref="SimulationTruthAttribute"/>);
/// the Application layer builds a view for one <see cref="AccessContext"/> from the truth plus the
/// registered <see cref="IKnowledgeProvider"/>s and hands only that view to AI code. AI code
/// signatures take <see cref="IKnowledgeView"/>, never truth types, so it cannot call truth getters.
/// The architecture test <c>AiNamespaceDoesNotReferenceTruthTypes</c> fails if the Ai namespace
/// uses Paddock.Domain, Paddock.Simulation or any [SimulationTruth] type.
/// A view must be a pure query: no state change, no RNG (INV-005).
/// </summary>
public interface IKnowledgeView
{
    AccessContext Viewer { get; }

    Known<double> Get(FactKey fact);
}
