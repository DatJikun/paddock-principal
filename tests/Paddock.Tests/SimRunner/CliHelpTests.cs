using System.Reflection;
using System.Text.RegularExpressions;

namespace Paddock.Tests.SimRunner;

/// <summary>
/// <c>--help</c> lists every command the two CLIs dispatch, with flags and one line each.
/// A new switch arm without that line fails this test.
/// </summary>
public class CliHelpTests
{
    [Fact]
    public void SimRunnerHelpListsEveryDispatchedCommand()
    {
        var help = HelpOf(typeof(Paddock.SimRunner.SimRunnerCli));
        Assert.Contains("SimRunner commands:", help, StringComparison.Ordinal);
        foreach (var name in Dispatched(Path.Combine(RepoPaths.Root(), "tools", "Paddock.SimRunner", "Program.cs"), typeof(Paddock.SimRunner.RaceCommand).Assembly))
        {
            Assert.Matches("(?m)^" + Regex.Escape(name) + " — \\S.+$", help);
        }

        foreach (var command in Paddock.SimRunner.SimRunnerCli.Commands)
        {
            Assert.False(string.IsNullOrWhiteSpace(command.Summary));
            Assert.Contains(command.Name, command.Usage, StringComparison.Ordinal);
            Assert.Contains(command.Usage, help, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DataPipelineHelpListsEveryDispatchedCommand()
    {
        var help = HelpOf(typeof(Paddock.DataPipeline.DataPipelineCli));
        Assert.Contains("DataPipeline commands:", help, StringComparison.Ordinal);
        var program = File.ReadAllText(Path.Combine(RepoPaths.Root(), "tools", "Paddock.DataPipeline", "Program.cs"));
        Assert.Contains("validate-authored", program, StringComparison.Ordinal);
        Assert.Matches("(?m)^validate-authored — \\S.+$", help);

        var pipeline = File.ReadAllText(Path.Combine(RepoPaths.Root(), "tools", "Paddock.DataPipeline", "PipelineCommands.cs"));
        foreach (Match match in Regex.Matches(pipeline, "case \"([a-z0-9-]+)\":"))
        {
            Assert.Matches("(?m)^" + Regex.Escape(match.Groups[1].Value) + " — \\S.+$", help);
        }

        foreach (var command in Paddock.DataPipeline.DataPipelineCli.Commands)
        {
            Assert.False(string.IsNullOrWhiteSpace(command.Summary));
            Assert.Contains(command.Name, command.Usage, StringComparison.Ordinal);
            Assert.Contains(command.Usage, help, StringComparison.Ordinal);
        }
    }

    private static string HelpOf(Type cli)
    {
        var write = cli.GetMethod("WriteHelp") ?? throw new InvalidOperationException(cli.Name);
        var stdout = new StringWriter();
        write.Invoke(null, [stdout]);
        return stdout.ToString();
    }

    private static IEnumerable<string> Dispatched(string programPath, Assembly assembly)
    {
        var text = File.ReadAllText(programPath);
        foreach (Match match in Regex.Matches(text, "\"([a-z0-9-]+)\" =>"))
        {
            yield return match.Groups[1].Value;
        }

        foreach (Match match in Regex.Matches(text, @"(\w+)\.Name =>"))
        {
            var type = assembly.GetTypes().Single(candidate => candidate.Name == match.Groups[1].Value);
            yield return (string)type.GetField("Name")!.GetValue(null)!;
        }
    }
}
