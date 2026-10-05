using Paddock.Application.Localization;

namespace Paddock.Application.Racing;

public static class StandInKeys
{
    [TranslationKey]
    public const string Subject = "inbox.racing.standin.subject";

    [TranslationKey]
    public const string OptionCandidateLabel = "inbox.racing.standin.option";

    [TranslationKey]
    public const string OptionCandidateConsequence = "inbox.racing.standin.consequence";

    [TranslationKey]
    public const string OptionReserveConsequence = "inbox.racing.standin.consequence.reserve";

    [TranslationKey]
    public const string OptionFreeAgentConsequence = "inbox.racing.standin.consequence.freeAgent";

    [TranslationKey]
    public const string OptionSkipLabel = "inbox.racing.standin.skip";

    [TranslationKey]
    public const string OptionSkipConsequence = "inbox.racing.standin.skip.consequence";
}
