using Paddock.Application.Commands;

namespace Paddock.Application.Managers;

public enum AdvanceRefusalKind
{
    HumansNotReady,
    BlockingItem,
}

public enum WaitingCause
{
    NotReady,
    BlockingItem,
}

public sealed record WaitingForManager(
    ManagerId ManagerId,
    string DisplayName,
    WaitingCause Cause,
    TranslationMessage Reason);

public sealed record AdvanceRefusal(
    AdvanceRefusalKind Kind,
    TranslationMessage Reason,
    IReadOnlyList<WaitingForManager> WaitingFor);

public abstract record AdvanceResult
{
    private AdvanceResult()
    {
    }

    public sealed record Advanced : AdvanceResult;

    public sealed record Refused(AdvanceRefusal Refusal) : AdvanceResult;
}
