using Paddock.Application.Localization;

namespace Paddock.Application.Regulation;

/// <summary>
/// Player-facing keys of regulation voting v2 (#275). Copy lives in <c>strings/pl.json</c> and <c>strings/en.json</c>. The reasons of the
/// simulation layer (<c>vote.reason.*</c>, <c>regulation.fia.reason.*</c>, <c>regulation.result.*</c>, <c>regulation.ai.reason.*</c>) are
/// constants of the types that produce them and are covered by the same strings.
/// </summary>
public static class RegulationKeys
{
    [TranslationKey]
    public const string NotVoted = "regulation.error.notVoted";

    [TranslationKey]
    public const string UnknownSeries = "regulation.error.unknownSeries";

    [TranslationKey]
    public const string UnknownTeam = "regulation.error.unknownTeam";

    [TranslationKey]
    public const string NoControl = "regulation.error.notInControl";

    [TranslationKey]
    public const string WindowClosed = "regulation.error.windowClosed";

    [TranslationKey]
    public const string Cooldown = "regulation.error.cooldown";

    [TranslationKey]
    public const string NotLive = "regulation.error.notLive";

    [TranslationKey]
    public const string ValueNotAllowed = "regulation.error.valueNotAllowed";

    [TranslationKey]
    public const string SameValue = "regulation.error.sameValue";

    [TranslationKey]
    public const string NoEffect = "regulation.error.noEffect";

    [TranslationKey]
    public const string RecentlyRejected = "regulation.error.recentlyRejected";

    [TranslationKey]
    public const string NoBooks = "regulation.error.noBooks";

    [TranslationKey]
    public const string AlreadyProposed = "regulation.error.alreadyProposed";

    [TranslationKey]
    public const string TooFewRounds = "regulation.error.tooFewRounds";

    [TranslationKey]
    public const string UnknownItem = "regulation.error.unknownItem";

    [TranslationKey]
    public const string ItemClosed = "regulation.error.itemClosed";

    [TranslationKey]
    public const string UnknownOption = "regulation.error.unknownOption";

    [TranslationKey]
    public const string SpendNeedsBank = "regulation.error.spendNeedsBank";

    [TranslationKey]
    public const string SpendOverBank = "regulation.error.spendOverBank";

    [TranslationKey]
    public const string SpendOnAbstain = "regulation.error.spendOnAbstain";

    [TranslationKey]
    public const string Banned = "regulation.error.banned";

    [TranslationKey]
    public const string LedgerProposalFee = "regulation.ledger.proposalFee";

    [TranslationKey]
    public const string ReasonTeamProposal = "regulation.reason.teamProposal";

    [TranslationKey]
    public const string ResultCalendarTooShort = "regulation.result.calendarTooShort";

    [TranslationKey]
    public const string ResultInvalid = "regulation.result.invalid";

    [TranslationKey]
    public const string ResultBanned = "regulation.result.banned";

    public const string AnnouncedKind = "regulation.announced";

    [TranslationKey]
    public const string AnnouncedSubject = "regulation.inbox.announced.subject";

    public const string ResultKind = "regulation.result";

    [TranslationKey]
    public const string ResultSubject = "regulation.inbox.result.subject";

    /// <summary>The key of the plain-words effect of a live rule in the race.</summary>
    public static string EffectKey(string dimensionId) => "regulation.effect." + dimensionId;

    /// <summary>The key of the name of a rule dimension. Calendar dimensions share one key and carry the circuit as an argument.</summary>
    public static string DimensionKey(string dimensionId) =>
        Paddock.Simulation.Regulation.CalendarPolicy.IsCalendarDimension(dimensionId)
            ? "regulation.dimension.calendar"
            : "regulation.dimension." + dimensionId;

    /// <summary>The key of the label of one value of a rule. A calendar value is one of four shapes and carries the layout as an argument.</summary>
    public static string ValueKey(string dimensionId, string value)
    {
        if (!Paddock.Simulation.Regulation.CalendarPolicy.IsCalendarDimension(dimensionId))
        {
            return "regulation.value." + dimensionId + "." + value;
        }

        if (value.StartsWith(Paddock.Simulation.Regulation.CalendarPolicy.LayoutPrefix, StringComparison.Ordinal))
        {
            return "regulation.value.calendar.layout";
        }

        return value.StartsWith(Paddock.Simulation.Regulation.CalendarPolicy.AddedPrefix, StringComparison.Ordinal)
            ? "regulation.value.calendar.added"
            : "regulation.value.calendar." + value;
    }
}
