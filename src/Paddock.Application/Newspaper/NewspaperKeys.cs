using Paddock.Application.Localization;

namespace Paddock.Application.Newspaper;

/// <summary>
/// Every translation key of the newspaper on the main screen (#324). The name of the paper is one key, so renaming it
/// touches the two catalogs only. No headline is built from loose words: each one is a key with parameters, and a count
/// that has plural forms carries it as the <c>count</c> parameter.
/// </summary>
public static class NewspaperKeys
{
    /// <summary>The name of the paper. A placeholder; it appears nowhere else.</summary>
    [TranslationKey]
    public const string Name = "paper.name";

    [TranslationKey]
    public const string Empty = "paper.empty";

    [TranslationKey]
    public const string KindRace = "paper.kind.race";

    [TranslationKey]
    public const string KindOwn = "paper.kind.own";

    [TranslationKey]
    public const string KindInjury = "paper.kind.injury";

    [TranslationKey]
    public const string KindRetirement = "paper.kind.retirement";

    [TranslationKey]
    public const string KindSigning = "paper.kind.signing";

    [TranslationKey]
    public const string RaceWin = "paper.race.win";

    [TranslationKey]
    public const string RacePodium = "paper.race.podium";

    [TranslationKey]
    public const string RaceOwn = "paper.race.own";

    [TranslationKey]
    public const string RaceOwnOut = "paper.race.ownOut";

    /// <summary>Plural: the points the player's team scored in the race.</summary>
    [TranslationKey]
    public const string RaceOwnPoints = "paper.race.ownPoints";

    [TranslationKey]
    public const string LineTeam = "paper.line.team";

    [TranslationKey]
    public const string InjuryLight = "paper.injury.light";

    [TranslationKey]
    public const string InjurySerious = "paper.injury.serious";

    [TranslationKey]
    public const string InjuryCareerEnding = "paper.injury.careerEnding";

    [TranslationKey]
    public const string InjuryFatal = "paper.injury.fatal";

    [TranslationKey]
    public const string Retired = "paper.retired";

    [TranslationKey]
    public const string RetiredAge = "paper.retired.age";

    [TranslationKey]
    public const string SigningDriver = "paper.signing.driver";

    [TranslationKey]
    public const string SigningFrom = "paper.signing.from";

    [TranslationKey]
    public const string SigningRenewed = "paper.signing.renewed";

    [TranslationKey]
    public const string TeamHired = "paper.team.hired";

    [TranslationKey]
    public const string RulesAdopted = "paper.rules.adopted";

    [TranslationKey]
    public const string RulesKept = "paper.rules.kept";
}
