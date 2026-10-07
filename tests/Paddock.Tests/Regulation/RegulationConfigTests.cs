using Paddock.Domain.Career;
using Paddock.Persistence;

namespace Paddock.Tests.Regulation;

/// <summary>The vote mode next to the rules source (#275): three modes at career start, a default that changes no byte of an existing career.</summary>
public class RegulationConfigTests
{
    [Fact]
    public void TheThreeModesAreHistoricalVotedOneVoteEachAndVotedWithABank()
    {
        var historical = CareerConfig.FromPreset(CareerPreset.Balanced);
        var oneVote = CareerConfig.FromPreset(CareerPreset.Chaos);
        var bank = oneVote.WithVoteMode(VoteMode.VoteBank);

        Assert.Equal(RulesSource.Historical, historical.RulesSource);
        Assert.Equal(RulesSource.VotedEachSeason, oneVote.RulesSource);
        Assert.Equal(VoteMode.OneVoteEach, oneVote.VoteMode);
        Assert.Equal(VoteMode.VoteBank, bank.VoteMode);
        Assert.All(new[] { CareerPreset.MostHistorical, CareerPreset.Balanced, CareerPreset.Chaos }, preset =>
            Assert.Equal(VoteMode.OneVoteEach, CareerConfig.FromPreset(preset).VoteMode));
        Assert.True(bank.Validate().IsValid);
    }

    [Fact]
    public void HistoricalIsTheDefaultAndItsConfigBytesDoNotChange()
    {
        var json = CareerConfig.FromPreset(CareerPreset.Balanced).ToCanonicalJson();

        Assert.DoesNotContain("voteMode", json, StringComparison.Ordinal);
        Assert.Equal(RulesSource.Historical, CareerConfigCodec.Read(json).RulesSource);
        Assert.Equal(VoteMode.OneVoteEach, CareerConfigCodec.Read(json).VoteMode);
    }

    [Fact]
    public void ABankModeIsSavedBetweenTheRulesSourceAndTheAiBehaviorAndRoundTrips()
    {
        var config = CareerConfig.FromPreset(CareerPreset.Chaos).WithVoteMode(VoteMode.VoteBank);

        var json = config.ToCanonicalJson();

        Assert.Contains("\"rulesSource\":\"VotedEachSeason\",\"voteMode\":\"VoteBank\",\"aiBehavior\"", json, StringComparison.Ordinal);
        Assert.Equal(config, CareerConfigCodec.Read(json));
        Assert.Equal(json, CareerConfigCodec.Read(json).ToCanonicalJson());
    }

    [Fact]
    public void TheCodecRejectsAWrittenDefaultAnUnknownModeAndAMisplacedProperty()
    {
        var json = CareerConfig.FromPreset(CareerPreset.Chaos).WithVoteMode(VoteMode.VoteBank).ToCanonicalJson();
        var explicitDefault = json.Replace("\"voteMode\":\"VoteBank\"", "\"voteMode\":\"OneVoteEach\"", StringComparison.Ordinal);
        var unknown = json.Replace("\"voteMode\":\"VoteBank\"", "\"voteMode\":\"Auction\"", StringComparison.Ordinal);
        var misplaced = json.Replace(",\"voteMode\":\"VoteBank\"", "", StringComparison.Ordinal).Insert(json.Length - 1 - ",\"voteMode\":\"VoteBank\"".Length, ",\"voteMode\":\"VoteBank\"");

        Assert.Throws<InvalidDataException>(() => CareerConfigCodec.Read(explicitDefault));
        Assert.Throws<InvalidDataException>(() => CareerConfigCodec.Read(unknown));
        Assert.Throws<InvalidDataException>(() => CareerConfigCodec.Read(misplaced));
    }

    [Fact]
    public void AVoteModeMeansNothingForHistoricalRulesAndIsOnlyAWarning()
    {
        var historical = CareerConfig.FromPreset(CareerPreset.Balanced).WithVoteMode(VoteMode.VoteBank);

        var validation = historical.Validate();

        Assert.True(validation.IsValid);
        Assert.Equal([CareerConfigCodes.VoteModeWithHistoricalRules], validation.Warnings.Select(warning => warning.Code));
        Assert.Empty(CareerConfig.FromPreset(CareerPreset.Chaos).WithVoteMode(VoteMode.VoteBank).Validate().Warnings);
        Assert.Equal([CareerConfigCodes.VoteMode], CareerConfig.FromPreset(CareerPreset.Balanced).WithVoteMode((VoteMode)9).Validate().Errors.Select(error => error.Code));
    }

    [Fact]
    public void ChangingAnotherAxisKeepsTheVoteMode()
    {
        var config = CareerConfig.FromPreset(CareerPreset.Chaos).WithVoteMode(VoteMode.VoteBank);

        Assert.Equal(VoteMode.VoteBank, config.WithStartYear(1960).VoteMode);
        Assert.Equal(VoteMode.VoteBank, config.WithPeopleSource(PeopleSource.RealPotential).VoteMode);
        Assert.Equal(VoteMode.VoteBank, config.WithRulesSource(RulesSource.VotedEachSeason).VoteMode);
        Assert.Equal(VoteMode.VoteBank, config.WithAiBehavior(AiBehavior.ReactToSituation).VoteMode);
        Assert.Equal(VoteMode.VoteBank, config.WithHistoryStrength(3).VoteMode);
        Assert.Equal(VoteMode.VoteBank, config.WithRandomnessLevel(3).VoteMode);
        Assert.Equal(VoteMode.VoteBank, config.WithFatalityLevel(FatalityLevel.On).VoteMode);
        Assert.Equal(VoteMode.VoteBank, config.WithPlayerTeam("ferrari").VoteMode);
        Assert.Equal(VoteMode.VoteBank, config.WithNoNumbers(true).VoteMode);
    }
}
