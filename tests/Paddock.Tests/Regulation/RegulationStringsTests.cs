using System.Reflection;
using Paddock.Application.Localization;
using Paddock.Application.Regulation;
using Paddock.Domain.Career;
using Paddock.Simulation.Regulation;

namespace Paddock.Tests.Regulation;

/// <summary>Every text of voting v2 exists in Polish and English (PP-021), including the ones built from a rule and a value.</summary>
public class RegulationStringsTests
{
    private static readonly TranslationCatalog Catalog = TranslationLoader.LoadDirectory(Path.Combine(RepoPaths.Root(), "strings"));

    private static void AssertBoth(IEnumerable<string> keys)
    {
        foreach (var key in keys.Distinct(StringComparer.Ordinal))
        {
            Assert.True(Catalog.Entries(Language.Pl).ContainsKey(key), key + " is missing in pl.json");
            Assert.True(Catalog.Entries(Language.En).ContainsKey(key), key + " is missing in en.json");
        }
    }

    private static IEnumerable<string> ConstKeys(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Where(value => value.Contains('.', StringComparison.Ordinal));

    [Fact]
    public void TheReasonsTheSimulationLayerReturnsAreTranslated()
    {
        AssertBoth(ConstKeys(typeof(VoteInterest)));
        AssertBoth(ConstKeys(typeof(FiaAgenda)));
        AssertBoth(ConstKeys(typeof(BallotTally)));
        AssertBoth(Enum.GetValues<Paddock.Domain.Racing.PoliticalLeaning>().Select(VoteInterest.ReasonLeaning));
    }

    [Fact]
    public void EveryLiveRuleHasANameAnEffectAndALabelForEveryValue()
    {
        foreach (var rule in LiveRules.All)
        {
            AssertBoth([RegulationKeys.DimensionKey(rule.DimensionId), RegulationKeys.EffectKey(rule.DimensionId), rule.EffectKey]);
            AssertBoth(rule.Values.Select(value => RegulationKeys.ValueKey(rule.DimensionId, value)));
        }
    }

    [Fact]
    public void TheCalendarHasANameAndALabelForEveryShapeOfValue()
    {
        var dimension = CalendarPolicy.DimensionOf("monza");

        AssertBoth([RegulationKeys.DimensionKey(dimension)]);
        AssertBoth(new[]
        {
            CalendarPolicy.Kept,
            CalendarPolicy.Dropped,
            CalendarPolicy.LayoutPrefix + "monza_1955",
            CalendarPolicy.AddedPrefix + "monza_1955",
        }.Select(value => RegulationKeys.ValueKey(dimension, value)));
    }

    [Fact]
    public void TheConfigKeysOfTheVoteModeAreTranslated()
    {
        AssertBoth([CareerConfigCodes.VoteMode, CareerConfigCodes.VoteModeWithHistoricalRules]);
    }

    [Fact]
    public void TheShippedFilesStillPassEveryCheck() =>
        Assert.Empty(I18nChecker.CheckRepository(RepoPaths.Root()));
}
