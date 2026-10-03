namespace Paddock.Application.Managers;

public sealed record ManagerSnapshot(
    ManagerId Id,
    ManagerKind Kind,
    string DisplayName,
    bool IsReady,
    BlockingItem? BlockingItem);
