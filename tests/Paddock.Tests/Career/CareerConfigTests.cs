using System.Security.Cryptography;
using System.Text;
using Paddock.Domain.Career;
using Paddock.Persistence;

namespace Paddock.Tests.Career;

public class CareerConfigTests
{
    private const string BalancedJson =
        "{\"peopleSource\":\"RealPotential\",\"rulesSource\":\"Historical\",\"aiBehavior\":\"ReactToSituation\",\"historyStrength\":5,\"randomnessLevel\":50,\"fatalityLevel\":\"Off\",\"startYear\":1950,\"playerTeam\":\"NewTeam\",\"noNumbers\":false,\"presetName\":\"Balanced\"}";

    private const string BalancedHash = "8875A9A000291F410B04ECD503CB278C431ACCD4B5E7A27F45FE3E6BF734A645";

    [Fact]
    public void MostHistoricalPresetMatchesTheTask()
    {
        var config = CareerConfig.FromPreset(CareerPreset.MostHistorical);
        Assert.Equal(PeopleSource.RealTrajectory, config.PeopleSource);
        Assert.Equal(RulesSource.Historical, config.RulesSource);
        Assert.Equal(AiBehavior.ReplayHistory, config.AiBehavior);
        Assert.Equal(CareerConfig.MaxHistoryStrength, config.HistoryStrength);
        Assert.Equal(CareerPreset.MostHistorical, config.PresetName);
        AssertValid(config);
    }

    [Fact]
    public void BalancedPresetIsTheDefaultComposition()
    {
        var config = CareerConfig.FromPreset(CareerPreset.Balanced);
        Assert.Equal(PeopleSource.RealPotential, config.PeopleSource);
        Assert.Equal(RulesSource.Historical, config.RulesSource);
        Assert.Equal(AiBehavior.ReactToSituation, config.AiBehavior);
        Assert.Equal(CareerConfig.BalancedHistoryStrength, config.HistoryStrength);
        Assert.Equal(5, config.HistoryStrength);
        Assert.Equal(0.5, config.HistoryStrengthFraction);
        Assert.Equal(CareerPreset.Balanced, config.PresetName);
        AssertValid(config);
    }

    [Fact]
    public void ChaosPresetMatchesTheTaskDefault()
    {
        var config = CareerConfig.FromPreset(CareerPreset.Chaos);
        Assert.Equal(PeopleSource.FullyGenerated, config.PeopleSource);
        Assert.Equal(RulesSource.VotedEachSeason, config.RulesSource);
        Assert.Equal(AiBehavior.PureRandom, config.AiBehavior);
        Assert.Equal(CareerConfig.MinHistoryStrength, config.HistoryStrength);
        Assert.Equal(CareerPreset.Chaos, config.PresetName);
        AssertValid(config);
    }

    [Fact]
    public void PresetsShareTheUnspecifiedAxes()
    {
        foreach (var preset in new[] { CareerPreset.MostHistorical, CareerPreset.Balanced, CareerPreset.Chaos })
        {
            var config = CareerConfig.FromPreset(preset);
            Assert.Equal(CareerConfig.DefaultRandomnessLevel, config.RandomnessLevel);
            Assert.Equal(FatalityLevel.Off, config.FatalityLevel);
            Assert.Equal(CareerConfig.MinStartYear, config.StartYear);
            Assert.Equal(CareerConfig.NewTeam, config.PlayerTeam);
            Assert.False(config.NoNumbers);
        }
    }

