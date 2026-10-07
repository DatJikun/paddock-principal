namespace Paddock.SimRunner;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 0 && SimRunnerCli.IsHelp(args[0]))
        {
            SimRunnerCli.WriteHelp(Console.Out);
            return 0;
        }

        return args.Length == 0 ? RngCommand.Execute(args, Console.Out, Console.Error) : args[0] switch
        {
            "rng" => RngCommand.Execute(args, Console.Out, Console.Error),
            "gen-people" => GenPeopleCommand.Execute(args, Console.Out, Console.Error),
            "toy" => ToyCommand.Execute(args, Console.Out, Console.Error),
            "vote-sim" => VoteSimCommand.Execute(args, Console.Out, Console.Error),
            "config" => ConfigCommand.Execute(args, Console.Out, Console.Error),
            RaceReplayCommand.Name => RaceReplayCommand.Execute(args, Console.Out, Console.Error),
            Paddock.SimRunner.Calibration.CalibrateRaceCommand.Name => Paddock.SimRunner.Calibration.CalibrateRaceCommand.Execute(args, Console.Out, Console.Error),
            RaceCommand.Name => RaceCommand.Execute(args, Console.Out, Console.Error),
            I18nCheckCommand.Name => I18nCheckCommand.Execute(args, Console.Out, Console.Error),
            InitWorldCommand.Name => InitWorldCommand.Execute(args, Console.Out, Console.Error),
            RunCommand.Name => RunCommand.Execute(args, Console.Out, Console.Error),
            PlayCommand.Name => PlayCommand.Execute(args, Console.In, Console.Out, Console.Error),
            ScenarioCommand.Name => ScenarioCommand.Execute(args, Console.Out, Console.Error),
            StartSweepCommand.Name => StartSweepCommand.Execute(args, Console.Out, Console.Error),
            _ => Unknown(args[0]),
        };
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine(
            "Unknown command: " + command + ". Expected: " + string.Join(", ", SimRunnerCli.Commands.Select(entry => entry.Name)) + ".");
        return 1;
    }
}
