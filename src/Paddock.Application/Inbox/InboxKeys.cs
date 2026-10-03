using Paddock.Application.Localization;
using Paddock.Domain.Inbox;

namespace Paddock.Application.Inbox;

/// <summary>
/// Player-facing keys of the inbox. Copy lives in <c>strings/pl.json</c> and <c>strings/en.json</c>.
/// The kinds of items owning systems post bring their own keys.
/// </summary>
public static class InboxKeys
{
    [TranslationKey]
    public const string ItemUnknown = "inbox.error.itemUnknown";

    [TranslationKey]
    public const string ItemClosed = "inbox.error.itemClosed";

    [TranslationKey]
    public const string NotADecision = "inbox.error.notADecision";

    [TranslationKey]
    public const string OptionUnknown = "inbox.error.optionUnknown";

    [TranslationKey]
    public const string DecisionCannotBeDismissed = "inbox.error.decisionCannotBeDismissed";

    [TranslationKey]
    public const string NotYetLapsed = "inbox.error.notYetLapsed";

    [TranslationKey]
    public const string StatusOpen = "inbox.status.open";

    [TranslationKey]
    public const string StatusResolved = "inbox.status.resolved";

    [TranslationKey]
    public const string StatusDismissed = "inbox.status.dismissed";

    [TranslationKey]
    public const string StatusExpired = "inbox.status.expired";

    /// <summary>The key that names an item status in the UI.</summary>
    public static string StatusKey(InboxStatus status) => status switch
    {
        InboxStatus.Open => StatusOpen,
        InboxStatus.Resolved => StatusResolved,
        InboxStatus.Dismissed => StatusDismissed,
        InboxStatus.Expired => StatusExpired,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown status."),
    };
}
