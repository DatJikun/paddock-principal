namespace Paddock.Application.Access;

public enum AccessKind
{
    Developer,
    Manager,
    Ai,
}

/// <summary>
/// Who is asking (INV-003). Developer sees the simulation truth; a manager (the player) and an AI
/// manager see only what the organisation knows.
/// </summary>
public sealed record AccessContext
{
    private AccessContext(AccessKind kind, ManagerId? manager)
    {
        Kind = kind;
        Manager = manager;
    }

    public AccessKind Kind { get; }

    /// <summary>Set for <see cref="AccessKind.Manager"/> and <see cref="AccessKind.Ai"/>, null for the developer.</summary>
    public ManagerId? Manager { get; }

    public static AccessContext Developer { get; } = new(AccessKind.Developer, null);

    public static AccessContext ForManager(ManagerId managerId) => new(AccessKind.Manager, managerId);

    public static AccessContext ForAi(ManagerId managerId) => new(AccessKind.Ai, managerId);
}
