using Paddock.Application.Localization;

namespace Paddock.Desktop.Bridge;

/// <summary>Player-facing keys of the desktop bridge. Copy lives in <c>strings/pl.json</c> and <c>strings/en.json</c>.</summary>
public static class BridgeKeys
{
    [TranslationKey]
    public const string UnknownName = "bridge.error.unknownName";

    [TranslationKey]
    public const string BadMessage = "bridge.error.badMessage";

    [TranslationKey]
    public const string ManagerRequired = "bridge.error.managerRequired";

    [TranslationKey]
    public const string Internal = "bridge.error.internal";

    /// <summary>Reason stored when the host appoints the human at career start, until the wizard (#112) owns that command.</summary>
    [TranslationKey]
    public const string CareerAppointed = "bridge.career.appointed";

    [TranslationKey]
    public const string NoRace = "bridge.race.none";
}
