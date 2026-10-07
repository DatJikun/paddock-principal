using Microsoft.Data.Sqlite;
using Paddock.Domain.Career;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Tests.Persistence;

namespace Paddock.Tests.Regulation;

/// <summary>The political life survives a save and a resume (#275): the cooldown, the bank, the pending proposals and the results are in the hash and in the file.</summary>
public sealed class RegulationsPersistenceTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-regulations-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private WorldState RoundTrip(WorldState world, string name)
    {
        using var file = SaveFile.Create(Path.Combine(_directory, name + ".paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(file);
        repository.SaveWorld(world, world.CurrentDate);
        return repository.LoadWorld();
    }

    [Fact]
    public void ASectionWithBallotsResultsCooldownsAndBanksRoundTripsWithTheSameHash()
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { Seed = 5, Mode = VoteMode.VoteBank, Capital = _ => 400_000 });
        harness.LiveTo(new GameDate(1956, 3, 1));
        var section = harness.Section;
        var series = section.Find(PoliticsHarness.Series)!;
        Assert.NotEmpty(series.Ballot);
        Assert.Contains(series.Ballot, item => item.IsResolved);
        Assert.Contains(series.Teams, team => team.Bank > 0);

        var loaded = RoundTrip(harness.World, "round-trip");

        Assert.Equal(harness.World.StateHash(), loaded.StateHash());
        var back = loaded.Section<RegulationsSection>(RegulationsSection.SectionName)!.Find(PoliticsHarness.Series)!;
        Assert.Equal(series.Season, back.Season);
        Assert.Equal(series.Teams.Select(t => (t.TeamId, t.Leaning, t.ProposeFromSeason, t.Bank)), back.Teams.Select(t => (t.TeamId, t.Leaning, t.ProposeFromSeason, t.Bank)));
        Assert.Equal(series.Ballot.Count, back.Ballot.Count);
        Assert.Equal(series.Rejected.Count, back.Rejected.Count);
        Assert.Equal(series.Values, back.Values);
        Assert.Equal(series.Ballot.Select(i => i.Result?.ReasonKey), back.Ballot.Select(i => i.Result?.ReasonKey));
        Assert.Equal(series.Ballot.SelectMany(i => i.Result?.Stances ?? []).Count(), back.Ballot.SelectMany(i => i.Result?.Stances ?? []).Count());
    }

    [Fact]
    public void AnOpenBallotWithVotesAndPendingProposalsRoundTripsToo()
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { AllHuman = true, TeamCount = 6 });
        harness.LiveFrom(new GameDate(1955, 1, 2), new GameDate(1955, 2, 10));
        var choice = harness.Politics.ChoicesFor(harness.Of(), 1955, new HashSet<string>(StringComparer.Ordinal)).First();
        harness.Politics.Propose(PoliticsHarness.Series, "t01", choice.DimensionId, choice.Value, harness.Today);
        var pendingHash = harness.World.StateHash();
        var withPending = RoundTrip(harness.World, "pending");
        Assert.Equal(pendingHash, withPending.StateHash());
        Assert.Single(withPending.Section<RegulationsSection>(RegulationsSection.SectionName)!.Find(PoliticsHarness.Series)!.Pending);

        harness.LiveTo(new GameDate(1955, 5, 1));
        var item = harness.Items().Single(i => i.Origin == BallotOrigin.Teams);
        harness.Politics.Cast(PoliticsHarness.Series, "t02", item.Id, "v1", 0, harness.Today);
        harness.Politics.Cast(PoliticsHarness.Series, "t03", item.Id, BallotOptions.Abstain, 0, harness.Today);

        var loaded = RoundTrip(harness.World, "open");

        Assert.Equal(harness.World.StateHash(), loaded.StateHash());
        var back = loaded.Section<RegulationsSection>(RegulationsSection.SectionName)!.Find(PoliticsHarness.Series)!.ItemOf(item.Id)!;
        Assert.Equal(["t02:v1:0", "t03:abstain:0"], back.Votes.Select(v => $"{v.TeamId}:{v.Option}:{v.Spent}"));
        Assert.Null(back.Result);
        Assert.Equal(["t01"], back.Variants.Single().ProposerTeamIds);
    }

    [Fact]
    public void TheCooldownAndTheBankOfATeamEnterTheStateHash()
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { Seed = 3, Mode = VoteMode.VoteBank });
        harness.LiveFrom(new GameDate(1955, 1, 2), new GameDate(1955, 1, 5));
        var section = harness.Section;
        var series = section.Find(PoliticsHarness.Series)!;
        var team = series.Teams[0];
        var before = harness.World.StateHash();

        harness.SetSection(section.WithSeries(series.WithTeam(team.WithProposeFrom(team.ProposeFromSeason + 1))));
        var otherCooldown = harness.World.StateHash();
        harness.SetSection(section.WithSeries(series.WithTeam(team.WithBank(team.Bank + 1))));
        var otherBank = harness.World.StateHash();

        Assert.NotEqual(before, otherCooldown);
        Assert.NotEqual(before, otherBank);
        Assert.NotEqual(otherCooldown, otherBank);
    }

    [Fact]
    public void TheCooldownAndTheFutureSurviveSaveAndResume()
    {
        var options = new HarnessOptions { Seed = 9, Capital = _ => 400_000 };
        var whole = PoliticsHarness.Create(options);
        whole.LiveTo(new GameDate(1955, 8, 1));
        var proposers = whole.Of().Teams.Where(team => team.ProposeFromSeason > 1958).Select(team => team.TeamId).ToArray();
        var cooldowns = whole.Of().Teams.Select(team => (team.TeamId, team.ProposeFromSeason)).ToArray();

        var loaded = RoundTrip(whole.World, "resume");
        Assert.Equal(cooldowns, loaded.Section<RegulationsSection>(RegulationsSection.SectionName)!.Find(PoliticsHarness.Series)!.Teams.Select(team => (team.TeamId, team.ProposeFromSeason)));

        var resumed = PoliticsHarness.Create(options);
        resumed.Resume(loaded, new GameDate(1955, 8, 1));
        whole.LiveTo(new GameDate(1958, 12, 31));
        resumed.LiveTo(new GameDate(1958, 12, 31));

        Assert.Equal(whole.World.StateHash(), resumed.World.StateHash());
        Assert.NotNull(proposers);
    }

    [Fact]
    public void ASectionStoredUnderSchemaOneIsReadAsTheWorldChampionshipWithNoTeamsAndNoBallot()
    {
        var path = Path.Combine(_directory, "schema1.paddock");
        using (SaveFile.Create(path, WorldFixtures.Meta()))
        {
        }

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite }.ToString());
        connection.Open();
        using var transaction = connection.BeginTransaction();
        foreach (var sql in new[]
        {
            "INSERT INTO regulations_meta (singleton, season) VALUES (1, 1961)",
            "INSERT INTO regulations_values (dimension_id, value) VALUES ('safety_car', 'physical'), ('refuelling', 'banned')",
            "INSERT INTO regulations_rejected (dimension_id, value, season) VALUES ('safety_car', 'none', 1960)",
        })
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        var store = new RegulationsSectionStore();
        var section = (RegulationsSection)store.Load(connection, 1);

        var series = Assert.Single(section.Series);
        Assert.Equal(SeriesIds.WorldChampionship, series.SeriesId);
        Assert.Equal(1961, series.Season);
        Assert.Equal("physical", series.RuleSetFor(1961)!.Value("safety_car"));
        Assert.Equal("physical", series.RuleSetFor(1962)!.Value("safety_car"));
        Assert.Empty(series.Teams);
        Assert.Empty(series.Ballot);
        Assert.Single(series.Rejected);

        // The next save writes the new tables and empties the old ones.
        store.Replace(connection, transaction, section);
        using var count = connection.CreateCommand();
        count.Transaction = transaction;
        count.CommandText = "SELECT (SELECT COUNT(*) FROM regulations_meta) + (SELECT COUNT(*) FROM regulations_values) + (SELECT COUNT(*) FROM regulations_rejected)";
        Assert.Equal(0L, count.ExecuteScalar());
        var again = (RegulationsSection)store.Load(connection, 2);
        Assert.Equal(section.Series[0].Values, again.Series[0].Values);
        Assert.Equal(1961, again.Series[0].Season);
    }

    [Fact]
    public void TheCurrentSchemaVersionIsTheNextOneAfterTheInfrastructureSection()
    {
        Assert.Equal(28, SaveMigrations.CurrentVersion);
        Assert.Equal(2, new RegulationsSectionStore().SchemaVersion);
        Assert.Equal(2, RegulationsSection.Create(1955, new Dictionary<string, string> { ["a"] = "b" }, []).SchemaVersion);
    }

    [Fact]
    public void TheVoteModeIsSavedWithTheCareerAndOldSavesReadAsOneVoteEach()
    {
        var voted = CareerConfig.FromPreset(CareerPreset.Chaos).WithVoteMode(VoteMode.VoteBank);
        var meta = new SaveMeta(
            "n",
            "m",
            "ferrari",
            new DateOnly(1955, 1, 1),
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            42UL,
            voted);
        var path = Path.Combine(_directory, "mode.paddock");
        using (SaveFile.Create(path, meta))
        {
        }

        using (var opened = SaveFile.Open(path))
        {
            Assert.Equal(VoteMode.VoteBank, opened.ReadMeta().CareerConfig.VoteMode);
            Assert.Equal(CareerPreset.Custom, opened.ReadMeta().CareerConfig.PresetName);
        }

        // A save from before the vote mode existed stores no such property; it keeps the bytes it had and reads as one vote each.
        var old = CareerConfig.FromPreset(CareerPreset.Chaos).ToCanonicalJson();
        Assert.DoesNotContain("voteMode", old, StringComparison.Ordinal);
        Assert.Equal(VoteMode.OneVoteEach, CareerConfigCodec.Read(old).VoteMode);
    }
}
