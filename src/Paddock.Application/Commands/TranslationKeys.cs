using Paddock.Application.Localization;

namespace Paddock.Application.Commands;

/// <summary>
/// Player-facing reason keys. Copy lives in <c>strings/pl.json</c> and <c>strings/en.json</c>. Every const is marked
/// <c>[TranslationKey]</c> so <c>i18n-check</c> requires it in both files.
/// </summary>
public static class TranslationKeys
{
    [TranslationKey]
    public const string ManagerUnknown = "command.managerUnknown";

    [TranslationKey]
    public const string UnknownCommand = "command.unknown";

    [TranslationKey]
    public const string HumansNotReady = "ready.humansNotReady";

    [TranslationKey]
    public const string BlockingItem = "ready.blockingItem";

    [TranslationKey]
    public const string WaitingFor = "ready.waitingFor";
}
