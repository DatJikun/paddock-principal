namespace Paddock.Application.Commands;

/// <summary>
/// Player-facing reason keys. Copy lives in <c>strings/pl.json</c> and <c>strings/en.json</c>.
/// </summary>
public static class TranslationKeys
{
    public const string ManagerUnknown = "command.managerUnknown";

    public const string UnknownCommand = "command.unknown";

    public const string HumansNotReady = "ready.humansNotReady";

    public const string BlockingItem = "ready.blockingItem";

    public const string WaitingFor = "ready.waitingFor";
}