    [Fact]
    public void CustomIsNotASelectablePreset()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CareerConfig.FromPreset(CareerPreset.Custom));
        Assert.Throws<ArgumentOutOfRangeException>(() => CareerConfig.FromPreset((CareerPreset)99));
    }

    [Theory]
    [MemberData(nameof(AxisEdits))]
    public void EditingAnyAxisReportsCustom(CareerConfig edited)
    {
        Assert.Equal(CareerPreset.Custom, edited.PresetName);
    }

    public static IEnumerable<object[]> AxisEdits()
    {
        var balanced = CareerConfig.FromPreset(CareerPreset.Balanced);
        yield return [balanced.WithPeopleSource(PeopleSource.RealTrajectory)];
        yield return [balanced.WithRulesSource(RulesSource.VotedEachSeason)];
        yield return [balanced.WithAiBehavior(AiBehavior.PureRandom)];
        yield return [balanced.WithHistoryStrength(4)];
        yield return [balanced.WithRandomnessLevel(49)];
        yield return [balanced.WithFatalityLevel(FatalityLevel.On)];
        yield return [balanced.WithStartYear(1951)];
        yield return [balanced.WithPlayerTeam("team-ferrari")];
        yield return [balanced.WithNoNumbers(true)];
    }

    [Fact]
    public void RestoringThePresetAxesClearsCustom()
    {
        var edited = CareerConfig.FromPreset(CareerPreset.Balanced)
            .WithPeopleSource(PeopleSource.RealTrajectory);
        Assert.Equal(CareerPreset.Custom, edited.PresetName);

        var restored = edited.WithPeopleSource(PeopleSource.RealPotential);
        Assert.Equal(CareerPreset.Balanced, restored.PresetName);
        Assert.Equal(CareerConfig.FromPreset(CareerPreset.Balanced), restored);
    }

    [Fact]
    public void AxesThatMatchAnotherPresetTakeThatName()
    {
        var config = CareerConfig.FromPreset(CareerPreset.Chaos)
            .WithPeopleSource(PeopleSource.RealPotential)
            .WithRulesSource(RulesSource.Historical)
            .WithAiBehavior(AiBehavior.ReactToSituation)
            .WithHistoryStrength(CareerConfig.BalancedHistoryStrength);
        Assert.Equal(CareerConfig.FromPreset(CareerPreset.Balanced), config);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void HistoryStrengthOutsideTheRangeIsAnError(int strength)
    {
        var validation = CareerConfig.FromPreset(CareerPreset.Balanced)
            .WithHistoryStrength(strength)
            .Validate();
        Assert.False(validation.IsValid);
        var issue = Assert.Single(validation.Errors);
        Assert.Equal(CareerConfigCodes.HistoryStrengthRange, issue.Code);
        Assert.Equal(["0", "10"], issue.Arguments);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void RandomnessOutsideTheRangeIsAnError(int randomness)
    {
        var validation = CareerConfig.FromPreset(CareerPreset.Balanced)
            .WithRandomnessLevel(randomness)
            .Validate();
        Assert.False(validation.IsValid);
        var issue = Assert.Single(validation.Errors);
        Assert.Equal(CareerConfigCodes.RandomnessRange, issue.Code);
        Assert.Equal(["0", "100"], issue.Arguments);
    }

    [Fact]
    public void StartYearBefore1950IsAnError()
    {
        var validation = CareerConfig.FromPreset(CareerPreset.Balanced)
            .WithStartYear(1949)
            .Validate();
        var issue = Assert.Single(validation.Errors);
        Assert.Equal(CareerConfigCodes.StartYearMin, issue.Code);
        Assert.Equal(["1950"], issue.Arguments);
    }

    [Fact]
    public void ReplayHistoryRequiresHistoricalRules()
    {
        var validation = CareerConfig.FromPreset(CareerPreset.MostHistorical)
            .WithRulesSource(RulesSource.VotedEachSeason)
            .Validate();
        var issue = Assert.Single(validation.Errors);
        Assert.Equal(CareerConfigCodes.ReplayNeedsHistoricalRules, issue.Code);
    }

    [Theory]
    [InlineData(PeopleSource.RealNamesRandomSkills)]
    [InlineData(PeopleSource.FullyGenerated)]
    public void ReplayHistoryRequiresRealTrajectoryOrPotential(PeopleSource people)
    {
        var validation = CareerConfig.FromPreset(CareerPreset.MostHistorical)
            .WithPeopleSource(people)
            .Validate();
        var issue = Assert.Single(validation.Errors);
        Assert.Equal(CareerConfigCodes.ReplayNeedsRealPeople, issue.Code);
    }

    [Fact]
    public void ReplayHistoryAllowsRealPotential()
    {
        var config = CareerConfig.FromPreset(CareerPreset.MostHistorical)
            .WithPeopleSource(PeopleSource.RealPotential);
        AssertValid(config);
        Assert.Equal(CareerPreset.Custom, config.PresetName);
    }

    [Fact]
    public void StartYearAfter2026RequiresFullyGeneratedPeople()
    {
        var rejected = CareerConfig.FromPreset(CareerPreset.Balanced).WithStartYear(2027).Validate();
        var issue = Assert.Single(rejected.Errors);
        Assert.Equal(CareerConfigCodes.LateStartNeedsGeneratedPeople, issue.Code);
        Assert.Equal(["2026"], issue.Arguments);

        AssertValid(CareerConfig.FromPreset(CareerPreset.Chaos).WithStartYear(2027));
        AssertValid(CareerConfig.FromPreset(CareerPreset.Balanced).WithStartYear(2026));
    }

    [Fact]
    public void PositiveHistoryStrengthWithPureRandomIsAWarning()
    {
        var warned = CareerConfig.FromPreset(CareerPreset.Chaos).WithHistoryStrength(1).Validate();
        Assert.True(warned.IsValid);
        var warning = Assert.Single(warned.Warnings);
        Assert.Equal(CareerConfigCodes.HistoryStrengthWithPureRandom, warning.Code);
        Assert.Empty(warned.Errors);

        var silent = CareerConfig.FromPreset(CareerPreset.Chaos).Validate();
        Assert.Empty(silent.Warnings);

        var outOfRange = CareerConfig.FromPreset(CareerPreset.Chaos).WithHistoryStrength(11).Validate();
        Assert.False(outOfRange.IsValid);
        Assert.Empty(outOfRange.Warnings);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void BlankPlayerTeamIsAnError(string team)
    {
        var validation = CareerConfig.FromPreset(CareerPreset.Balanced).WithPlayerTeam(team).Validate();
        var issue = Assert.Single(validation.Errors);
        Assert.Equal(CareerConfigCodes.PlayerTeamRequired, issue.Code);
    }

    [Fact]
    public void UnknownEnumValuesAreErrors()
    {
        var validation = new CareerConfig(
            (PeopleSource)99,
            (RulesSource)99,
            (AiBehavior)99,
            historyStrength: 5,
            randomnessLevel: 50,
            (FatalityLevel)99,
            startYear: 1950,
            playerTeam: CareerConfig.NewTeam,
            noNumbers: false).Validate();

        Assert.Equal(
            [
                CareerConfigCodes.PeopleSource,
                CareerConfigCodes.RulesSource,
                CareerConfigCodes.AiBehavior,
                CareerConfigCodes.FatalityLevel,
            ],
            validation.Errors.Select(error => error.Code).ToArray());
    }

    [Fact]
    public void ValidateReturnsEveryBrokenRuleTogether()
    {
        var validation = new CareerConfig(
            PeopleSource.RealNamesRandomSkills,
            RulesSource.VotedEachSeason,
            AiBehavior.ReplayHistory,
            historyStrength: 11,
            randomnessLevel: -1,
            FatalityLevel.Off,
            startYear: 2027,
            playerTeam: "",
            noNumbers: false).Validate();

        Assert.Equal(
            [
                CareerConfigCodes.HistoryStrengthRange,
                CareerConfigCodes.RandomnessRange,
                CareerConfigCodes.PlayerTeamRequired,
                CareerConfigCodes.ReplayNeedsHistoricalRules,
                CareerConfigCodes.ReplayNeedsRealPeople,
                CareerConfigCodes.LateStartNeedsGeneratedPeople,
            ],
            validation.Errors.Select(error => error.Code).ToArray());
        Assert.Empty(validation.Warnings);
    }

    [Fact]
    public void NullPlayerTeamIsRejectedByTheConstructor()
    {
        Assert.Throws<ArgumentNullException>(() => CareerConfig.FromPreset(CareerPreset.Balanced).WithPlayerTeam(null!));
    }

    [Fact]
    public void CanonicalJsonPropertyOrderAndHashAreStable()
    {
        var json = CareerConfig.FromPreset(CareerPreset.Balanced).ToCanonicalJson();
        Assert.Equal(BalancedJson, json);
        Assert.Equal(json, CareerConfig.FromPreset(CareerPreset.Balanced).ToCanonicalJson());
        Assert.Equal(BalancedHash, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))));

        var previous = -1;
        foreach (var name in new[]
        {
            "peopleSource",
            "rulesSource",
            "aiBehavior",
            "historyStrength",
            "randomnessLevel",
            "fatalityLevel",
            "startYear",
            "playerTeam",
            "noNumbers",
            "presetName",
        })
        {
            var index = json.IndexOf("\"" + name + "\"", StringComparison.Ordinal);
            Assert.True(index > previous);
            previous = index;
        }
    }

    [Fact]
    public void CanonicalJsonEscapesPlayerTeamAndRoundTrips()
    {
        var config = CareerConfig.FromPreset(CareerPreset.Balanced).WithPlayerTeam("a\"b\\c\n");
        var json = config.ToCanonicalJson();
        Assert.Contains("\"playerTeam\":\"a\\\"b\\\\c\\n\"", json, StringComparison.Ordinal);
        Assert.Equal(config, CareerConfigCodec.Read(json));
        Assert.Equal(json, CareerConfigCodec.Read(json).ToCanonicalJson());
    }

    [Fact]
    public void CodecRejectsNonCanonicalPayloads()
    {
        var json = CareerConfig.FromPreset(CareerPreset.Balanced).ToCanonicalJson();
        var reordered = json.Replace(
            "\"peopleSource\":\"RealPotential\",\"rulesSource\":\"Historical\"",
            "\"rulesSource\":\"Historical\",\"peopleSource\":\"RealPotential\"",
            StringComparison.Ordinal);
        var wrongPreset = json.Replace(
            "\"presetName\":\"Balanced\"",
            "\"presetName\":\"Custom\"",
            StringComparison.Ordinal);
        var extra = json.Insert(json.Length - 1, ",\"extra\":1");

        Assert.Throws<InvalidDataException>(() => CareerConfigCodec.Read(reordered));
        Assert.Throws<InvalidDataException>(() => CareerConfigCodec.Read(wrongPreset));
        Assert.Throws<InvalidDataException>(() => CareerConfigCodec.Read(extra));
        Assert.Throws<InvalidDataException>(() => CareerConfigCodec.Read("{\"peopleSource\":\"RealPotential\"}"));
        var emptyPreset = json.Replace("\"presetName\":\"Balanced\"", "\"presetName\":\"\"", StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => CareerConfigCodec.Read(emptyPreset));
        Assert.Throws<InvalidDataException>(() => CareerConfigCodec.Read(""));
        Assert.Throws<ArgumentNullException>(() => CareerConfigCodec.Read(null!));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    [InlineData(10, 10)]
    [InlineData(50, 5)]
    [InlineData(55, 6)]
    [InlineData(100, 10)]
    [InlineData(150, 10)]
    public void AStoredHistoryStrengthAboveTenLoadsAsRoundOfValueOverTen(int stored, int loaded)
    {
        Assert.Equal(loaded, CareerConfig.ScaleLegacyHistoryStrength(stored));
        var config = CareerConfig.FromPreset(CareerPreset.Balanced).WithHistoryStrength(stored);
        var read = CareerConfigCodec.Read(config.ToCanonicalJson());
        Assert.Equal(loaded, read.HistoryStrength);
        Assert.Equal(loaded / 10.0, read.HistoryStrengthFraction);
    }

    private static void AssertValid(CareerConfig config)
    {
        var validation = config.Validate();
        Assert.True(validation.IsValid);
        Assert.Empty(validation.Errors);
        Assert.Empty(validation.Warnings);
    }
}
