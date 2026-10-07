namespace Paddock.SimRunner;

/// <summary>One command line of <c>SimRunner --help</c>: the name, the flags, and one sentence.</summary>
public sealed record SimRunnerCommandHelp(string Name, string Usage, string Summary);

/// <summary><c>--help</c> and <c>help</c> for SimRunner. Every command the program dispatches is listed here.</summary>
public static class SimRunnerCli
{
    public static IReadOnlyList<SimRunnerCommandHelp> Commands { get; } =
    [
        new("rng", "rng --seed <ulong> --stream <name> --season <int> --count <int> [--round <int>]", "Draws values from one named RNG stream."),
        new("gen-people", "gen-people --seed <ulong> --count <int> --season <int> [--lang en|pl]", "Generates people and prints them."),
        new("toy", "toy --seed <ulong> --steps <int> [--spy]", "Runs the toy decision loop and prints the state hash."),
        new("vote-sim", "vote-sim --from-year <Y> --seasons <N> --seed <S> [--data <dir>]", "Runs the voted-regulations stand-in and prints which rules changed."),
        new("config", "config --preset <name> [--axis <name>=<value> ...]", "Prints one career preset, with optional axis overrides."),
        new(RaceReplayCommand.Name, "race-replay --seed <N> [--season <Y>] [--round <R>] [--speed <X>]", "Replays one weekend as timed text lines."),
        new(Paddock.SimRunner.Calibration.CalibrateRaceCommand.Name, "calibrate-race --from <Y> --to <Y> [--stride <n>] [--seeds <n>] [--seed <ulong>] [--cache <dir>] [--out <file>]", "Compares simulated race statistics with the historical cache."),
        new(RaceCommand.Name, "race --year <Y> --round <N> [--seed <S>] [--grid fixture] [--spy] [--lang pl|en] [--verbose] [--log] [--hash] [--timing] | race --season <Y> [--seed <S>] [--lang pl|en]", "Runs one race weekend, or every round of a season, and prints the report."),
        new(I18nCheckCommand.Name, "i18n-check [--root <repo>]", "Checks that player-facing keys exist in both languages."),
        new(InitWorldCommand.Name, "init-world --preset <name> --year <Y> --seed <N> [--lang en|pl] [--data-root <dir>] [--schedule <file> --drivers <file>]", "Builds a starting world and prints counts, gaps and the state hash."),
        new(RunCommand.Name, "run --preset <name> --from <Y> --to <Y> --seed <N> [--save <path>] [--lang en|pl] [--data-root <dir>] [--schedule <file> --drivers <file>] | run --resume <save> --to <Y> [--save <path>]", "Lives a career and prints one line per season."),
        new(PlayCommand.Name, "play [--load <file>] [--lang en|pl] [--seed <N>] [--data-root <dir>] [--name <text>] [--autosave <path>]", "Starts or loads a career and reads the wizard and the shell from stdin."),
        new(StartSweepCommand.Name, "start-sweep --from <Y> --to <Y> --seed <N> [--preset <name>] [--data-root <dir>] [--schedule <file> --drivers <file>]", "Builds the opening world of every start year and plays its first race; prints one table row per year."),
        new(ScenarioCommand.Name, "scenario phase4-1955 --seed <N> --team <id> [--out <dir>] [--monkey] [--seeds <n>] [--data-root <dir>]", "Plays a 1955 career with a bot manager and writes the phase-4 gate report."),
    ];

    public static bool IsHelp(string token) =>
        token is "help" or "--help" or "-h";

    public static void WriteHelp(TextWriter stdout)
    {
        ArgumentNullException.ThrowIfNull(stdout);
        stdout.WriteLine("SimRunner commands:");
        foreach (var command in Commands)
        {
            stdout.WriteLine(command.Name + " — " + command.Summary);
            stdout.WriteLine("    " + command.Usage);
        }
    }
}
