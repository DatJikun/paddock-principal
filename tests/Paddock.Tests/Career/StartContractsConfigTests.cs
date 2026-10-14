using Paddock.Domain.Career;
using Paddock.Persistence;

namespace Paddock.Tests.Career;

/// <summary>The start-contracts option of the career config (#325): a default that changes no byte of an existing career, saved only when chosen.</summary>
public class StartContractsConfigTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-start-contracts-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void RealContractsAreTheDefaultOfEveryPresetAndTheirConfigBytesDoNotChange()
    {
        Assert.All(new[] { CareerPreset.MostHistorical, CareerPreset.Balanced, CareerPreset.Chaos }, preset =>
        {
            var config = CareerConfig.FromPreset(preset);
            Assert.Equal(StartContracts.Real, config.StartContracts);
            Assert.DoesNotContain("startContracts", config.ToCanonicalJson(), StringComparison.Ordinal);
            Assert.Equal(config, CareerConfigCodec.Read(config.ToCanonicalJson()));
        });
    }

    [Fact]
    public void TheOldOptionIsSavedBetweenTheRulesSourceAndTheAiBehaviorAndRoundTrips()
    {
        var config = CareerConfig.FromPreset(CareerPreset.Balanced).WithStartContracts(StartContracts.AllEndThisYear);

        var json = config.ToCanonicalJson();

        Assert.Contains("\"rulesSource\":\"Historical\",\"startContracts\":\"AllEndThisYear\",\"aiBehavior\"", json, StringComparison.Ordinal);
        Assert.Equal(config, CareerConfigCodec.Read(json));
        Assert.Equal(json, CareerConfigCodec.Read(json).ToCanonicalJson());
        // Not an axis of a preset: choosing it does not turn Balanced into a custom career.
        Assert.Equal(CareerPreset.Balanced, config.WithStartYear(CareerConfig.MinStartYear).PresetName);
    }

    [Fact]
    public void BothOptionalPropertiesCanBeSavedTogetherInOrder()
    {
        var config = CareerConfig.FromPreset(CareerPreset.Chaos)
            .WithVoteMode(VoteMode.VoteBank)
            .WithStartContracts(StartContracts.AllEndThisYear);

        var json = config.ToCanonicalJson();

        Assert.Contains("\"voteMode\":\"VoteBank\",\"startContracts\":\"AllEndThisYear\",\"aiBehavior\"", json, StringComparison.Ordinal);
        Assert.Equal(config, CareerConfigCodec.Read(json));
    }

    [Fact]
    public void TheCodecRejectsAWrittenDefaultAnUnknownValueAndAMisplacedProperty()
    {
        var json = CareerConfig.FromPreset(CareerPreset.Balanced).WithStartContracts(StartContracts.AllEndThisYear).ToCanonicalJson();
        var explicitDefault = json.Replace("\"startContracts\":\"AllEndThisYear\"", "\"startContracts\":\"Real\"", StringComparison.Ordinal);
        var unknown = json.Replace("\"startContracts\":\"AllEndThisYear\"", "\"startContracts\":\"Forever\"", StringComparison.Ordinal);
        var misplaced = json.Replace(",\"startContracts\":\"AllEndThisYear\"", "", StringComparison.Ordinal)
            .Insert(json.Length - 1 - ",\"startContracts\":\"AllEndThisYear\"".Length, ",\"startContracts\":\"AllEndThisYear\"");

        Assert.Throws<InvalidDataException>(() => CareerConfigCodec.Read(explicitDefault));
        Assert.Throws<InvalidDataException>(() => CareerConfigCodec.Read(unknown));
        Assert.Throws<InvalidDataException>(() => CareerConfigCodec.Read(misplaced));
    }

    [Fact]
    public void AnUnknownValueIsAConfigError()
    {
        var validation = CareerConfig.FromPreset(CareerPreset.Balanced).WithStartContracts((StartContracts)9).Validate();

        Assert.Equal([CareerConfigCodes.StartContracts], validation.Errors.Select(error => error.Code));
        Assert.True(CareerConfig.FromPreset(CareerPreset.Balanced).WithStartContracts(StartContracts.AllEndThisYear).Validate().IsValid);
    }

    [Fact]
    public void ChangingAnotherAxisKeepsTheOption()
    {
        var config = CareerConfig.FromPreset(CareerPreset.Chaos).WithStartContracts(StartContracts.AllEndThisYear);

        Assert.Equal(StartContracts.AllEndThisYear, config.WithStartYear(1960).StartContracts);
        Assert.Equal(StartContracts.AllEndThisYear, config.WithPeopleSource(PeopleSource.RealPotential).StartContracts);
        Assert.Equal(StartContracts.AllEndThisYear, config.WithRulesSource(RulesSource.Historical).StartContracts);
        Assert.Equal(StartContracts.AllEndThisYear, config.WithVoteMode(VoteMode.VoteBank).StartContracts);
        Assert.Equal(StartContracts.AllEndThisYear, config.WithAiBehavior(AiBehavior.ReactToSituation).StartContracts);
        Assert.Equal(StartContracts.AllEndThisYear, config.WithHistoryStrength(3).StartContracts);
        Assert.Equal(StartContracts.AllEndThisYear, config.WithRandomnessLevel(3).StartContracts);
        Assert.Equal(StartContracts.AllEndThisYear, config.WithFatalityLevel(FatalityLevel.On).StartContracts);
        Assert.Equal(StartContracts.AllEndThisYear, config.WithPlayerTeam("ferrari").StartContracts);
        Assert.Equal(StartContracts.AllEndThisYear, config.WithNoNumbers(true).StartContracts);
        Assert.Equal(StartContracts.Real, config.WithStartContracts(StartContracts.Real).StartContracts);
    }

    [Fact]
    public void TheOptionIsSavedWithTheCareerAndASaveFromBeforeItReadsAsRealContracts()
    {
        var chosen = CareerConfig.FromPreset(CareerPreset.Balanced).WithStartContracts(StartContracts.AllEndThisYear);
        var path = Path.Combine(_directory, "option.paddock");
        using (SaveFile.Create(path, MetaOf(chosen)))
        {
        }

        using (var opened = SaveFile.Open(path))
        {
            Assert.Equal(StartContracts.AllEndThisYear, opened.ReadMeta().CareerConfig.StartContracts);
        }

        var old = CareerConfig.FromPreset(CareerPreset.Balanced).ToCanonicalJson();
        Assert.DoesNotContain("startContracts", old, StringComparison.Ordinal);
        Assert.Equal(StartContracts.Real, CareerConfigCodec.Read(old).StartContracts);
    }

    private static SaveMeta MetaOf(CareerConfig config) => new(
        "n",
        "m",
        "ferrari",
        new DateOnly(1955, 1, 1),
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        42UL,
        config);
}
