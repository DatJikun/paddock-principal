namespace Paddock.Application.Inbox;

/// <summary>
/// Which inbox items are worth stopping the clock or raising a toast for. A pure rule over the item's kind and whether it asks
/// for an answer, so the page never guesses it from the text. Anything that asks for a decision is important; of the plain
/// notices only those that change the player's situation are (an answer to an offer, a finished build, a stand-in, the board,
/// a contract). Routine notices are not.
/// </summary>
public static class InboxImportance
{
    private static readonly HashSet<string> Kinds = new(StringComparer.Ordinal)
    {
        "board.notice",
        "contract.notice",
        "supply.notice",
        "negotiation.response",
        "market.agreed",
        "infrastructure.buildFinished",
        "racing.standin",
    };

    public static bool IsImportant(string kind, bool needsDecision)
    {
        ArgumentNullException.ThrowIfNull(kind);
        return needsDecision || Kinds.Contains(kind);
    }
}
