using Microsoft.Data.Sqlite;
using Paddock.Persistence;
using Paddock.SimRunner;

namespace Paddock.Tests.SimRunner;

/// <summary><c>run --resume</c>: the same future as a run that never stopped, and refusals that name the reason (issue #123).</summary>
[Trait("Category", "Slow")]
public sealed class ResumeCommandTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-resume-cmd-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Theory]
    [InlineData("Chaos", 7UL)]
    [InlineData("Balanced", 11UL)]
    public void ASplitRunPrintsAndSavesTheSameSeasonsAsTheRunThatNeverStopped(string preset, ulong seed)
    {
        var whole = Path.Combine(_directory, preset + "-whole.paddock");
        var half = Path.Combine(_directory, preset + "-half.paddock");
        var resumed = Path.Combine(_directory, preset + "-resumed.paddock");
        var s = seed.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var wholeLines = Run(["run", "--preset", preset, "--from", "1950", "--to", "1962", "--seed", s, "--save", whole]);
        Run(["run", "--preset", preset, "--from", "1950", "--to", "1956", "--seed", s, "--save", half]);
        var resumedLines = Run(["run", "--resume", half, "--to", "1962", "--save", resumed]);

        Assert.Contains(resumedLines, line => line.StartsWith("Resumed " + preset + " (opened 1950) on 1957-01-01, running through 1962", StringComparison.Ordinal));
        var seasons = resumedLines.Where(line => line.StartsWith("Season ", StringComparison.Ordinal)).ToArray();
        Assert.Equal(6, seasons.Length);
        Assert.StartsWith("Season 1957:", seasons[0], StringComparison.Ordinal);
        Assert.Equal(wholeLines.Where(line => line.StartsWith("Season ", StringComparison.Ordinal)).TakeLast(6), seasons);

        var a = CareerSaveReader.Read(whole);
        var b = CareerSaveReader.Read(resumed);
        Assert.Equal(a.Session.World.StateHash(), b.Session.World.StateHash());
        Assert.Equal(a.Session.Years, b.Session.Years);
        Assert.Equal(
            a.Session.World.Section<Paddock.Domain.Pool.TalentPoolSection>(Paddock.Domain.Pool.TalentPoolSection.SectionName)!.Members.Select(member => member.Id.Value + member.Handle),
            b.Session.World.Section<Paddock.Domain.Pool.TalentPoolSection>(Paddock.Domain.Pool.TalentPoolSection.SectionName)!.Members.Select(member => member.Id.Value + member.Handle));
        Assert.Equal(a.Session.Intakes, b.Session.Intakes);
        Assert.Equal(a.Session.ContractExpiries, b.Session.ContractExpiries);
        Assert.Equal(a.Session.OpenedYear, b.Session.OpenedYear);
        Assert.Equal(a.Session.Clock.NextEventId, b.Session.Clock.NextEventId);
        Assert.Equal(a.Meta.MasterSeed, b.Meta.MasterSeed);
        Assert.Equal(a.Meta.WorldDataHash, b.Meta.WorldDataHash);
        Assert.Equal(a.Meta.CareerConfig, b.Meta.CareerConfig);
        Assert.Equal(a.Meta.CareerName, b.Meta.CareerName);
        Assert.Equal(a.Host.NextSubmissionNumber, b.Host.NextSubmissionNumber);
    }

    [Fact]
    public void ASaveThatWasResumedCanBeResumedAgainAndTheTitleFollowsTheLanguage()
    {
        var half = Path.Combine(_directory, "half.paddock");
        var middle = Path.Combine(_directory, "middle.paddock");
        var wholeLines = Run(["run", "--preset", "Chaos", "--from", "1950", "--to", "1960", "--seed", "3"]);
        Run(["run", "--preset", "Chaos", "--from", "1950", "--to", "1952", "--seed", "3", "--save", half]);
        Run(["run", "--resume", half, "--to", "1956", "--save", middle]);
        var last = Run(["run", "--resume", middle, "--to", "1960", "--lang", "pl"]);

        Assert.Contains(last, line => line.StartsWith("Wznowiono Chaos (start 1950) dnia 1957-01-01", StringComparison.Ordinal));
        Assert.Equal(
            wholeLines.Where(line => line.StartsWith("Season ", StringComparison.Ordinal)).TakeLast(4).Select(HashOf),
            last.Where(line => line.StartsWith("Sezon ", StringComparison.Ordinal)).Select(HashOf));
    }

    [Theory]
    [InlineData("--preset", "Chaos")]
    [InlineData("--from", "1950")]
    [InlineData("--seed", "7")]
    public void ResumeTakesPresetStartAndSeedFromTheSave(string flag, string value)
    {
        var half = Saved("flags.paddock");

        var error = Fail(["run", "--resume", half, "--to", "1955", flag, value]);

        Assert.Contains("--resume takes the preset", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ResumeRefusesBadInvocationsAndSaysWhy()
    {
        var half = Saved("bad.paddock");

        Assert.Contains("--to", Fail(["run", "--resume", half]), StringComparison.Ordinal);
        Assert.Contains("different file", Fail(["run", "--resume", half, "--to", "1955", "--save", half]), StringComparison.Ordinal);
        Assert.Contains("--lang", Fail(["run", "--resume", half, "--to", "1955", "--lang", "de"]), StringComparison.Ordinal);
        Assert.Contains("go together", Fail(["run", "--resume", half, "--to", "1955", "--schedule", "a.json"]), StringComparison.Ordinal);
        Assert.Contains("1952 or a later year", Fail(["run", "--resume", half, "--to", "1951"]), StringComparison.Ordinal);
        Assert.NotEqual(string.Empty, Fail(["run", "--resume", Path.Combine(_directory, "missing.paddock"), "--to", "1955"]));
    }

    [Fact]
    public void ResumeRefusesASaveWhoseBaseDataChanged()
    {
        var half = Saved("data.paddock");
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = half, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE meta SET world_data_hash = '" + new string('0', 64) + "'";
            command.ExecuteNonQuery();
        }

        var error = Fail(["run", "--resume", half, "--to", "1955"]);

        Assert.Contains("changed since this save was written", error, StringComparison.Ordinal);
        Assert.Contains("cannot be resumed to the same future", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ResumeRefusesASaveWithoutRunState()
    {
        var half = Saved("stripped.paddock");
        using (var save = SaveFile.Open(half))
        {
            var repository = new WorldRepository(save);
            var snapshot = repository.LoadAll();
            repository.SaveAll(
                new WorldSnapshot(snapshot.World, [], snapshot.NextEventId, 0, snapshot.Managers, [], 1) { RngStates = snapshot.RngStates },
                snapshot.World.CurrentDate);
        }

        Assert.Contains("cannot be resumed exactly", Fail(["run", "--resume", half, "--to", "1955"]), StringComparison.Ordinal);
    }

    [Fact]
    public void ResumeRefusesAFileThatHoldsAnUnknownTag()
    {
        var half = Saved("tag.paddock");
        using (var save = SaveFile.Open(half))
        {
            var repository = new WorldRepository(save);
            var snapshot = repository.LoadAll();
            repository.SaveAll(
                new WorldSnapshot(
                    snapshot.World,
                    [],
                    snapshot.NextEventId,
                    0,
                    snapshot.Managers,
                    [new StoredCommand(1, "ai:paddock", new DateOnly(1951, 12, 31), "buy-engine/1", "{}")],
                    2)
                {
                    Run = snapshot.Run,
                    RngStates = snapshot.RngStates,
                },
                snapshot.World.CurrentDate);
        }

        var error = Fail(["run", "--resume", half, "--to", "1955"]);

        Assert.Contains("buy-engine/1", error, StringComparison.Ordinal);
    }

    private string Saved(string name)
    {
        var path = Path.Combine(_directory, name);
        Run(["run", "--preset", "Chaos", "--from", "1950", "--to", "1951", "--seed", "5", "--save", path]);
        return path;
    }

    private static string HashOf(string line) => System.Text.RegularExpressions.Regex.Match(line, "[0-9a-f]{64}").Value;

    private static string[] Run(string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = RunCommand.Execute(args, stdout, stderr);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.Equal(0, code);
        return stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string Fail(string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = RunCommand.Execute(args, stdout, stderr);
        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stdout.ToString());
        return stderr.ToString();
    }
}
